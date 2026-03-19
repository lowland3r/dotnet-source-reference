# Test Scenario: bootstrap-ilspy

## Setup
You are testing the `bootstrap-ilspy` command. The command's instruction file is at `commands/bootstrap-ilspy.md`.

## Test Case A: ilspycmd already installed
Simulate that `ilspycmd` is already available on PATH by pretending `dotnet tool list -g` returns a line containing `ilspycmd`.

Expected behavior:
- Command reports: "ilspycmd is already installed" (or similar confirmation)
- Command does NOT attempt to run `dotnet tool install`
- Command exits successfully

## Test Case B: ilspycmd not installed
Simulate that `dotnet tool list -g` returns output with no mention of ilspycmd.

Expected behavior:
- Command runs: `dotnet tool install -g ilspycmd`
- If install succeeds: reports "ilspycmd installed successfully"
- If `dotnet` is not on PATH: hard stop with message "dotnet SDK is required. Install from https://dot.net and re-run."
- If install fails for any other reason: surface the exact error from `dotnet tool install` verbatim and halt

## Pass Criteria
- Correct detection of installed vs. not-installed state
- No install attempt when already installed
- Correct hard-stop message when dotnet SDK missing
- Verbatim error surfacing when install fails
