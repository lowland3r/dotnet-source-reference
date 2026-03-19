# Test Scenario: generate-indexes

## Setup

Profile: `tests/fixtures/minimal-suite/test-profile.json`

Manifest (classification-manifest.json) contains:
- `classifier_results["FakeSuite.dll"]`:
  - `relevant: true`
  - `primary_purpose: "Order management and business logic for purchase orders"`
  - `key_public_types: [{ name: "OrderManager" }, { name: "IOrderRepository" }]`
  - `db_tables: ["ordertable", "orderline"]`
  - `cross_component_relationships: []` (empty)
  - `review_needed: false`
- `assemblies[]` entry for `FakeSuite.dll`:
  - `component: "main"`
  - `decompile_output: "tests/fixtures/minimal-suite/FakeSuite.decompiled.cs"`
  - `decompile_status: "success"`
- `completed_stages` includes `"review-drop"`

The fixture directory exists and contains the minimal-suite profile and decompiled source files.

## Expected Behavior

1. Command loads the profile and manifest successfully
2. Git commit SHA is captured (or empty string if not a git repo)
3. Existing index tables are parsed (if present) to preserve First Indexed timestamps
4. Index table for the "main" component is rebuilt:
   - Contains one row for FakeSuite.decompiled.cs
   - All 9 columns populated correctly from classifier_results
   - First Indexed and First Indexed Commit values follow the preservation rules (see test cases)
5. Cross-Component Relationships section is written (empty or with "No relationships" message if the classifier_results has empty relationships list)
6. Index file is written to the path specified in profile under `index_output_path`
7. A summary is printed showing: Components: 1, Assemblies indexed: 1, Index files written: [path]

## Test Case A: Fresh index (no existing index file)

**Setup:**
- No index file exists at the target path
- Manifest has classifier_results with FakeSuite.dll

**Expected behavior:**
1. Command creates `index-main.md` file at `<index_output_path>/index-main.md`
2. File contains the markdown table header and one data row for FakeSuite.decompiled.cs
3. Row columns are populated:
   - File / Folder: `FakeSuite.decompiled.cs`
   - Description: `Order management and business logic for purchase orders` (no "(Review needed)" suffix because review_needed is false)
   - API/Business Logic Relevant: `true`
   - Primary Language: `C#`
   - Key Public Types: `OrderManager, IOrderRepository` (comma-separated list of type names)
   - DB Tables: `ordertable, orderline` (comma-separated)
   - First Indexed: today's date in YYYY-MM-DD format
   - First Indexed Commit: output of `git rev-parse HEAD` (or empty string if not a git repo)
   - Stored in repo: `Yes` (because relevant is true)
4. Cross-Component Relationships section is present at the bottom:
   - Either lists relationships if any exist, or contains "No cross-component relationships found."
   - In this fixture, the relationships list is empty, so the section should show "No cross-component relationships found."
5. Summary output shows:
   ```
   Index generation complete.
     Components: 1
     Assemblies indexed: 1
     Index files written: tests/fixtures/minimal-suite/output/index-main.md
   ```

**Pass criteria:**
- Index file created at correct path
- All 9 columns present in table header and data row
- First Indexed is today's date (YYYY-MM-DD)
- First Indexed Commit is either a git SHA or empty string
- Stored in repo: Yes
- Cross-Component Relationships section present (empty message or list)
- Summary shows correct counts

## Test Case B: Existing index — preserve First Indexed

**Setup:**
- An existing `index-main.md` file already has a row for `FakeSuite.decompiled.cs` with:
  - `First Indexed: 2026-01-15`
  - `First Indexed Commit: abc1234567890abcdef1234567890abcdef123456`
- Manifest classifier_results has updated content:
  - `key_public_types` now includes a different set (e.g., `[{name: "OrderProcessor"}, {name: "IOrderService"}]`)
  - `db_tables` unchanged

**Expected behavior:**
1. Command parses the existing index file and extracts the row for FakeSuite.decompiled.cs
2. Rebuilt row preserves:
   - `First Indexed: 2026-01-15` (original date, not updated to today)
   - `First Indexed Commit: abc1234567890abcdef1234567890abcdef123456` (original commit, not updated)
3. Row updates:
   - `Key Public Types: OrderProcessor, IOrderService` (new values from classifier_results)
   - `DB Tables: ordertable, orderline` (unchanged, still correct)
   - Other columns also refreshed from current classifier_results
4. File is rewritten with the updated row
5. Summary shows 1 component, 1 assembly indexed

**Pass criteria:**
- First Indexed date preserved from existing row (not changed to today)
- First Indexed Commit preserved from existing row
- Key Public Types updated to current values
- File rewritten successfully

## Test Case C: Multiple assemblies in same component

**Setup:**
- Manifest `classifier_results` contains two assemblies both with `component: "main"`:
  - `FakeSuite.dll` (as usual)
  - `AnotherAssembly.dll` with similar classifier result
