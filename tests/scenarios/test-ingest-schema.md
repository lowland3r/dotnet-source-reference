# Test Scenario: ingest-schema

## Setup

The following files must be present before running any test case:

- Suite profile: `tests/fixtures/minimal-suite/test-profile.json`
  - `context_output_path` is `"output/context"` — resolved as `tests/fixtures/minimal-suite/output/context/`
- Schema enrichment: `tests/fixtures/schema-fixture/schema-enrichment.json`
  - Contains database `FakeDB` with tables `ordertable` (3 columns, not lookup) and `statuslookup` (2 columns, is lookup, 3 lookup values)
- Command specification: `commands/ingest-schema.md`

For test cases that test normal enrichment, you will need a `.ctx.md` file in the resolved `context_output_path`. Use the content of `tests/fixtures/minimal-suite/FakeSuite.ctx.md` as the template:

- Place a copy at `tests/fixtures/minimal-suite/output/context/FakeSuite.ctx.md`
- The frontmatter `db_tables` field lists `ordertable` and `statuslookup`
- The file does NOT contain a `## DB Schema` section at the start of each test case (reset to the un-enriched state)

Each test case defines its own `classification-manifest.json` and, where applicable, `database-context.json`. These are placed in `tests/fixtures/minimal-suite/`. After each test case, remove generated outputs and reset fixture files to their original state before proceeding.

## Test Case A: Normal enrichment — correct columns and lookup values

### Setup

Create `classification-manifest.json` in `tests/fixtures/minimal-suite/` with:

- `schema_version: "1.0"`
- `suite: "Fake Test Suite"`
- `completed_stages: ["pre-classify", "decompile", "review-drop", "detect-databases"]`

Place `tests/fixtures/minimal-suite/database-context.json` (from Phase 3 fixtures) in `tests/fixtures/minimal-suite/`.

Place a fresh copy of the un-enriched `FakeSuite.ctx.md` at `tests/fixtures/minimal-suite/output/context/FakeSuite.ctx.md`.

Run `ingest-schema` with profile `tests/fixtures/minimal-suite/test-profile.json` and schema enrichment path `tests/fixtures/schema-fixture/schema-enrichment.json`, following `commands/ingest-schema.md`.

### Expected Behavior

1. The command validates `database-context.json` — `schema_version: "1.0"` passes
2. The command validates `schema-enrichment.json` — `schema_version: "1.0"` passes
3. The command finds `output/context/FakeSuite.ctx.md`, reads `db_tables: [ordertable, statuslookup]` from frontmatter
4. It looks up `ordertable` in `schema-enrichment.json` and builds a columns table with 3 rows
5. It looks up `statuslookup` and builds a columns table with 2 rows plus a `**Lookup values:**` subsection with 3 rows (OP/Open, CL/Closed, HD/On Hold)
6. A `## DB Schema` section is appended to `FakeSuite.ctx.md` containing both table subsections
7. The manifest gains `"ingest-schema"` in `completed_stages`

### Pass Criteria

- `output/context/FakeSuite.ctx.md` contains a `## DB Schema` section — verifies AC2.1
- The `### ordertable` subsection contains a markdown table with columns `Column`, `Type`, `Nullable`, `Notes` and rows for `fcorderid`, `fccustid`, `fcstatus` with correct values from `schema-enrichment.json` — verifies AC2.2
- The `### statuslookup` subsection contains a `**Lookup values:**` block with a table listing `OP / Open`, `CL / Closed`, `HD / On Hold` — verifies AC2.3
- The existing file sections (`## Summary`, `## Public API`, `## SQL / DB Usage`, `## Cross-Component References`) are preserved unchanged — verifies AC2.1 (in-place enrichment, not replacement)

## Test Case B: Idempotency — second run replaces, does not duplicate

### Setup

Use the same manifest and `database-context.json` from Test Case A. The `FakeSuite.ctx.md` should already contain a `## DB Schema` section from Test Case A (do not reset it).

