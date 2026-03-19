# db-context-correction Design

## Summary

This design corrects and completes the database-awareness portion of the pipeline. The existing `detect-databases` command is a shallow aggregation step — it collects table names recorded by the classifier but performs no analysis of how those tables are actually used. The existing `ingest-schema` command writes per-table documentation to a separate `schema/` directory that nothing downstream consumes. Both commands are rewritten to close those gaps.

`detect-databases` is upgraded to a hybrid scan: it uses the table names already captured by `assembly-classifier` as its known table set, then reads each assembly's `.decompiled.cs` file directly to detect SQL access patterns (SELECT, INSERT, UPDATE, DELETE), flag dynamic SQL and stored procedure calls, and infer which database each table belongs to from connection string literals in the source. Its output, renamed from `db-detection.json` to `database-context.json`, replaces the old flat list with a structured schema that captures per-table access patterns, probable lookup classification, and unresolved reference warnings. `ingest-schema` is then aligned to this richer contract: it requires `database-context.json` as a prerequisite guard, and instead of writing to a separate directory it enriches each component's existing `.ctx.md` file in-place with a `## DB Schema` section. The rename of the output file is propagated to all references across commands, tests, and the process-drop orchestrator.

## Definition of Done

1. `detect-databases.md` rewritten using a hybrid scan approach: `classifier_results` provides the known table list; `.decompiled.cs` files are scanned directly for access patterns (`SELECT`/`INSERT`/`UPDATE`/`DELETE`), probable lookup signals, and database grouping inference from code structure. Output renamed from `db-detection.json` to `database-context.json`, using the full spec schema (`schema_version`, `databases[]`, per-table `access_patterns` + `probable_lookup`, `gaps[]`).
2. `ingest-schema.md` rewritten for full spec alignment: enriches `.ctx.md` files in-place with `## DB Schema` sections, requires `database-context.json` as a prerequisite guard, and drops the `schema/` docs directory output.
3. `db-detection.json` renamed to `database-context.json` in all references across commands, tests, and process-drop.
4. `test-detect-databases.md` and `test-ingest-schema.md` updated to match the new behaviour.

## Acceptance Criteria

### db-context-correction.AC1: detect-databases produces database-context.json with rich scan data
- **db-context-correction.AC1.1 Success:** Output contains `schema_version: "1.0"`, `generated_at`, `suite`, `components_analyzed`, `databases[]`
- **db-context-correction.AC1.2 Success:** Tables from `classifier_results` appear in `database-context.json`
- **db-context-correction.AC1.3 Success:** SELECT access pattern detected from SQL literal on `ordertable` in `FakeSuite.decompiled.cs`
- **db-context-correction.AC1.4 Success:** INSERT access pattern detected from SQL literal on `ordertable` in `FakeSuite.decompiled.cs`
- **db-context-correction.AC1.5 Success:** Table name appearing only in a comment (not a SQL literal) does not produce `access_patterns`
- **db-context-correction.AC1.6 Success:** `probable_lookup: true` set for a table with SELECT-only access AND 3+ referencing assemblies
- **db-context-correction.AC1.7 Success:** String concatenation in a SQL context produces an `unresolved_references` entry with reason `"dynamic SQL"`
- **db-context-correction.AC1.8 Success:** Connection string with `Initial Catalog=X` in decompiled source → `databases[].name` is X
- **db-context-correction.AC1.9 Success:** No detectable connection string → all tables grouped under single database named after `suite_name`
- **db-context-correction.AC1.10 Failure:** Missing or empty `classifier_results` → hard stop with message referencing `/review-drop`
- **db-context-correction.AC1.11 Failure:** Missing manifest → hard stop with message referencing `/pre-classify` and `/review-drop`

### db-context-correction.AC2: ingest-schema enriches .ctx.md files in-place
- **db-context-correction.AC2.1 Success:** `.ctx.md` file with `db_tables` gets `## DB Schema` section appended
- **db-context-correction.AC2.2 Success:** DB Schema section contains correct column table (name, type, nullable, notes)
- **db-context-correction.AC2.3 Success:** Lookup table (`is_lookup: true`) gets Lookup Values subsection with all `lookup_values` entries
- **db-context-correction.AC2.4 Success:** Running ingest-schema twice does not duplicate the `## DB Schema` section — second run replaces it
- **db-context-correction.AC2.5 Success:** Table in `db_tables` not found in `schema-enrichment.json` → informational note in report only; `.ctx.md` unchanged for that table
- **db-context-correction.AC2.6 Failure:** Missing `database-context.json` → hard stop referencing `/detect-databases`
- **db-context-correction.AC2.7 Failure:** Missing `schema-enrichment.json` → hard stop with descriptive error including attempted path
- **db-context-correction.AC2.8 Failure:** `schema-enrichment.json` `schema_version` ≠ `"1.0"` → hard stop
- **db-context-correction.AC2.9 Failure:** `database-context.json` `schema_version` ≠ `"1.0"` → hard stop

