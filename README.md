# dotnet-source-reference

A Claude Code plugin that decompiles .NET assemblies and generates LLM-optimized reference documentation. Point it at a folder of DLLs, run one command, and get a set of `.ctx.md` files and index tables ready for use as context in other Claude sessions.

## Prerequisites

- Claude Code
- [.NET SDK](https://dot.net) on your PATH
- `ilspycmd` decompiler (the plugin can install this for you — see below)

## Installation

```bash
/plugin install file:///absolute/path/to/dotnet-source-reference
```

Or install from a marketplace if your team has one configured.

## Quick start

**Step 1 — Check the decompiler is installed:**

```
/bootstrap-ilspy
```

This checks for `ilspycmd` and installs it via `dotnet tool install -g` if it's missing.

**Step 2 — Create a suite profile** (see [Suite profile format](#suite-profile-format) below).

**Step 3 — Run the full pipeline:**

```
/process-drop path/to/my-profile.json
```

That's it. The pipeline runs all stages in order and writes the output to the paths defined in your profile.

## What gets produced

- **`classification-manifest.json`** — pipeline state file; tracks which stages have completed and stores classifier results
- **`index-<component>.md`** files — human-readable tables listing every assembly, its relevance, key types, and DB tables
- **`.ctx.md` files** — one per assembly, containing a YAML front-matter block, public API summary, SQL usage, and cross-component references
- **`database-context.json`** — detected DB tables, access patterns, probable lookup flags, and dynamic SQL warnings

## Suite profile format

The profile is a JSON file that tells the pipeline where your assemblies live and where to write output.

```json
{
  "suite_name": "MyApp",
  "components": [
    {
      "name": "main",
      "path": "relative/path/to/binaries"
    }
  ],
  "known_suite_patterns": ["MyApp.*.dll", "MyApp.exe"],
  "known_third_party_patterns": ["Newtonsoft.*.dll", "log4net.dll"],
  "unknown_default": "skip",
  "decompile_parallel_threshold": 10,
  "index_output_path": "docs/indexes",
  "context_output_path": "docs/context"
}
```

All `path` values in `components` are resolved relative to the profile file's directory. `index_output_path` and `context_output_path` work the same way.

`unknown_default` controls what happens when an assembly matches neither suite nor third-party patterns — `"skip"` ignores it, `"decompile"` treats it as suite code. The pipeline will still prompt you interactively for unknowns; this is the fallback if you don't respond.

## Pipeline stages

The stages run in this order when you use `/process-drop`. You can also run them individually.

| Command | What it does |
|---------|-------------|
| `/pre-classify <profile>` | Scans component folders, classifies assemblies, deletes third-party binaries, writes `classification-manifest.json` |
| `/decompile <profile>` | Runs `ilspycmd` on suite assemblies to produce `.decompiled.cs` files |
| `/review-drop <profile>` | Runs the `assembly-classifier` agent on each assembly, writes index tables |
| `/generate-context <profile>` | Runs the `context-distiller` agent on each assembly, writes `.ctx.md` files |
| `/prune <profile>` | Deletes binaries and PDB files; removes `.decompiled.cs` files not marked for retention |
| `/detect-databases <profile>` | Scans decompiled source for SQL patterns, writes `database-context.json` |
| `/generate-indexes <profile>` | Rebuilds index tables from existing classifier results (optional; useful after format changes) |

### Resume behavior

If a run fails mid-pipeline, the manifest tracks which stages completed. Re-running `/process-drop` with the same profile picks up where it left off — it skips completed stages automatically.

## Schema enrichment (separate step)

`/ingest-schema` enriches `.ctx.md` files with column definitions and lookup values. It runs separately because it requires external input — a `schema-enrichment.json` file produced by a companion schema extraction tool.

```
/ingest-schema path/to/profile.json path/to/schema-enrichment.json
```

This command requires `/detect-databases` to have run first (it checks for `database-context.json` as a prerequisite guard).

## Running individual commands

Every command accepts a profile path and is idempotent via `completed_stages` tracking. You can re-run any stage if you need to refresh its output — just remove its entry from `completed_stages` in the manifest first, or delete the manifest to start fresh.

```
/review-drop path/to/profile.json
/detect-databases path/to/profile.json
```

## process-drop flags

`/process-drop` supports three optional flags:

- `--skip-prune` — skips the prune stage, useful for debugging (keeps binaries on disk)
- `--regenerate-indexes` — runs `/generate-indexes` after `/detect-databases`
- `--dry-run` — prints the stages that would run without executing anything

Example:

```
/process-drop path/to/profile.json --skip-prune --dry-run
```

## Large drops

When the number of assemblies to decompile exceeds `decompile_parallel_threshold` (default: 10), the `decompile` command automatically batches them and dispatches parallel `decompile-batch` agents. You don't need to configure this — it happens transparently.

## Troubleshooting

**`dotnet` not found:** Install the [.NET SDK](https://dot.net), ensure it's on your PATH, then re-run `/bootstrap-ilspy`.

**`ilspycmd` decompile failure:** Check the `decompile_errors` field in `classification-manifest.json` for the affected assembly. Common causes are native DLLs (not .NET assemblies) and corrupted binaries.

**Picked up wrong assemblies:** Tighten your `known_suite_patterns` or `known_third_party_patterns` in the profile. Pattern matching is glob-style and case-insensitive.

**`.ctx.md` files missing after a partial run:** If `/prune` ran before `/generate-context` completed, `.decompiled.cs` files may have been deleted. Remove `"generate-context"` from `completed_stages` in the manifest and re-run — the command logs each missing file and skips it. For full coverage, start fresh.
