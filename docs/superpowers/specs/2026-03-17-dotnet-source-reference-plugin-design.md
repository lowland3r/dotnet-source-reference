# dotnet-source-reference Plugin — Design Spec

**Date:** 2026-03-17
**Status:** Approved
**Author:** Jake Wimmer + Claude

---

## Definition of Done

A publishable ed3d-style Claude Code plugin (`dotnet-source-reference`) that, given a folder of .NET assemblies and a suite config profile, produces:

- LLM-optimized context files per relevant assembly (primary output, always generated)
- A database context handoff file for the companion schema extraction plugin
- A CHANGELOG tracking processed versions over time
- Enriched human-readable index tables per component (optional, non-default)

The plugin ships with an Aptean Made2Manage (M2M) config profile as the bundled reference implementation.

---

## Context

This plugin automates and formalizes the existing manual workflow in the `m2m-source-reference` repository, where M2M ERP binary drops are reviewed, indexed, pruned, and documented for use by AI agents developing customizations and integrations.

The existing repo already contains:
- Three source component folders (`source-m2merpserver`, `source-m2midserver`, `source-m2mwebapi`)
- Flat index tables per component (`.github/agents/reference/index-<component>.md`)
- Manual job instruction files (`.github/agents/jobs/review_source_material.md`, `prune_source_material.md`)
- A manually-produced `sql-cross-reference.md` (the precursor to automated schema context)
- A broken GitHub Actions workflow referencing a missing `contextualize.md`

This plugin replaces the manual job files and broken workflow with structured, independently re-runnable slash commands.

---

## Architecture

The plugin is named `dotnet-source-reference` and follows the ed3d plugin structure.

**Generality constraint:** All commands, agents, and skills are written using generic .NET terminology. No file in `commands/`, `agents/`, or `skills/` references any specific software suite, assembly name, or vendor. Suite-specific details exist only in config profiles. The M2M profile (`config/profiles/m2m.json`) is the sole artifact in this plugin that references Aptean Made2Manage by name or assembly pattern — all other generated artifacts are reusable for any .NET software suite.

```
dotnet-source-reference/
│
├── plugin.json
│
├── commands/
│   ├── bootstrap-ilspy.md
│   ├── pre-classify.md
│   ├── decompile.md
│   ├── review-drop.md
│   ├── prune.md
│   ├── generate-indexes.md
│   ├── generate-context.md
│   ├── detect-databases.md
│   ├── ingest-schema.md
│   └── process-drop.md
│
├── agents/
│   ├── assembly-classifier.md
│   ├── context-distiller.md
│   └── decompile-batch.md
│
├── skills/
│   └── ilspy-runner.md
│
├── config/
│   ├── schema.json
│   └── profiles/
│       └── m2m.json
│
└── tests/
    ├── fixtures/
    │   ├── minimal-suite/
    │   ├── m2m-sample/
    │   └── schema-fixture/
    └── scenarios/
```

---

## Pipeline Stages

`process-drop` orchestrates all stages in sequence against all components defined in the profile simultaneously. Each stage can also be invoked independently via its slash command with an optional `--component` flag.

```
[DLL folders across all components]
         │
         ▼
 bootstrap-ilspy      check/install ilspycmd via dotnet tool install -g ilspycmd
         │
         ▼
 pre-classify         fast filename pass → prune third-party immediately
         │            produces classification-manifest.json
         ▼
 decompile            ilspycmd on passing assemblies → .decompiled.cs per assembly
         │
         ▼
 review-drop          assembly-classifier agent (all components in scope)
         │            updates index tables
         ▼
 prune                delete binaries, PDBs, irrelevant .decompiled.cs
         │
         ▼
 generate-context     context-distiller agent (multi-component scope)     [DEFAULT]
         │            writes .ctx.md per relevant assembly
         ▼
[optional: --with-indexes]
         │
         ▼
 generate-indexes     enrich index tables; append cross-component relationships
         ▼
[optional enrichment]
         │
         ▼
 detect-databases     analyze decompiled source → database-context.json
         │
         ▼
 ingest-schema        consume schema-enrichment.json from schema plugin
                      → enriched indexes + .ctx.md files
```

`process-drop` prompts for a version/release label before running and appends an entry to `CHANGELOG.md`. It runs `generate-context` by default. `generate-indexes` is skipped unless `--with-indexes` is passed. It also prompts at the end of the main pipeline whether to run the optional database enrichment stages.

---

## Components

### Commands

