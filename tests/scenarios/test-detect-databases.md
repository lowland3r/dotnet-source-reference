# Test Scenario: detect-databases

## Setup

Profile: `tests/fixtures/minimal-suite/test-profile.json`

Classification manifest with `classifier_results` containing:
- `FakeSuite.dll`: `db_tables: ["ordertable", "orderline"]`
- `AnotherService.dll`: `db_tables: ["ordertable", "invoicetable"]` (ordertable appears in both — deduplication required across assemblies)
- `Newtonsoft.Json.dll`: not present in `classifier_results` (third-party, skipped by review-drop)

The manifest's `completed_stages` must include `"review-drop"`.

## Test Case A: Normal run with deduplication

### Setup

- Manifest contains classifier_results for FakeSuite.dll and AnotherService.dll as described above

### Expected Behavior

1. `db-detection.json` is written to the same directory as the manifest
2. `detected_tables` array contains exactly 3 objects (in sorted order):
   - `{ "table": "invoicetable", "referenced_by": ["AnotherService.dll"] }`
   - `{ "table": "orderline", "referenced_by": ["FakeSuite.dll"] }`
   - `{ "table": "ordertable", "referenced_by": ["FakeSuite.dll", "AnotherService.dll"] }`
3. The `ordertable` entry has `referenced_by` containing both assembly names
4. `total_unique_tables` = 3
5. `generated` field is set to today's date (YYYY-MM-DD format)
6. `completed_stages` in manifest is updated to include `"detect-databases"`
7. Updated manifest is written back to disk

### Pass Criteria

- Table names are normalised (lowercased)
- Duplicate table names across assemblies are deduplicated
- `referenced_by` arrays contain all assemblies that reference each table
- `detected_tables` are sorted by table name

## Test Case B: Normalisation

### Setup

- Manifest contains classifier_results for a single assembly with mixed-case and prefixed table names:
  - `db_tables: ["dbo.OrderTable", "[orderline]", "ordertable"]`

### Expected Behavior

1. All three variants normalise to exactly 2 unique tables: `ordertable` and `orderline`
2. Deduplication is correct: dbo.OrderTable, [orderline], and ordertable all normalise correctly
3. `detected_tables` contains exactly 2 entries
4. `total_unique_tables` = 2

### Pass Criteria

- Schema prefixes (dbo., schema.) are stripped
- Square brackets are stripped
- Case normalisation works correctly
- Multiple variant forms of the same table name are collapsed to one

## Test Case C: Empty db_tables

### Setup

- Manifest contains classifier_results for two assemblies:
  - Assembly A: `db_tables: []` (empty array)
  - Assembly B: has no `db_tables` field at all

### Expected Behavior

1. `db-detection.json` is still written
2. `detected_tables` is an empty array
3. `total_unique_tables` = 0
4. Command completes without hard stop

### Pass Criteria

- Missing or empty `db_tables` fields are handled gracefully
- Empty result is not treated as an error
- Valid JSON is written even with no tables

## Test Case D: Missing classifier_results

### Setup

- Manifest exists but `classifier_results` field is missing or an empty object `{}`

### Expected Behavior

- Command hard stops with message: "Run /review-drop before /detect-databases — no classifier results found."
- No output files are written

### Pass Criteria

- Missing/empty classifier_results triggers hard stop before any output

## Test Case E: Missing manifest

### Setup

- Profile exists but `classification-manifest.json` does not exist

### Expected Behavior

- Command hard stops with message: "Run /pre-classify and /review-drop before /detect-databases."
- No output files are written

### Pass Criteria

- Missing manifest triggers hard stop before any operations

## Test Case F: Referenced assembly count

### Setup

- Manifest contains classifier_results for 3 assemblies:
  - Assembly A: `db_tables: ["table1", "table2"]`
  - Assembly B: `db_tables: ["table1", "table3"]`
  - Assembly C: `db_tables: ["table2", "table3"]`

### Expected Behavior

1. `detected_tables` contains 3 unique tables: table1, table2, table3
2. `referenced_by` counts are:
   - table1: 2 assemblies (A, B)
   - table2: 2 assemblies (A, C)
   - table3: 2 assemblies (B, C)
3. Report summary shows "Referenced by assemblies: 3" (unique assembly count across all tables)

### Pass Criteria

- Assembly reference counts are correct per table
- Total unique assembly count in summary is accurate
