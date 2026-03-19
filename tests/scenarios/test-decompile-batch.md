# Test Scenario: decompile-batch agent

## Setup
batch_id: "batch-1"
assemblies:
  - { name: "FakeSuite.dll", path: "tests/fixtures/minimal-suite/FakeSuite.dll", component: "main" }
  - { name: "FakeCore.dll",  path: "tests/fixtures/minimal-suite/FakeCore.dll",  component: "main" }
profile_dir: "tests/fixtures/minimal-suite/"

Note: both DLL files are zero-byte stubs — ilspycmd will fail on them.

## Expected Behavior

1. ilspycmd is attempted for each assembly
2. Each fails (zero-byte stubs are not valid .NET assemblies)
3. Failures are recorded in the result for each assembly — NOT a hard abort
4. Agent returns a JSON array with two entries, both with decompile_status: "failed"
5. decompile_errors contains the ilspycmd error output

## Test Case B: pre-existing .decompiled.cs
If FakeSuite.decompiled.cs already exists alongside the DLL and is non-empty with a namespace declaration:
- Agent must NOT invoke ilspycmd for that assembly
- decompile_status: "skipped"
- decompile_output: path to the pre-existing FakeSuite.decompiled.cs file
- decompile_errors: []

## Pass Criteria
- Returns JSON array (not a hard stop) even when all assemblies fail
- Pre-existing valid output is detected and skipped
- Each result record contains: name, component, path, decompile_status, decompile_output, decompile_errors