| Command | Inputs | Purpose |
|---|---|---|
| `bootstrap-ilspy` | — | Checks PATH for ilspycmd; installs via `dotnet tool install -g ilspycmd` if missing |
| `pre-classify` | `<source-folder>`, `<profile>` | Scans DLL filenames against profile patterns; physically deletes third-party binaries (.dll, .pdb, .xml, .exe) immediately; marks them in manifest; writes `classification-manifest.json` |
| `decompile` | `<source-folder>`, `<profile>`, `classification-manifest.json` | Reads manifest for passing assemblies. If assembly count exceeds `decompile_parallel_threshold` (profile setting, default 10), partitions assemblies into batches and dispatches one `decompile-batch` subagent per batch in parallel; otherwise runs sequentially. Writes `.decompiled.cs` alongside each DLL; skips already-decompiled assemblies when resuming. Merges all batch results back into the manifest. |
| `review-drop` | `<source-folder>`, `<profile>` | Dispatches assembly-classifier agent; updates index table rows |
| `prune` | `<source-folder>`, `<profile>` | Deletes binaries, PDBs, and files not marked `Stored in repo: Yes` in the index |
| `generate-indexes` | `<source-folder>`, `<profile>` | Enriches index tables with key types, method summaries, namespace descriptions; appends cross-component section |
| `generate-context` | `<source-folder>`, `<profile>` | Dispatches context-distiller agent per assembly; writes `.ctx.md` files |
| `detect-databases` | `<source-folder>`, `<profile>` | Analyzes decompiled source for DB usage patterns; writes `database-context.json` |
| `ingest-schema` | `schema-enrichment.json` | Reads schema plugin output; enriches index tables and `.ctx.md` files with schema details |
| `process-drop` | `<profile>` | Orchestrates all stages; prompts for version label; writes CHANGELOG entry. Component paths are resolved relative to the directory containing the profile file. Performs a pre-flight check that all component paths exist before starting any stage. |

### Agents

**`assembly-classifier`**
Given a `.decompiled.cs` file and the suite profile (with all components in scope), produces a structured relevance assessment: relevance score, primary purpose, key public types, API/business logic areas touched, and cross-component relationships observed.

**`context-distiller`**
Given a `.decompiled.cs` file, its index entry, and the full multi-component decompiled source for cross-referencing, produces a `.ctx.md` file optimized for LLM context injection. Strips implementation noise; retains public API surface, integration points, key patterns, and DB table relationships.

**`decompile-batch`**
Receives a list of assembly paths, the profile, and a batch identifier. Runs ilspycmd on each assembly in sequence; writes `.decompiled.cs` output; returns a batch result record (assembly name → decompile status, output path, errors) for the parent `decompile` command to merge into the manifest. Used only when assembly count exceeds `decompile_parallel_threshold`.

### Skills

**`ilspy-runner`**
Encapsulates the ilspycmd invocation pattern: flags, output path conventions, and error capture. Shared by `bootstrap-ilspy` and `decompile` to avoid duplication.

---

## Config Profile

Profiles are JSON files defining a software suite's structure and classification rules.

### Schema (`config/schema.json`)

```json
{
  "suite_name": "string",
  "components": [
    { "name": "string", "path": "string" }
  ],
  "known_suite_patterns": ["string"],
  "known_third_party_patterns": ["string"],
  "unknown_default": "decompile | skip",
  "decompile_parallel_threshold": 10,
  "index_output_path": "string",
  "context_output_path": "string",
  "version_prompt": "string"
}
```

### M2M Profile (`config/profiles/m2m.json`)

```json
{
  "suite_name": "Aptean Made2Manage",
  "components": [
    { "name": "erpserver", "path": "source-m2merpserver" },
    { "name": "idserver",  "path": "source-m2midserver"  },
    { "name": "webapi",    "path": "source-m2mwebapi"    }
  ],
  "known_suite_patterns": [
    "M2M*", "Aptean.*", "Consona.*", "BDL*",
    "PSDomain.*", "Notification*", "Reporting.*",
    "SMSIntegration.*", "ErrorAndValidation*", "CloudPrinter.*"
  ],
  "known_third_party_patterns": [
    "Microsoft.*", "System.*", "Newtonsoft.*", "DevExpress.*",
    "CefSharp.*", "libcef.*", "chrome_*", "libGLES*", "libEGL*",
    "Google.*", "Owin.*", "Serilog.*", "RestSharp.*",
    "PayPal.*", "Stripe.*", "Twilio.*", "AuthorizeNet.*",
    "BouncyCastle.*", "DocumentFormat.*", "WindowsBase.*",
    "IdentityServer3.*", "IdentityModel.*"
  ],
  "unknown_default": "decompile",
  "index_output_path": ".github/agents/reference",
  "context_output_path": ".github/agents/context",
  "version_prompt": "What M2M version/service pack is this binary drop?"
}
```

