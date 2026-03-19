# process-drop

Orchestrator command that runs the full pipeline in the correct order. Calls each sub-command in sequence to automate assembly drop processing from raw binaries to a complete reference index with context files.

## Required inputs

- `<profile>`: path to suite profile JSON
- `<source-folder>`: path to the folder containing the dropped .NET assemblies

## Purpose

Single-command entry point that automates the full pipeline from a raw assembly drop to a complete reference index with context files. Useful for first-time setup and for reprocessing drops.

## Flags

- `--regenerate-indexes`: run `/generate-indexes` after detect-databases (optional)
- `--skip-prune`: skip the prune stage (optional, useful for debugging)
- `--dry-run`: print what would be run without executing anything (optional)

## Pipeline stages (in order)

1. `/pre-classify <profile> <source-folder>` — classify assemblies, resolve unknowns, write manifest
2. `/decompile <profile>` — decompile suite assemblies
3. `/review-drop <profile>` — classify relevance, write index tables
4. `/generate-context <profile>` — generate .ctx.md files
5. `/prune <profile>` — delete non-reference files (skipped if `--skip-prune`)
6. `/detect-databases <profile>` — aggregate db table list
7. `/generate-indexes <profile>` (optional, run only if `--regenerate-indexes` flag provided) — rebuild index from scratch

Note: the `ingest-schema` command is NOT part of this pipeline (it requires external input and is run separately).

## Steps

### 1. Check for existing manifest (resume mode)

If `classification-manifest.json` already exists in the directory containing the profile file, load it and check `completed_stages`.

For each stage already in `completed_stages`, record it as "will skip". Print a resume message:

```
Resuming from existing manifest. Skipping: <comma-separated list of stages already completed>
```

If this is a fresh run (no existing manifest), print:

```
Starting process-drop from scratch.
```

### 2. Determine the pipeline

Build the ordered list of stages to execute:

1. pre-classify (skip if already in completed_stages)
2. decompile (skip if already in completed_stages)
3. review-drop (skip if already in completed_stages)
4. generate-context (skip if already in completed_stages)
5. prune (skip if already in completed_stages, UNLESS `--skip-prune` flag is set; if flag is set, always skip this stage)
6. detect-databases (skip if already in completed_stages)
7. generate-indexes (include ONLY if `--regenerate-indexes` flag is set; skip if already in completed_stages)

### 3. Handle --dry-run flag

If `--dry-run` is set:

Print the stages that would be executed, in order. Example:

```
--dry-run mode. Would execute:
  1. pre-classify
  2. decompile
  3. review-drop
  4. generate-context
  5. prune
  6. detect-databases
```

Then exit without running anything and without modifying the manifest.

### 4. Run each stage

For each stage in the pipeline:

1. Print a start message: `Running <stage>...`
2. Invoke that stage's command with the profile (and source-folder for pre-classify)
3. If the stage succeeds, mark it as completed and continue to the next
4. If the stage hard-stops (returns a failure), print the error and stop the pipeline

After each successful stage, update the manifest: add that stage name to `completed_stages` if not already present. Write the updated manifest to disk.

### 5. Report overall summary

After all stages complete (or if a stage fails), output:

```
process-drop complete.
  Stages completed: <comma-separated list of completed stages>
  Stages skipped: <comma-separated list of skipped stages, if any>
  Output:
    Index: <index_output_path from profile>
    Context: <context_output_path from profile>
    DB detection: <path to db-detection.json in profile directory>
```

If a stage failed, instead output:

```
process-drop failed at stage: <stage name>
  Error: <error message from the failed stage>
  Resume: Fix the issue and run process-drop again with the same profile — it will resume from where it left off.
```

## Error handling

- Any stage hard stop → print the error, stop pipeline, advise user to fix and resume
- Partial progress is preserved in the manifest — user can re-run process-drop and it will resume from where it left off
- If the manifest file becomes corrupted or unreadable: hard stop with "Failed to load classification-manifest.json. Check file integrity and retry."

## Notes

- The `--skip-prune` flag is useful for debugging; it keeps binaries on disk after indexing is complete
- The `--regenerate-indexes` flag is useful if the index format changes or needs to be rebuilt without re-running the classifier
- Resume behavior allows the user to fix errors mid-pipeline and continue without reprocessing completed stages
- This command does not validate that input assemblies are present; that check happens in `/pre-classify`
