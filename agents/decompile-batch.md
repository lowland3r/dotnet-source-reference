# decompile-batch

Decompile a batch of .NET assemblies using ilspycmd. Called by the decompile command when parallel processing is needed.

## Inputs (provided by the calling command)
- `batch_id`: identifier for this batch (e.g., "batch-1")
- `assemblies`: list of objects, each with `name`, `path`, `component`
- `profile_dir`: path to the directory containing the suite profile (used to resolve relative paths)

## Steps

Use the `ilspy-runner` skill for all ilspycmd invocations.

For each assembly in the batch:

1. Resolve the full path: `<profile_dir>/<assembly.path>`
2. Check if `<assembly.name>.decompiled.cs` already exists alongside the assembly. If it does and is non-empty and contains at least one `namespace` or `class` declaration: **skip** (already decompiled), set status to "success".
3. Run ilspycmd using the `-o` flag with an explicit output file path (NOT `--outputdir` which writes a directory tree):
   `ilspycmd "<full-path>" -o "<directory-of-assembly>/<AssemblyNameWithoutExtension>.decompiled.cs"`
   Example: for `source/main/MyAssembly.dll` → `ilspycmd "source/main/MyAssembly.dll" -o "source/main/MyAssembly.decompiled.cs"`
4. Capture exit code and all output.
5. Validate the output file exists, is non-empty, and contains at least one `namespace` or `class`.
6. Record result for this assembly.

## Output

Return a JSON result record for each assembly:

```json
[
  {
    "name": "MyAssembly.dll",
    "component": "main",
    "path": "source/main/MyAssembly.dll",
    "decompile_status": "success | failed | skipped",
    "decompile_output": "source/main/MyAssembly.decompiled.cs",
    "decompile_errors": ["error text if failed"]
  }
]
```

After processing all assemblies in the batch, output the complete JSON result array. The calling `decompile` command will merge these results into `classification-manifest.json`.

## Notes

- Never abort the entire batch due to one assembly failing. Log the failure and continue.
- If ilspycmd is not found on PATH, immediately stop and output: "ERROR: ilspycmd not found. Run /bootstrap-ilspy first."