---

## Output Formats

### Human-Readable Index Tables (`index-<component>.md`)

Extends the existing repo format. Existing columns are preserved exactly; two new columns (`Key Public Types`, `DB Tables`) are appended before `First Indexed`:

| File / Folder | Description | API/Business Logic Relevant | Primary Language | Key Public Types | DB Tables | First Indexed | First Indexed Commit | Stored in repo |
|---|---|---|---|---|---|---|---|---|
| `Aptean.M2M.WebApi.Repository.decompiled.cs` | Data access layer; ADO.NET queries mapped to business object DTOs | true | C# | `RepositoryBase`, `SalesOrderRepository` | somtable, qtitem | 2026-03-11 | aad1bc9 | Yes |
| `Newtonsoft.Json.dll` | JSON serialization (third-party) | false | C# | — | — | 2026-03-17 | — | No (third-party) |

A **Cross-Component Relationships** section is appended at the bottom of each index, documenting shared assemblies and cross-boundary call patterns observed across all components.

**`First Indexed Commit` population:** `review-drop` writes this column by running `git rev-parse HEAD` at indexing time. If the working directory is not a git repository, the value is left blank. This means the column reflects the commit at which the assembly was *first observed*, not the commit that included the decompiled source file (which may not yet exist at index time).

**Suite-owned `.exe` files:** Host process executables (e.g., `M2MNetServicesHost.exe`) are classified as `suite` by `pre-classify` and passed to `decompile`. `review-drop` assesses their relevance. `prune` retains them if marked `Stored in repo: Yes`, deletes them otherwise — the same rule as `.dll` files.

### LLM-Optimized Context Files (`<assembly>.ctx.md`)

Structured for injection into an LLM context window. Frontmatter provides machine-readable metadata; body provides concise, high signal-to-noise content.

```markdown
---
assembly: Aptean.M2M.WebApi.Repository
component: webapi
suite: Aptean Made2Manage
indexed: 2026-03-17
relevant: true
db_tables: [somtable, qtitem, systat]
---

## Purpose
[One paragraph: what this assembly does and why it exists]

## Public API Surface
[Key classes and methods with brief descriptions]

## Integration Points
[What calls this, what this calls, which DB tables, cross-component relationships]

## Key Patterns
[Notable implementation patterns relevant to customization/integration developers]

## Notes
[Edge cases, known gaps, caveats]

## DB Schema  ← added by ingest-schema if available
[Table definitions for referenced tables]
```

### Database Context Handoff (`database-context.json`)

Produced by `detect-databases`; consumed by the companion schema extraction plugin.

```json
{
  "schema_version": "1.0",
  "generated_at": "<ISO8601>",
  "suite": "Aptean Made2Manage",
  "components_analyzed": ["erpserver", "idserver", "webapi"],
  "databases": [
    {
      "name": "M2MDATA05",
      "detected_from": ["connection strings in .config files", "SQL literals in source"],
      "tables": [
        {
          "name": "somtable",
          "referenced_in": ["M2MBusinessService", "Consona.Data"],
          "access_patterns": ["SELECT", "INSERT", "UPDATE"],
          "probable_lookup": false
        },
        {
          "name": "systat",
          "referenced_in": ["M2MCommon"],
          "access_patterns": ["SELECT"],
          "probable_lookup": true,
          "lookup_signals": ["read-only access pattern", "frequently joined", "no INSERT/UPDATE observed"]
        }
      ],
      "unresolved_references": [
        {
          "assembly": "M2MServerBase",
          "reason": "dynamic SQL — table name constructed at runtime"
        }
      ]
    }
  ],
  "gaps": [
    "Dynamic SQL in M2MServerBase — table names could not be resolved statically",
    "Stored procedure calls in BDLBase — internals not visible from source alone"
  ]
}
```

### Classification Manifest (`classification-manifest.json`)

Ephemeral checkpoint file written by `pre-classify` and updated by `decompile`. Acts as a shared contract between pipeline stages and enables resume behavior. Should be gitignored.

