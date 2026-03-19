# Test Scenario: ingest-schema

## Setup

Profile: `tests/fixtures/minimal-suite/test-profile.json`

Schema enrichment: `tests/fixtures/schema-fixture/schema-enrichment.json`

Manifest: exists, `completed_stages` includes `"review-drop"`, and `classifier_results` contains:
- `FakeSuite.dll` (component: main) with `db_tables: ["ordertable", "orderline"]`

The schema-enrichment.json fixture defines two tables in the FakeDB database:
- `ordertable`: 3 columns, `is_lookup: false`, no lookup values
- `statuslookup`: 2 columns, `is_lookup: true`, 3 lookup values (OP, CL, HD)

## Test Case A: Normal run

### Setup

- Profile and manifest present and valid
- Schema enrichment file exists and is readable
- Classifier results contain FakeSuite.dll with references to ordertable and orderline

### Expected Behavior

1. Schema directory `<index_output_path>/schema` is created
2. Database subdirectory `<index_output_path>/schema/fakedb` is created
3. `ordertable.md` is written to `<index_output_path>/schema/fakedb/ordertable.md` with:
   - Header showing table name and database
   - Columns section with all 3 columns (fcorderid, fccustid, fcstatus) and their types, nullability, and notes
   - NO Lookup Values section (is_lookup: false)
   - Cross-References section listing `FakeSuite.dll (component: main)`
4. `statuslookup.md` is written to `<index_output_path>/schema/fakedb/statuslookup.md` with:
   - Header showing table name and database
   - Columns section with both columns
   - Lookup Values section with all 3 lookup values (OP: Open, CL: Closed, HD: On Hold)
   - Cross-References section saying "No assemblies in the suite reference this table."
5. Schema index `<index_output_path>/schema/index.md` is written with a table containing both tables, sorted alphabetically
6. Manifest's `completed_stages` is updated to include `"ingest-schema"`
7. Manifest is written back to disk
8. Summary output shows: Databases: 1, Tables: 2, file paths, index path

### Pass Criteria

- All three markdown files are created with correct structure
- Columns table includes all fields with proper formatting
- Lookup Values section appears only in statuslookup.md, not in ordertable.md
- Cross-References correctly identifies which assemblies use each table
- Schema index lists both tables in alphabetical order
- Manifest is updated correctly
- Summary output contains all expected information

## Test Case B: Lookup table with lookup values

### Setup

- Same as Test Case A
- Schema enrichment fixture specifies statuslookup with `is_lookup: true` and 3 lookup values

### Expected Behavior

1. `statuslookup.md` includes Lookup Values section
2. Lookup Values table has correct headers and all 3 rows:
   - OP | Open
   - CL | Closed
   - HD | On Hold
3. Columns in statuslookup match the fixture exactly

### Pass Criteria

- Lookup Values section is present for is_lookup: true
- All lookup values from fixture appear in the table
- Section format matches specification exactly

## Test Case C: Table with no assembly references

### Setup

- Same as Test Case A
- The statuslookup table in schema-enrichment.json has no corresponding entries in classifier_results db_tables

### Expected Behavior

1. `statuslookup.md` is written successfully (not an error)
2. Cross-References section reads exactly: "No assemblies in the suite reference this table."
3. In schema/index.md, the statuslookup row shows blank in the "Referenced By" column as "(none)"

### Pass Criteria

- Tables without assembly references are documented anyway (not errors)
- Cross-References section uses exact wording for unreferenced tables
- Schema index shows "(none)" for unreferenced tables

## Test Case D: Table with multiple assembly references

### Setup

- Manifest's classifier_results contains two assemblies with db_tables:
  - `FakeSuite.dll` (component: main) with `db_tables: ["ordertable", "orderline"]`
  - `ReportService.dll` (component: reporting) with `db_tables: ["ordertable", "reportcache"]`
- Schema enrichment contains only ordertable

### Expected Behavior

1. `ordertable.md` Cross-References section lists both assemblies:
   - `FakeSuite.dll (component: main)`
   - `ReportService.dll (component: reporting)`
2. Assemblies are sorted alphabetically in the list
3. In schema/index.md, the ordertable row shows both assembly names comma-separated in Referenced By

### Pass Criteria

- Multiple assembly references are all listed
- Assemblies are sorted alphabetically
- Both are present in both the table markdown and index

## Test Case E: Missing schema-enrichment.json

### Setup

- Profile exists
- `<schema-enrichment-path>` points to a non-existent file

### Expected Behavior

- Command hard stops with error message that includes:
  - "schema-enrichment.json" or the provided path
  - The reason (file not found, unreadable, etc.)
- No output files are created
- Manifest is not modified

### Pass Criteria

- Missing schema file triggers hard stop before any output
- Error message is descriptive and includes the path

## Test Case F: Invalid schema-enrichment.json format

### Setup

- Schema enrichment file exists but is invalid JSON, or lacks `databases` array, or `databases` is empty

### Expected Behavior

- Command hard stops with descriptive error
- Message indicates the validation failure
- No output files are created

### Pass Criteria

- Invalid format triggers hard stop
- Error message is clear about what is wrong

## Test Case G: Missing manifest

### Setup

- Profile exists
- `classification-manifest.json` does not exist
- Schema enrichment file is valid

### Expected Behavior

- Command hard stops with message: "Run /pre-classify before /ingest-schema."
- No output files are created

### Pass Criteria

- Missing manifest triggers hard stop before processing schema
- Hard stop message is exact as specified

## Test Case H: Schema index format

### Setup

- Same as Test Case A
- Two tables: ordertable and statuslookup

### Expected Behavior

1. Schema index file has a markdown table with headers: Table, Database, Columns, Lookup, Referenced By
2. Rows are sorted alphabetically by table name:
   - ordertable first (alphabetically)
   - statuslookup second
3. Column counts are correct (3 for ordertable, 2 for statuslookup)
4. Lookup column shows "No" for ordertable, "Yes" for statuslookup
5. Referenced By shows appropriate assembly or "(none)"

### Pass Criteria

- Index table has correct structure and headers
- Rows are sorted alphabetically by table name
- All columns are populated correctly
- "(none)" appears for unreferenced tables
