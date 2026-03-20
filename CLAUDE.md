# dotnet-source-reference

Last verified: 2026-03-20

## What This Is

Claude plugin that decompiles .NET assemblies and generates LLM-optimized reference documentation. Skills, agents, and shared logic are markdown specs executed by Claude -- there is no traditional source code.

## Structure

- `skills/` - Pipeline skill specs (markdown). Each subdirectory contains a `SKILL.md` defining inputs, steps, outputs, and error handling for one pipeline stage.
- `agents/` - Agent specs dispatched by skills (assembly-classifier, context-distiller, decompile-batch)
- `config/` - Suite profile schema and example profiles
- `tests/scenarios/` - Self-verifying test scenarios (markdown). Each `test-<skill>.md` tests one skill.
- `tests/fixtures/` - Test fixture data (JSON, .cs, .ctx.md files)
- `docs/` - Design plans and implementation plans

## Pipeline Order

Commands run in this sequence (managed by `process-drop`):

1. `pre-classify` - Classify assemblies, prune third-party
2. `decompile` - Decompile to C# source
3. `review-drop` - Assess relevance, write indexes
4. `generate-context` - Generate .ctx.md files per assembly
5. `prune` - Delete non-retained files
6. `detect-databases` - Scan source for DB usage, write `database-context.json`
7. `generate-indexes` - (optional) Rebuild index tables
8. `ingest-schema` - (separate, not in process-drop) Enrich .ctx.md with schema data

## Key Artifacts

- `classification-manifest.json` - Pipeline state; tracks `completed_stages` and `classifier_results`
- `database-context.json` - DB detection output: tables, access patterns, database groupings
- `.ctx.md` files - LLM-optimized context docs per assembly (enriched by ingest-schema)

## Conventions

- Skill specs are self-contained: each includes inputs, steps, output schema, and error handling
- Test scenarios are self-verifying: they include fixture data, expected outputs, and verification criteria
- Pipeline ordering is enforced via `completed_stages` in the manifest
- Table names are normalised: lowercase, no schema prefix, no brackets