### db-context-correction.AC3: File rename consistency
- **db-context-correction.AC3.1 Success:** No file in `commands/`, `tests/scenarios/`, or `tests/fixtures/` contains the string `db-detection.json`

### db-context-correction.AC4: Test scenarios are self-verifying
- **db-context-correction.AC4.1 Success:** `test-detect-databases.md` launched as subagent with command + fixtures produces a criterion-by-criterion PASS/FAIL verdict
- **db-context-correction.AC4.2 Success:** `test-ingest-schema.md` launched as subagent with command + fixtures produces a criterion-by-criterion PASS/FAIL verdict

## Glossary

- **suite**: A named collection of .NET assemblies that belong to a single application or product, as defined in the suite profile. All pipeline commands operate on one suite at a time.
- **suite profile**: A JSON configuration file that defines the suite's name, component folders, and glob patterns used to classify assemblies as suite-owned or third-party.
- **assembly**: A compiled .NET binary (`.dll` or `.exe`). The unit of input throughout the pipeline.
- **.decompiled.cs**: A C# source file produced by ILSpy from a compiled assembly. It is a reconstruction of the original source and is the primary artifact scanned by `detect-databases`.
- **classifier_results**: The section of `classification-manifest.json` written by `assembly-classifier` (via `review-drop`). It records per-assembly metadata including the `db_tables` list of table names referenced by that assembly.
- **classification-manifest.json**: The shared state file for the pipeline. Each command reads it for input and appends its name to `completed_stages` on success.
- **completed_stages**: The array in `classification-manifest.json` that records which pipeline stages have successfully finished. Used by individual commands as prerequisite guards and by `process-drop` for resume logic.
- **database-context.json**: The output file produced by `detect-databases` (replacing `db-detection.json`). The contract between `detect-databases` and `ingest-schema`, containing per-database, per-table access pattern data and unresolved reference warnings.
- **schema-enrichment.json**: An externally supplied file produced by a companion schema extraction plugin. Contains column definitions, types, and lookup values for each table. Consumed by `ingest-schema`.
- **.ctx.md**: A context markdown file generated per assembly by `generate-context`. Contains YAML frontmatter (including `db_tables`) and human-readable reference notes. Enriched in-place by `ingest-schema`.
- **hybrid scan approach**: Strategy used by the rewritten `detect-databases`: combine the table list already extracted by the classifier with a direct scan of decompiled source to determine access patterns and database grouping.
- **access_patterns**: The set of SQL DML verbs (SELECT, INSERT, UPDATE, DELETE) detected for a given table within a decompiled source file. Stored per table in `database-context.json`.
- **probable_lookup**: A heuristic flag on a table entry in `database-context.json`. Set to `true` when a table is accessed only via SELECT and is either referenced by 3+ assemblies or has a name matching common lookup-table suffixes.
- **unresolved_references**: Entries in `database-context.json` recording assemblies where SQL could not be fully analyzed — e.g., string-concatenated queries (dynamic SQL) or stored procedure calls.
- **dynamic SQL**: A SQL query constructed at runtime by string concatenation. Cannot be statically parsed for table names or access patterns; recorded as an `unresolved_references` entry.
- **hard stop**: An immediate command failure that halts execution and emits a message directing the user to a prerequisite command. Used when required inputs are missing or invalid.
- **schema_version**: A string field (`"1.0"`) in both `database-context.json` and `schema-enrichment.json`. Commands validate this field before proceeding; a mismatch triggers a hard stop.
- **self-verifying scenario**: A test scenario file that instructs the subagent to evaluate each pass criterion explicitly and emit a structured PASS/FAIL verdict per criterion, enabling automated test execution without human result interpretation.
- **subagent**: An AI agent instance launched to execute a specific command or test scenario file.
- **ILSpy / ilspycmd**: An open-source .NET decompiler. `ilspycmd` is its command-line form, used by the pipeline to produce `.decompiled.cs` files from compiled assemblies.
- **ADO.NET**: A .NET data-access library for executing raw SQL via `SqlConnection` and `SqlCommand`. The decompiled fixtures use ADO.NET, which is why SQL string literals appear in `.decompiled.cs` files and can be scanned.
- **Initial Catalog**: The connection string keyword specifying the target database name. Scanning for this value is the primary mechanism for inferring `databases[].name` in `database-context.json`.

---

## Architecture