- Both have corresponding entries in `assemblies[]` with `decompile_status: "success"`

**Expected behavior:**
1. Command builds index with two rows (one per assembly)
2. Both rows have all columns filled
3. Index file contains both rows
4. Summary shows: Components: 1, Assemblies indexed: 2

**Pass criteria:**
- Both assemblies represented in the index
- Both rows correctly formatted

## Test Case D: Missing classifier_results

**Setup:**
- Manifest exists but has no `classifier_results` key (or it is empty dict/null)

**Expected behavior:**
- Command hard stops immediately with message: "Run /review-drop before /generate-indexes — no classifier results found."
- No index files are created or modified
- Manifest is not modified

**Pass criteria:**
- Hard stop message is clear
- No changes to filesystem or manifest

## Test Case E: Missing manifest entirely

**Setup:**
- No `classification-manifest.json` file in the profile directory

**Expected behavior:**
- Command hard stops with message: "Run /pre-classify and /review-drop before /generate-indexes."
- No files created or modified

**Pass criteria:**
- Hard stop occurs immediately
- Error message is clear

## Test Case F: review_needed flag

**Setup:**
- Manifest `classifier_results["FakeSuite.dll"]` has:
  - `review_needed: true`
  - `primary_purpose: "Ambiguous assembly that may contain business logic"`

**Expected behavior:**
1. Index row Description column contains: "Ambiguous assembly that may contain business logic (Review needed)"
2. All other columns normal
3. File written successfully

**Pass criteria:**
- "(Review needed)" appended to Description when classifier_result.review_needed is true
- Rest of row correct

## Test Case G: Irrelevant assembly

**Setup:**
- Manifest `classifier_results["FakeSuite.dll"]` has:
  - `relevant: false`
  - `primary_purpose: "Utility library with no domain logic"`
  - `key_public_types: []` (empty)
  - `db_tables: []` (empty)

**Expected behavior:**
1. Index row has:
   - API/Business Logic Relevant: `false`
   - Key Public Types: (blank)
   - DB Tables: (blank)
   - Stored in repo: `No` (because relevant is false)
2. File written successfully

**Pass criteria:**
- Irrelevant assembly row correctly shows false/No values
- Empty lists leave columns blank

## Test Case H: Third-party assembly in classifier_results

**Setup:**
- Manifest `assemblies[]` includes a third-party assembly:
  - `Newtonsoft.Json.dll`, `classification: "third_party"`
- Manifest `classifier_results` includes an entry for `"Newtonsoft.Json.dll"`:
  - `component: "main"`
  - (other fields may be present but are not used for third-party rows)

**Expected behavior:**
1. Index file includes a row for Newtonsoft.Json.dll with:
   - File / Folder: `Newtonsoft.Json.dll` (no `.decompiled.cs` suffix)
   - Description: `Third-party library`
   - API/Business Logic Relevant: `false`
   - Primary Language: (blank)
   - Key Public Types: (blank)
   - DB Tables: (blank)
   - First Indexed: today's date (or preserved if existing row)
   - First Indexed Commit: git SHA or empty (or preserved if existing row)
   - Stored in repo: `No (third-party)`
2. Summary shows: Assemblies indexed: 2 (FakeSuite + Newtonsoft.Json)

**Pass criteria:**
- Third-party assembly row has correct format
- Stored in repo column shows "No (third-party)"
- Does not attempt to parse classifier_result fields for third-party rows

## Test Case I: Assembly in classifier_results but missing from manifest.assemblies

**Setup:**
- Manifest `classifier_results` includes `"OrphanAssembly.dll"`
- But there is no corresponding entry in `manifest.assemblies[]` for OrphanAssembly.dll

**Expected behavior:**
1. Command logs a warning: "Assembly OrphanAssembly.dll in classifier_results but not found in manifest.assemblies"
2. Skips the orphan assembly (does not write a row for it)
3. Continues with other assemblies
4. Summary still shows correct count of indexed assemblies (excluding the orphan)

**Pass criteria:**
- Warning logged for orphan assembly
- No hard stop; command continues
- Orphan assembly not included in index
- Summary reflects correct count

## General Pass Criteria

- Manifest loaded successfully
- classifier_results existence validated (hard stop if missing)
- Git commit captured (or empty string if not a git repo)
- Existing index files parsed to preserve First Indexed timestamps
- Index rows built from classifier_results with correct column mapping
- First Indexed and First Indexed Commit preservation rules applied
- Cross-Component Relationships section written at bottom of each index file
- Index files written to correct path
- Summary report shows correct component and assembly counts
- All 9 columns present in table header
- Hard stop messages are clear and actionable
- Individual assembly warnings do not abort the run
- Third-party assemblies handled distinctly from suite assemblies
