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
   - API/Business Logic Relevant: true
   - Primary Language: C#
   - Key Public Types: includes OrderManager and IOrderRepository
   - DB Tables: ordertable, orderline (from SQL in the fixture)
   - First Indexed: today's date
   - First Indexed Commit: result of `git rev-parse HEAD` (or blank if not a git repo)
   - Stored in repo: (left for prune to set — default "Yes" for suite assemblies)
4. `completed_stages` in manifest includes "review-drop"

## Pass Criteria
- Index file created with correct columns
- assembly-classifier called for each suite assembly
- review_needed flag respected (row flagged if confidence < 0.7)