Two commands are rewritten. They form a sequential pair in the optional database enrichment tail of the pipeline: `detect-databases` produces `database-context.json`; `ingest-schema` consumes it (as a guard) alongside `schema-enrichment.json` from the companion schema extraction plugin.

### detect-databases (rewrite)

Hybrid approach: `classifier_results` in the manifest already contains normalized table names per assembly (extracted by `assembly-classifier`). `detect-databases` uses this list as the known table set, then scans each assembly's `.decompiled.cs` directly to determine *how* those tables are accessed.

**Scan logic per assembly:**
- For each known table name, search SQL string literals and parameterized query patterns in the decompiled source.
- Detect adjacent SQL keywords (`SELECT`, `INSERT`, `UPDATE`, `DELETE`) to build `access_patterns[]` per table.
- Flag string concatenation in SQL contexts and `EXEC`/stored procedure calls as `unresolved_references`.

**Database grouping inference:**
- Scan for connection string literals containing `Initial Catalog=`, `Database=`, or `Data Source=`.
- Scan for `SqlConnectionStringBuilder` usage with named database fields.
- Scan for distinct repository base class groupings or namespace segments (e.g., `Data.Reporting` vs `Data.Core`).
- If distinguishable groups are found, assign tables to named databases.
- If no inference is possible, group all tables under a single database named after `suite_name` from the profile.

**Probable lookup detection:** A table is flagged `probable_lookup: true` when access patterns contain only `SELECT` AND (it is referenced in 3+ assemblies OR its name matches common lookup suffixes: `lookup`, `status`, `type`, `code`, `ref`, `list`).

**Output contract — `database-context.json`:**

```json
{
  "schema_version": "1.0",
  "generated_at": "<ISO8601>",
  "suite": "<suite_name from profile>",
  "components_analyzed": ["<component names>"],
  "databases": [
    {
      "name": "<inferred db name or suite_name>",
      "tables": [
        {
          "name": "<normalized table name>",
          "referenced_in": ["<assembly filenames>"],
          "access_patterns": ["SELECT", "INSERT"],
          "probable_lookup": false
        }
      ],
      "unresolved_references": [
        {
          "assembly": "<assembly name>",
          "reason": "dynamic SQL | stored procedure"
        }
      ]
    }
  ],
  "gaps": ["<human-readable notes on detection limitations>"]
}
```

### ingest-schema (rewrite)

Reads `schema-enrichment.json` (from the companion schema extraction plugin) and `database-context.json` (required as a prerequisite guard — hard stop if absent or wrong `schema_version`). Builds a table lookup map from the schema file. For each `.ctx.md` file under `context_output_path`, reads the YAML frontmatter `db_tables` list, looks up each table in the schema map, and writes or replaces a `## DB Schema` section at the end of the file.

The `schema/` docs directory output from the current implementation is removed. `.ctx.md` enrichment is the sole output.

**## DB Schema section format** (appended/replaced in each `.ctx.md`):

```markdown
## DB Schema

### <table_name>
| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| ...    | ...  | ...      | ...   |

**Lookup values:** (only if is_lookup: true)
| Code | Description |
|------|-------------|
| ...  | ...         |
```

Tables in `db_tables` not found in `schema-enrichment.json` are logged as informational notes in the summary report; they do not cause a hard stop or alter the `.ctx.md` file.

---

## Existing Patterns

All pipeline commands in this plugin are markdown instruction files in `commands/`. Test scenarios in `tests/scenarios/` follow a consistent structure: Setup → one or more named Test Cases with Expected Behavior → Pass Criteria. Fixtures in `tests/fixtures/minimal-suite/` provide pre-built data.

The manifest (`classification-manifest.json`) is the shared state contract between stages; commands read it for input and write `completed_stages` on success. Both rewritten commands follow this pattern.

This design introduces one new pattern to scenario files: a **Self-Verification section**. Each scenario instructs the subagent to explicitly evaluate each Pass Criterion and output a structured PASS/FAIL verdict. This enables subagent-automated test execution without human interpretation of results.

---

## Implementation Phases

<!-- START_PHASE_1 -->
### Phase 1: Rewrite detect-databases command and add scan fixtures

**Goal:** Replace the manifest-aggregation implementation with the hybrid scan approach. Add `.decompiled.cs` fixtures needed for new test cases.

**Components:**
- `commands/detect-databases.md` — full rewrite: hybrid scan algorithm, DB grouping inference, probable lookup detection, dynamic SQL flagging, `database-context.json` output schema
- `tests/fixtures/minimal-suite/FakeSuiteWithConnString.decompiled.cs` — new fixture containing a `SqlConnection` with `Initial Catalog=FakeDB`; exercises DB name inference
- `tests/fixtures/minimal-suite/FakeSuiteDynamicSql.decompiled.cs` — new fixture containing string concatenation in a SQL context; exercises `unresolved_references`