```json
{
  "schema_version": "1.0",
  "generated_at": "<ISO8601>",
  "suite": "Aptean Made2Manage",
  "components_analyzed": ["erpserver", "idserver", "webapi"],
  "completed_stages": ["pre-classify"],
  "assemblies": [
    {
      "name": "Aptean.M2M.WebApi.dll",
      "component": "webapi",
      "path": "source-m2mwebapi/Aptean.M2M.WebApi.dll",
      "classification": "suite",
      "decompile_status": "success",
      "decompile_output": "source-m2mwebapi/Aptean.M2M.WebApi.decompiled.cs",
      "decompile_errors": []
    },
    {
      "name": "Newtonsoft.Json.dll",
      "component": "webapi",
      "path": "source-m2mwebapi/Newtonsoft.Json.dll",
      "classification": "third_party",
      "decompile_status": "skipped",
      "decompile_output": null,
      "decompile_errors": []
    }
  ],
  "unknowns": [
    {
      "name": "SomeUnknown.dll",
      "component": "erpserver",
      "path": "source-m2merpserver/SomeUnknown.dll",
      "awaiting_user_decision": true,
      "user_classification": null
    }
  ]
}
```

`completed_stages` is updated by each stage on success. Stages check this array to skip already-completed work when resuming a partial run.

**Unknown assembly resolution:** After the user makes a decision, `pre-classify` updates the `unknowns` entry: `awaiting_user_decision` is set to `false` and `user_classification` is set to either `"suite"` or `"skip"`. `decompile` reads both `assemblies[]` (for `classification: suite` entries) and `unknowns[]` (for `user_classification: suite` entries). Entries with `user_classification: skip` are treated identically to `third_party` — they are pruned and noted in the index.

### Schema Enrichment Input (`schema-enrichment.json`)

Produced by the companion schema extraction plugin; consumed by `ingest-schema`. The schema extraction plugin defines the authoritative format — the fields below are the minimum `ingest-schema` requires to function:

```json
{
  "schema_version": "1.0",
  "generated_at": "<ISO8601>",
  "suite": "Aptean Made2Manage",
  "databases": [
    {
      "name": "M2MDATA05",
      "tables": [
        {
          "name": "somtable",
          "columns": [
            { "name": "fcsono", "type": "varchar(10)", "nullable": false, "notes": "PK — sales order number" }
          ],
          "foreign_keys": [
            { "column": "fcstatus", "references_table": "systat", "references_column": "fcstatus" }
          ],
          "is_lookup": false
        },
        {
          "name": "systat",
          "columns": [
            { "name": "fcstatus", "type": "char(2)", "nullable": false, "notes": "Status code" },
            { "name": "fcdescription", "type": "varchar(50)", "nullable": true, "notes": "Display label" }
          ],
          "foreign_keys": [],
          "is_lookup": true,
          "lookup_values": [
            { "fcstatus": "OP", "fcdescription": "Open" },
            { "fcstatus": "CL", "fcdescription": "Closed" }
          ]
        }
      ]
    }
  ]
}
```

`ingest-schema` hard-stops if `schema_version` is absent or does not match a supported version.

### Output Paths

```
.github/agents/reference/
  index-erpserver.md
  index-idserver.md
  index-webapi.md

.github/agents/context/
  erpserver/
    M2MBusinessServer.ctx.md
    M2MDomain.ctx.md
    ...
  idserver/
    Aptean.M2M.WebApi.IDServer.ctx.md
    ...
  webapi/
    Aptean.M2M.WebApi.ctx.md
    Aptean.M2M.WebApi.Repository.ctx.md
    ...

database-context.json
CHANGELOG.md
generation-errors.md           (appended by generate-indexes/generate-context on failure)
classification-manifest.json   (ephemeral — gitignored)
```

---

## Error Handling

**Guiding principle: fail loudly at stage boundaries; never silently skip.**

| Stage | Failure | Behavior |
|---|---|---|
| `bootstrap-ilspy` | `dotnet` not on PATH | Hard stop with install instructions |
| `bootstrap-ilspy` | Install fails | Surface dotnet error verbatim; do not proceed |
| `pre-classify` | No assemblies found | Hard stop — likely wrong folder |
| `pre-classify` | Unknown assembly | Placed in `unknowns` list; user prompted to confirm before decompile proceeds |
| `decompile` | ilspycmd fails on an assembly | Log to manifest `decompile_errors`; skip assembly; surface summary at end |
| `decompile` | Truncated output | Treated as decompile failure; logged |
| `review-drop` | Low-confidence relevance | Row added with `Review needed` flag; human can override |
| `prune` | Would delete a `Stored in repo: Yes` file | Hard stop; surface conflict |
| `prune` | Git working tree dirty | Warn user; do not block |
| `generate-indexes` / `generate-context` | Agent fails or times out | Skip assembly; log to `generation-errors.md`; continue |
| `detect-databases` | No SQL patterns found | Write empty `databases: []` with note; not an error |
| `ingest-schema` | `schema-enrichment.json` missing or wrong `schema_version` | Hard stop with clear message: "Expected schema-enrichment.json from schema extraction plugin at schema_version 1.0" |
| `ingest-schema` | `database-context.json` missing or incompatible `schema_version` | Hard stop: "Run detect-databases before ingest-schema, or re-run if database-context.json schema_version has changed" |
| `ingest-schema` | Table in enrichment not in `database-context.json` | Log as informational note; continue |
| `process-drop` | Any hard-stop stage fails | Abort remaining stages; display which stage failed and resume instructions |

