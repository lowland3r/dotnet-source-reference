# Test Scenario: review-drop

## Setup
Profile: `tests/fixtures/minimal-suite/test-profile.json`
Source folder contains: `FakeSuite.decompiled.cs` (the pre-written fixture)
Manifest has: `FakeSuite.dll` → classification: "suite", decompile_status: "success"

No existing index file exists.

## Expected Behavior

1. assembly-classifier is dispatched for FakeSuite.dll with FakeSuite.decompiled.cs as input
2. A new index file is created at the path specified by `index_output_path` in the profile
3. The index file contains a markdown table row for FakeSuite.dll with:
   - File / Folder column: FakeSuite.decompiled.cs
   - Description: non-empty (and NOT containing "(Review needed)" — confidence should be high for this fixture)
   - API/Business Logic Relevant: true
   - Primary Language: C#
   - Key Public Types: includes OrderManager and IOrderRepository
   - DB Tables: ordertable, orderline (from SQL in the fixture)
   - First Indexed: today's date (YYYY-MM-DD format)
   - First Indexed Commit: result of `git rev-parse HEAD` (or blank if not a git repo)
   - Stored in repo: "Yes" (FakeSuite is relevant — written immediately by review-drop, not deferred to prune)
4. `classifier_results` in the manifest contains a key `"FakeSuite.dll"` with the full JSON result from assembly-classifier
5. `completed_stages` in manifest includes "review-drop"

## Test Case B: Review-needed flag
If assembly-classifier returns low confidence (simulate by providing a source with ambiguous content):
- The Description column in the index row should contain "(Review needed)"

## Test Case C: Third-party assembly row
If the manifest also contains a third-party assembly (e.g., Newtonsoft.Json.dll):
- A row should appear in the index with `Stored in repo: No (third-party)`
- No assembly-classifier dispatch for third-party assemblies

## Pass Criteria
- Index file created with all 9 correct columns
- assembly-classifier called for each suite assembly (not for third-party)
- `Stored in repo` set to "Yes" for relevant assemblies at time of writing (not deferred)
- `classifier_results` populated in manifest for each reviewed assembly
- Description gets "(Review needed)" appended when classifier returns low confidence
