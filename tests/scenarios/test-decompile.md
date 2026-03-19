# Test Scenario: decompile

## Setup
Profile: `tests/fixtures/minimal-suite/test-profile.json`
Pre-existing manifest: a `classification-manifest.json` where:
- `FakeSuite.dll` → classification: "suite", decompile_status: "pending"
- `Newtonsoft.Json.dll` → classification: "third_party", decompile_status: "skipped"
- `Unknown.Library.dll` → unknowns → user_classification: "skip"

Note: `FakeSuite.dll` is a zero-byte stub — ilspycmd will fail on it.

## Expected Behavior

1. Only `FakeSuite.dll` is attempted (suite + user-classified-as-suite entries only)
2. ilspycmd fails on the zero-byte stub (expected)
3. Failure is logged to `decompile_errors` in the manifest
4. `decompile_status` is updated to "failed" for FakeSuite.dll
5. Summary is shown: "1 assembly decompiled, 0 succeeded, 1 failed"
6. `completed_stages` now includes "decompile"
7. Process does NOT abort — failure is surfaced as a summary

## Pass Criteria
- Skipped assemblies not attempted
- Failed decompile logged to manifest, not a hard stop
- completed_stages updated
- Summary output shown
