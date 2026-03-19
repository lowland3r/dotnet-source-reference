# decompile

Decompile all suite-classified assemblies using ilspycmd. Reads classification-manifest.json to determine which assemblies to process. Supports parallel batch execution for large drops.

## Required inputs
- `<profile>`: path to suite profile JSON file
- `classification-manifest.json` must exist in the same directory as the profile (written by pre-classify)

## Steps

### 1. Read inputs

Load the profile and `classification-manifest.json` from the profile directory.

If `classification-manifest.json` does not exist: hard stop with "Run /pre-classify before /decompile."

### 2. Collect assemblies to decompile

From `assemblies[]`: collect entries where `classification: "suite"` and `decompile_status: "pending"`.
From `unknowns[]`: collect entries where `user_classification: "suite"` or `user_classification: "decompile"`.

If this list is empty: output "Nothing to decompile — all suite assemblies already processed." and exit successfully.

### 3. Check ilspycmd

Run `dotnet tool list -g`. If `ilspycmd` is not present: hard stop with "ilspycmd not installed. Run /bootstrap-ilspy first."

### 4. Determine execution mode

Count the assemblies to decompile. Compare against `decompile_parallel_threshold` from the profile (default: 10).

- **Sequential (count ≤ threshold):** Use the `ilspy-runner` skill to decompile each assembly directly.
- **Parallel (count > threshold):** Compute batch count N = `ceil(count / threshold)`. Partition assemblies into N batches of `ceil(count / N)` assemblies each. Dispatch one `decompile-batch` subagent per batch in parallel. Wait for all batches to complete and collect their JSON result arrays.

### 5. Update manifest

For each result returned (sequential or from batches):
- Update the matching entry in `assemblies[]` (or `unknowns[]`) with `decompile_status`, `decompile_output`, and `decompile_errors`.
  - On success: `decompile_output` = path to `.decompiled.cs`; `decompile_errors` = `[]`
  - On failure: `decompile_output` = `null`; `decompile_errors` = array of error strings

After all results are processed, add `"decompile"` to `completed_stages` (once, not per assembly).

Write the updated manifest back to disk.

### 6. Report summary

```
Decompilation complete.
  Total attempted: N
  Succeeded: N
  Failed: N  (see classification-manifest.json for details)
  Skipped (already done): N
```

If any assemblies failed, list their names and the first error line.

## Error handling

- ilspycmd not on PATH → hard stop (see step 3)
- Individual assembly decompile failure → log to manifest, continue (never abort the whole run)
- Truncated output (file exists but fails validation) → treat as failure