**Dependencies:** None (rewrites existing file; new fixtures are standalone)

**Done when:** A subagent launched with `commands/detect-databases.md` + the new fixtures + a fabricated manifest can simulate running the command and produce a `database-context.json` that includes access patterns and inferred DB grouping.
<!-- END_PHASE_1 -->

<!-- START_PHASE_2 -->
### Phase 2: Rewrite test-detect-databases scenario

**Goal:** Replace the existing scenario (which tests manifest aggregation only) with a self-verifying scenario covering the new scan behaviour.

**Components:**
- `tests/scenarios/test-detect-databases.md` — full rewrite with self-verifying structure. Test cases: access pattern detection (SELECT + INSERT on ordertable from FakeSuite.decompiled.cs), probable lookup detection (SELECT-only + multi-assembly reference), DB grouping with connection string present, DB grouping fallback to suite_name, dynamic SQL flagging, missing classifier_results hard stop, missing manifest hard stop.

**Dependencies:** Phase 1 (command spec and fixtures must exist before scenario can reference them)

**Done when:** Subagent launched with `test-detect-databases.md` + `commands/detect-databases.md` + referenced fixtures reports all test cases PASS with explicit criterion-by-criterion verdict. Covers acceptance criteria `db-context-correction.AC1.*`.
<!-- END_PHASE_2 -->

<!-- START_PHASE_3 -->
### Phase 3: Rewrite ingest-schema command and add ctx fixtures

**Goal:** Replace the schema-docs-directory implementation with the ctx-enrichment model. Add fixtures needed for new test cases.

**Components:**
- `commands/ingest-schema.md` — full rewrite: `database-context.json` prerequisite guard, ctx enrichment via direct file editing, `## DB Schema` section format, no `schema/` directory output
- `tests/fixtures/minimal-suite/FakeSuite.ctx.md` — sample pre-generated ctx.md with `db_tables: [ordertable, statuslookup]`; exercises ingest-schema enrichment
- `tests/fixtures/minimal-suite/database-context.json` — sample detect-databases output used as the prerequisite guard in test cases

**Dependencies:** Phase 1 (database-context.json schema must be finalized first)

**Done when:** A subagent launched with `commands/ingest-schema.md` + new fixtures + `tests/fixtures/schema-fixture/schema-enrichment.json` can simulate running the command and produce an enriched `.ctx.md` with a correct `## DB Schema` section.
<!-- END_PHASE_3 -->

<!-- START_PHASE_4 -->
### Phase 4: Rewrite test-ingest-schema scenario

**Goal:** Replace the existing scenario with a self-verifying scenario covering ctx enrichment, the database-context.json guard, and all hard-stop cases.

**Components:**
- `tests/scenarios/test-ingest-schema.md` — full rewrite with self-verifying structure. Test cases: normal ctx enrichment, replacement of existing ## DB Schema section, table not in schema (informational note only), missing database-context.json hard stop, missing schema-enrichment.json hard stop, wrong schema_version hard stop.

**Dependencies:** Phase 3 (command spec and fixtures must exist)

**Done when:** Subagent launched with `test-ingest-schema.md` + `commands/ingest-schema.md` + referenced fixtures reports all test cases PASS. Covers acceptance criteria `db-context-correction.AC2.*` and `db-context-correction.AC4.*`.
<!-- END_PHASE_4 -->

<!-- START_PHASE_5 -->
### Phase 5: Update process-drop and complete file rename

**Goal:** Remove the last reference to `db-detection.json` and ensure the rename is consistent across the plugin.

**Components:**
- `commands/process-drop.md` — update the Step 9 / final summary output block to reference `database-context.json` instead of `db-detection.json`

**Dependencies:** Phases 1–4 (all rewritten files use `database-context.json`; this phase makes process-drop consistent)

**Done when:** No file in `commands/`, `tests/scenarios/`, or `tests/fixtures/` contains the string `db-detection.json`. Covers `db-context-correction.AC3.1`.
<!-- END_PHASE_5 -->

---

## Additional Considerations

**`database-context.json` as a contract:** This file is the interface between `detect-databases` and `ingest-schema`. If its schema changes in the future, both commands must be updated together. The `schema_version` field is the version gate for this contract.

**`probable_lookup` is advisory:** The detection algorithm is heuristic-based. False positives are possible (a frequently-joined non-lookup table may be incorrectly flagged). This is acceptable — the field is informational guidance for the companion schema extraction plugin, not a hard classification.
