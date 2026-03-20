# Commands

Last verified: 2026-03-19

## Purpose

Each markdown file is a complete command specification that Claude executes directly. Commands form a pipeline for processing .NET assembly drops into LLM-optimized documentation.

## Contracts

- **Exposes**: Ten commands invokable as `/command-name <profile>`
- **Guarantees**: Each command documents its prerequisites, outputs, and error handling. Commands are idempotent via `completed_stages` tracking in `classification-manifest.json`.
- **Expects**: A valid suite profile JSON path. Prerequisites met (earlier pipeline stages completed).

## Dependencies

- **Uses**: `classification-manifest.json` (pipeline state), suite profile JSON, assembly binaries
- **Used by**: End users via CLI, `process-drop` orchestrator
- **Boundary**: Commands should not reference each other's internals; they communicate through the manifest and output files

## Key Decisions

- `detect-databases` scans decompiled source (not just classifier metadata): Enables access pattern detection, dynamic SQL warnings, and connection string inference that pure metadata aggregation cannot provide
- `ingest-schema` enriches `.ctx.md` in-place (no separate schema/ directory): Keeps all assembly context in one file, reducing token cost when Claude reads context
- `database-context.json` is the current output format: Richer schema with `access_patterns`, `probable_lookup`, `unresolved_references`, and `gaps`
- `ingest-schema` guards on `database-context.json` existence (not content): Enforces pipeline ordering without coupling to detection output format

## Invariants

- `detect-databases` requires `"review-drop"` in `completed_stages`
- `ingest-schema` requires `database-context.json` to exist with `schema_version: "1.0"`
- `process-drop` does NOT include `ingest-schema` (it requires external schema input)
- Output file is always `database-context.json`

## Key Files

- `detect-databases.md` - DB usage scanning and `database-context.json` generation
- `ingest-schema.md` - `.ctx.md` enrichment with schema column/lookup data
- `process-drop.md` - Pipeline orchestrator (runs stages 1-7 in order)