Note: This test case depends on Test Case A having completed successfully. If Test Case A did not produce a `## DB Schema` section, manually append one to `output/context/FakeSuite.ctx.md` before running this test case.

Run `ingest-schema` again with the same arguments.

### Expected Behavior

1. The command reads `FakeSuite.ctx.md` and finds an existing `## DB Schema` section
2. It replaces the section with a freshly generated one (same content)
3. Only one `## DB Schema` heading appears in the file after the second run

### Pass Criteria

- `output/context/FakeSuite.ctx.md` contains exactly one `## DB Schema` heading (not two) — verifies AC2.4
- The content of the `## DB Schema` section is correct (same as after the first run) — verifies AC2.4

## Test Case C: Table not in schema — informational note, .ctx.md unchanged for unknown table

### Setup

Create a fresh `.ctx.md` at `tests/fixtures/minimal-suite/output/context/FakeSuite.ctx.md` with:

- YAML frontmatter containing `db_tables: [ordertable, unknowntable]`
- `unknowntable` is a table name that does NOT exist in `schema-enrichment.json`
- Use the same body content as `tests/fixtures/minimal-suite/FakeSuite.ctx.md` but substitute `unknowntable` for `statuslookup` in the `db_tables` list

Use the same manifest and `database-context.json` from Test Case A.

Run `ingest-schema` with the same arguments.

### Expected Behavior

1. The command finds `ordertable` in `schema-enrichment.json` and enriches it normally
2. The command does not find `unknowntable` in `schema-enrichment.json`
3. An informational note is included in the summary report: e.g., `Table 'unknowntable' in FakeSuite.ctx.md not found in schema-enrichment.json — skipped`
4. The command does NOT hard stop
5. `output/context/FakeSuite.ctx.md` gets a `## DB Schema` section with only the `### ordertable` subsection (no `### unknowntable` subsection)

### Pass Criteria

- The command completes without a hard stop — verifies AC2.5 (not a fatal error)
- The summary output includes an informational note about `unknowntable` — verifies AC2.5
- `output/context/FakeSuite.ctx.md` contains `### ordertable` in the `## DB Schema` section — verifies AC2.5
- `output/context/FakeSuite.ctx.md` does NOT contain `### unknowntable` in the `## DB Schema` section — verifies AC2.5

## Test Case D: Missing database-context.json hard stop

### Setup

Create `classification-manifest.json` in `tests/fixtures/minimal-suite/` (as in Test Case A).

Do NOT place `database-context.json` in `tests/fixtures/minimal-suite/`. If it exists from a previous test case, remove it.

Run `ingest-schema` with profile `tests/fixtures/minimal-suite/test-profile.json` and schema enrichment path `tests/fixtures/schema-fixture/schema-enrichment.json`.

### Expected Behavior

1. The command looks for `database-context.json` in the profile directory and does not find it
2. It halts immediately with a hard stop error message referencing `/detect-databases`
3. No `.ctx.md` files are modified

### Pass Criteria

- The command halts before modifying any files — verifies AC2.6
- The error message contains a reference to `/detect-databases` — verifies AC2.6

## Test Case E: Missing schema-enrichment.json hard stop

### Setup

Place `tests/fixtures/minimal-suite/database-context.json` (from Phase 3 fixtures).
Create `classification-manifest.json` (as in Test Case A).

Run `ingest-schema` with profile `tests/fixtures/minimal-suite/test-profile.json` and a schema enrichment path that does NOT exist (e.g., `tests/fixtures/schema-fixture/nonexistent-schema.json`).

### Expected Behavior

1. The command loads and validates `database-context.json` successfully
2. It attempts to load `schema-enrichment.json` at the specified path and does not find it
3. It halts with a descriptive error message that includes the attempted file path
4. No `.ctx.md` files are modified

### Pass Criteria

- The command halts before modifying any files — verifies AC2.7
- The error message includes the attempted path (e.g., `tests/fixtures/schema-fixture/nonexistent-schema.json`) — verifies AC2.7