`classification-manifest.json` acts as a checkpoint — stages check it to skip already-completed work when resuming a partially-completed run.

---

## Testing

### Fixtures

```
tests/fixtures/
  minimal-suite/        3 DLLs: one suite, one third-party, one unknown
                        pre-decompiled .cs files; test profile
  m2m-sample/           Small real M2M assembly subset for M2M profile smoke test
  schema-fixture/       Minimal schema-enrichment.json for ingest-schema testing
```

### Stage Verification Matrix

| Stage | Verified by test |
|---|---|
| `pre-classify` | Third-party DLL pruned; suite DLL passed through; unknown flagged in manifest |
| `decompile` | `.decompiled.cs` created; decompile errors logged in manifest |
| `review-drop` | Index row created with correct fields; relevance flag set |
| `prune` | Only `Stored in repo: Yes` files remain; prune-vs-index conflict triggers hard stop |
| `generate-indexes` | Cross-component section present; `Key Public Types` populated |
| `generate-context` | `.ctx.md` frontmatter contains all required keys (`assembly`, `component`, `suite`, `indexed`, `relevant`, `db_tables`); body contains all required sections: `Purpose`, `Public API Surface`, `Integration Points`, `Key Patterns`, `Notes` |
| `detect-databases` | `database-context.json` valid against schema; `probable_lookup` flags correct |
| `ingest-schema` | `.ctx.md` files updated with schema section; missing file triggers hard stop |

### Integration Test

Run `process-drop` against `minimal-suite/` fixture end-to-end. Verify:
1. `CHANGELOG.md` entry written
2. `classification-manifest.json` produced
3. Only suite assembly has `.decompiled.cs`
4. Index has all three assemblies (two marked `Stored in repo: No`)
5. One `.ctx.md` produced for suite assembly
6. Hard stop triggered when prune conflict injected

### M2M Profile Smoke Test

Run `/pre-classify` only against the real `source-m2merpserver/` folder using `m2m.json`. Verify known third-party DLLs (CefSharp, Google, Newtonsoft) are correctly bucketed. Fast, safe, no file modifications.

---

## Resolved Trade-offs

| Trade-off | Decision |
|---|---|
| Automate ILSpy vs. manual prerequisite | Both — automate by default (`dotnet tool install -g ilspycmd`), accept pre-decompiled input |
| Primary output format | LLM context files (`.ctx.md`) are always generated; human-readable index tables are optional (`--with-indexes`) |
| Generic pipeline vs. M2M-specific | Generic core with JSON config profile; M2M ships as bundled reference profile |
| Process components independently vs. together | Always process all components together for cross-component relational context |
| Cross-reference as separate stage vs. built-in | Built into `generate-indexes` and `generate-context` (full-suite scope eliminates need for separate stage) |
| Raw schema ingestion vs. plugin handoff only | `ingest-schema` accepts only structured output from the schema extraction plugin |

## Scope

**In scope:**
- ed3d plugin structure with all commands, agents, skills (all generic — no suite-specific references)
- ILSpy bootstrapping and decompilation automation (with parallel subagent support for large drops)
- Pre-classification with immediate pruning of third-party assemblies
- LLM-optimized context file generation (primary, always-on output)
- Human-readable index table generation (optional, `--with-indexes` flag)
- M2M config profile (the only suite-specific artifact in the plugin)
- Database context handoff file (`database-context.json`)
- Schema enrichment ingestion (`ingest-schema`)
- CHANGELOG maintenance
- Test fixtures and scenario files

**Out of scope:**
- MCP server exposure
- Scheduled GitHub Actions automation
- GUI tooling
- Raw SQL/CSV schema ingestion (schema plugin handles this)
- The companion schema extraction plugin itself
