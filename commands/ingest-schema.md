# ingest-schema

Reads a `schema-enrichment.json` file and enriches the index by writing per-table schema documentation files organized by database. Creates a searchable schema index.

## Required inputs

- `<profile>`: path to suite profile JSON
- `<schema-enrichment-path>`: path to the `schema-enrichment.json` file
- `classification-manifest.json` must exist

## Steps

### 1. Load inputs

Load the profile JSON, the classification manifest (from the same directory as the profile), and the `schema-enrichment.json` file from the provided path.

If manifest is missing: hard stop with "Run /pre-classify before /ingest-schema."

If `schema-enrichment.json` is missing or unreadable: hard stop with a descriptive error message that includes the attempted path.

Validate that `schema-enrichment.json` has at minimum:
- `databases` array present
- At least one entry in the `databases` array

If validation fails: hard stop with descriptive message.

### 2. Create schema directory

Create directory `<index_output_path>/schema` if it does not exist.

### 3. For each database and table, write table schema file

For each database in `schema-enrichment.json`, for each table in that database:

Write `<index_output_path>/schema/<db_name>/<table_name>.md` (where `db_name` and `table_name` are normalised to lowercase with spaces replaced by underscores) in the following format:

```markdown
# <TableName>

**Database**: <db_name>
**Columns**: <count>

## Columns

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| fcorderid | varchar(10) | No | PK — order identifier |
| fccustid | varchar(10) | No | FK to customer identifier |
| fcstatus | char(2) | No | FK → statuslookup.fcstatus |

## Lookup Values

(include this section only if the table has `is_lookup: true`)

| Code | Label |
|------|-------|
| OP | Open |
| CL | Closed |

## Cross-References

Assemblies that reference this table (from classifier_results):
- `FakeSuite.dll` (component: main)

(If no assemblies reference this table, write: "No assemblies in the suite reference this table.")
```

**Rules for the Columns table**:
- Column names come from the `name` field
- Types come from the `type` field (as-is, with no modification)
- Nullable status is "Yes" if `nullable: true`, "No" if `nullable: false`
- Notes come from the `notes` field; if the field is absent or empty, leave the Notes cell empty

**Rules for Lookup Values section**:
- Only include this section if the table has `is_lookup: true`
- The table format uses the **value** of the first key in each lookup value object as Code and the **value** of the second key as Label
- All lookup values from the `lookup_values` array are included
- If `is_lookup: false` or absent, omit the entire Lookup Values section

**Rules for Cross-References section**:
- Query the manifest's `classifier_results` for all assemblies that have this table name in their `db_tables` array (use exact case-insensitive match against the schema table name)
- For each matching assembly, look up its component from the `component` field in the classifier result
- List each assembly as `<assembly_name>` (component: <component_name>)
- If no assemblies reference this table, write exactly: "No assemblies in the suite reference this table."
- Sort assemblies alphabetically by name within the list

### 4. Write schema index file

Write `<index_output_path>/schema/index.md` with the following format:

```markdown
# Schema Index

| Table | Database | Columns | Lookup | Referenced By |
|-------|----------|---------|--------|---------------|
| ordertable | FakeDB | 3 | No | FakeSuite.dll |
| statuslookup | FakeDB | 2 | Yes | (none) |
```

**Rules**:
- Collect all tables from all databases in `schema-enrichment.json`
- Sort tables alphabetically by table name (case-insensitive)
- For each table:
  - **Table**: table name from schema
  - **Database**: database name from schema
  - **Columns**: count of columns (length of the table's `columns` array)
  - **Lookup**: "Yes" if `is_lookup: true`, "No" otherwise
  - **Referenced By**: comma-separated list of assembly names (from classifier_results) that reference this table, sorted alphabetically. If no assemblies reference the table, write "(none)"

### 5. Update manifest

Add `"ingest-schema"` to the `completed_stages` array in the manifest (if not already present). Write the updated manifest to disk.

### 6. Report summary

Output a summary in this format:

```
Schema ingestion complete.
  Databases: N
  Tables: M
  Schema files written: <schema file 1>, <schema file 2>, ...
  Index: <path to schema/index.md>
```

- N is the count of databases in `schema-enrichment.json`
- M is the total count of all tables across all databases
- List schema file paths relative to the profile directory (e.g., `output/reference/schema/FakeDB/ordertable.md`)

## Error handling

- Missing manifest → hard stop "Run /pre-classify before /ingest-schema."
- Missing or invalid `schema-enrichment.json` → hard stop with descriptive error that includes the attempted path and reason
- Invalid `schema-enrichment.json` format (no `databases` array or empty) → hard stop with descriptive message
- Table in `schema-enrichment.json` with no matching entries in classifier_results → write the file anyway. This is not an error; the table may be documented even if no current assembly references it.
- `is_lookup` false or absent → omit the Lookup Values section. This is expected behavior, not an error.

## Notes

- The `schema-enrichment.json` format is documented in `tests/fixtures/schema-fixture/schema-enrichment.json`
- Relative paths in `<schema-enrichment-path>` are resolved relative to the profile directory
- `schema-enrichment.json` is provided externally — this command does not generate it
- Table and database names in the schema file paths use lowercase with underscores replacing spaces
