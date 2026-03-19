# Test Scenario: pre-classify

## Setup
Profile: `tests/fixtures/minimal-suite/test-profile.json`
Source folder: `tests/fixtures/minimal-suite/`

The folder contains:
- `FakeSuite.dll` — matches `FakeSuite*` (suite)
- `Newtonsoft.Json.dll` — matches `Newtonsoft.*` (third_party)
- `Unknown.Library.dll` — matches neither pattern (unknown)

## Expected Behavior

1. `Newtonsoft.Json.dll` is immediately deleted from disk
2. `FakeSuite.dll` and `Unknown.Library.dll` remain on disk
3. `classification-manifest.json` is written with:
   - `FakeSuite.dll` → classification: "suite"
   - `Newtonsoft.Json.dll` → classification: "third_party", decompile_status: "skipped"
   - `Unknown.Library.dll` → classification: "unknown", awaiting_user_decision: true
4. User is shown a summary of unknowns and asked to classify each as "suite" or "skip"
5. After user responds, manifest is updated with `user_classification` and `awaiting_user_decision: false`
6. `completed_stages` includes "pre-classify"

## Pass Criteria
- Third-party file physically deleted
- Suite file untouched
- Unknown file prompts user for decision
- Manifest written with correct structure (validate against spec schema)
