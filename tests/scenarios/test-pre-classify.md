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
   - `assemblies[]` contains:
     - `FakeSuite.dll` → classification: "suite", decompile_status: "pending"
     - `Newtonsoft.Json.dll` → classification: "third_party", decompile_status: "skipped"
   - `unknowns[]` contains:
     - `Unknown.Library.dll` → awaiting_user_decision: true, user_classification: null (before user responds)
4. User is shown a summary of unknowns and asked to classify each as "suite" or "skip"
5. After user responds, the manifest `unknowns[]` entry for Unknown.Library.dll is updated with `user_classification: "suite"` or `"skip"` and `awaiting_user_decision: false`. If user chose "suite" or "decompile", a corresponding entry is added to `assemblies[]` with classification: "suite" and decompile_status: "pending".
6. `completed_stages` includes "pre-classify"

## Pass Criteria
- Third-party file physically deleted
- Suite file untouched
- Unknown file prompts user for decision
- Manifest written with correct structure (validate against spec schema)
- `FakeSuite.dll` has decompile_status: "pending" in assemblies[]
- `Newtonsoft.Json.dll` has decompile_status: "skipped" in assemblies[]

## Test Case B: Third-party DLL with associated .pdb and .xml

Setup: Add `Newtonsoft.Json.pdb` and `Newtonsoft.Json.xml` to the fixture alongside `Newtonsoft.Json.dll`.

Expected:
- All three files (`Newtonsoft.Json.dll`, `Newtonsoft.Json.pdb`, `Newtonsoft.Json.xml`) are deleted from disk
- The manifest deletion count summary reports 3 files deleted (not 1)
- Only `Newtonsoft.Json.dll` appears in `assemblies[]` (the .pdb and .xml are NOT individually recorded)

Pass criteria for Test Case B:
- Associated .pdb and .xml files deleted alongside the .dll
- Only the .dll level recorded in assemblies[]
