# ilspy-runner

Shared skill for invoking the ilspycmd decompiler. Use this skill whenever you need to run ilspycmd.

## Invocation Pattern

To decompile a single assembly to a single `.decompiled.cs` file, use the `-o` flag with an explicit output file path (NOT a directory). This produces one flat `.cs` file:

```bash
ilspycmd "<assembly-path>" -o "<output-path>.decompiled.cs"
```

For a DLL at `source/MyAssembly.dll`, the command is:
```bash
ilspycmd "source/MyAssembly.dll" -o "source/MyAssembly.decompiled.cs"
```

This writes exactly `source/MyAssembly.decompiled.cs` as a single file.

**Do NOT use `--outputdir`** — that flag writes a directory tree of per-namespace `.cs` files, which breaks the single-file path assumptions used throughout the pipeline.

The output file name convention is always: `<AssemblyNameWithoutExtension>.decompiled.cs` in the same directory as the source assembly.

## Checking if ilspycmd is Installed

```bash
dotnet tool list -g
```

If the output contains a line with `ilspycmd`, it is installed. Otherwise it is not.

## Error Capture

Always capture stderr alongside stdout. A non-zero exit code from ilspycmd indicates failure. Capture the full output and include it in any error report.

Common failure modes:
- "Unable to find assembly" — the path is wrong or the file is not a valid .NET assembly
- "Not a valid PE file" — the file is not a .NET assembly (may be a native DLL, data file, etc.)
- Truncated output / partial `.decompiled.cs` — treat as failure; delete partial output

## Output Validation

After running ilspycmd, verify the output file:
1. Exists at the expected path
2. Is non-empty (size > 0)
3. Contains at least one `namespace` or `class` declaration

If any check fails, treat as a decompile failure.
