# detect-databases

Scan the manifest's classifier results to produce a list of unique database tables referenced across the entire suite. Write a db-detection.json summary file.

This is a pure data aggregation step — no agents dispatched.

## Required inputs

- `<profile>`: path to suite profile JSON
- `classification-manifest.json` must exist with `classifier_results` populated

## Steps

### 1. Load inputs

Load the profile JSON and the classification manifest from the same directory as the profile. If manifest is missing: hard stop with "Run /pre-classify and /review-drop before /detect-databases."

If `classifier_results` is absent or empty: hard stop with "Run /review-drop before /detect-databases — no classifier results found."

### 2. Aggregate db_tables

For each entry in `classifier_results`, collect all values from the `db_tables` array.

For each table name found, apply normalisation in the following order:
1. Strip any schema prefix: remove everything up to and including the first `.` (handles `dbo.Table`, `schema.Table`, `dbo.[Table]`)
2. Strip surrounding square brackets: remove `[` and `]` if present
3. Lowercase the result
4. After applying these rules, deduplicate exact matches

Examples: `dbo.OrderTable` → `ordertable`, `[orderline]` → `orderline`, `schema.table` → `table`, `dbo.[OrderTable]` → `ordertable`

Track which assemblies reference each normalised table name. For each unique normalised table, maintain a list of assembly names (the `assembly_name` key from `classifier_results` entries).

If an assembly has no `db_tables` field, or the field is empty, or the field is null: skip silently (not an error).

### 3. Write db-detection.json

Write `db-detection.json` to the directory containing the manifest (same directory as `classification-manifest.json`) with the following schema:

```json
{
  "detected_tables": [
    {
      "table": "ordertable",
      "referenced_by": ["FakeSuite.dll"]
    },
    {
      "table": "orderline",
      "referenced_by": ["FakeSuite.dll"]
    }
  ],
  "total_unique_tables": 2,
  "generated": "2026-03-19"
}
```

- `detected_tables`: array of objects, each with `table` (normalised name in lowercase) and `referenced_by` (array of assembly names that reference this table)
- `total_unique_tables`: count of unique normalised table names
- `generated`: today's date in YYYY-MM-DD format

Sort the `detected_tables` array by `table` name (case-insensitive alphabetically). Within each object's `referenced_by` array, sort alphabetically (ascending) by assembly filename.

If no tables are found after aggregation and normalisation (all assemblies have empty or missing `db_tables`): write `db-detection.json` with `detected_tables: []` and `total_unique_tables: 0`. This is not an error condition.

### 4. Update manifest

Add `"detect-databases"` to the `completed_stages` array in the manifest. Write the updated manifest to disk.

### 5. Report summary

Output a summary:

```
Database detection complete.
  Unique tables found: N
  Referenced by assemblies: M
  Output: <path to db-detection.json>
```

- N is the value of `total_unique_tables`
- M is the count of distinct assemblies across all `referenced_by` lists

## Notes

- `db-detection.json` is a discovery summary for human reference. It is not the input to `/ingest-schema`. The `/ingest-schema` command reads `schema-enrichment.json`, which is produced by a separate companion schema extraction tool.

## Error handling

- Missing manifest → hard stop "Run /pre-classify and /review-drop before /detect-databases."
- Missing or empty `classifier_results` → hard stop "Run /review-drop before /detect-databases — no classifier results found."
- Assembly with no `db_tables` field or empty `db_tables` → skip silently
- Empty result (no tables at all) → write valid JSON with empty `detected_tables: []` and `total_unique_tables: 0`. This is not a hard stop.