## Test Case F: Wrong schema_version in schema-enrichment.json hard stop

### Setup

Create a temporary `schema-enrichment.json` identical to `tests/fixtures/schema-fixture/schema-enrichment.json` but with `schema_version` set to `"2.0"` instead of `"1.0"`. Place it at a temporary path (e.g., `tests/fixtures/schema-fixture/schema-enrichment-v2.json`).

Place `tests/fixtures/minimal-suite/database-context.json` (from Phase 3 fixtures).
Create `classification-manifest.json` (as in Test Case A).

Run `ingest-schema` with profile `tests/fixtures/minimal-suite/test-profile.json` and schema enrichment path pointing to the `schema_version: "2.0"` file.

### Expected Behavior

1. The command loads and validates `database-context.json` successfully
2. It loads `schema-enrichment.json` and detects `schema_version: "2.0"` instead of `"1.0"`
3. It halts with a hard stop error message mentioning the version mismatch
4. No `.ctx.md` files are modified

### Pass Criteria

- The command halts before modifying any files — verifies AC2.8
- The error message mentions the version mismatch — verifies AC2.8

## Test Case G: Wrong schema_version in database-context.json hard stop

### Setup

Create a temporary `database-context.json` with `schema_version: "2.0"` (all other fields can be minimal). Place it at `tests/fixtures/minimal-suite/database-context.json`.
Create `classification-manifest.json` (as in Test Case A).

Run `ingest-schema` with profile `tests/fixtures/minimal-suite/test-profile.json` and schema enrichment path `tests/fixtures/schema-fixture/schema-enrichment.json`.

### Expected Behavior

1. The command loads `database-context.json` and detects `schema_version: "2.0"` instead of `"1.0"`
2. It halts with a hard stop error message mentioning the version mismatch
3. `schema-enrichment.json` is never loaded
4. No `.ctx.md` files are modified

### Pass Criteria

- The command halts before loading `schema-enrichment.json` or modifying any files — verifies AC2.9
- The error message mentions the version mismatch — verifies AC2.9

## Self-Verification

After completing all test cases above, evaluate each criterion and output a PASS/FAIL verdict using this exact format:

```
PASS: db-context-correction.AC2.1 — <criterion text>
FAIL: db-context-correction.AC2.3 — <criterion text> — Reason: <brief explanation>
```

Criteria to evaluate (one line each):

- db-context-correction.AC2.1: `.ctx.md` file with `db_tables` gets `## DB Schema` section appended — verified by Test Case A
- db-context-correction.AC2.2: DB Schema section contains correct column table (name, type, nullable, notes) — verified by Test Case A
- db-context-correction.AC2.3: Lookup table (`is_lookup: true`) gets Lookup Values subsection with all `lookup_values` entries — verified by Test Case A (`statuslookup`)
- db-context-correction.AC2.4: Running ingest-schema twice does not duplicate the `## DB Schema` section — second run replaces it — verified by Test Case B
- db-context-correction.AC2.5: Table in `db_tables` not found in `schema-enrichment.json` → informational note in report only; `.ctx.md` unchanged for that table — verified by Test Case C
- db-context-correction.AC2.6: Missing `database-context.json` → hard stop referencing `/detect-databases` — verified by Test Case D
- db-context-correction.AC2.7: Missing `schema-enrichment.json` → hard stop with descriptive error including attempted path — verified by Test Case E
- db-context-correction.AC2.8: `schema-enrichment.json` `schema_version` ≠ `"1.0"` → hard stop — verified by Test Case F
- db-context-correction.AC2.9: `database-context.json` `schema_version` ≠ `"1.0"` → hard stop — verified by Test Case G

After evaluating all criteria, output a summary line:

```
OVERALL: PASS (9/9)
```

or, if any criteria failed:

```
OVERALL: FAIL (N/9 passed) — failing criteria: <comma-separated list of criterion IDs>
```
