# ingest-schema

Enrich each assembly's `.ctx.md` file in-place with a `## DB Schema` section containing column definitions and lookup values sourced from the companion schema extraction output. Requires `database-context.json` to be present as a prerequisite guard.

## Required inputs

- `<profile>`: path to suite profile JSON
- `<schema-enrichment-path>`: path to `schema-enrichment.json` produced by the companion schema extraction plugin

## Steps

### 1. Load inputs

Load the suite profile JSON to read `context_output_path`.

Load `database-context.json` from the same directory as the profile (same directory as `classification-manifest.json`):
- If `database-context.json` is missing: hard stop — "Run /detect-databases before /ingest-schema."
- If `database-context.json` is present but its `schema_version` field is not `"1.0"`: hard stop — "database-context.json schema_version is '<version>' — expected '1.0'. Re-run /detect-databases to regenerate."

Load `schema-enrichment.json` from the path provided as `<schema-enrichment-path>`:
- If the file is missing: hard stop — "schema-enrichment.json not found at '<attempted path>'. Verify the path and re-run."
- If the file is present but its `schema_version` field is not `"1.0"`: hard stop — "schema-enrichment.json schema_version is '<version>' — expected '1.0'."

Load `classification-manifest.json` from the same directory as the profile:
- If missing: hard stop — "Run /pre-classify and /review-drop before /ingest-schema."

### 2. Build table lookup map

From `schema-enrichment.json`, build a flat lookup map keyed by table name (case-insensitive):

- Iterate over `databases[]` and then `tables[]` within each database
- For each table entry, store the full entry (columns, is_lookup, lookup_values) under its normalised name (lowercase)
- If two tables in different databases share the same name: use the first occurrence and record a gap note

### 3. Locate .ctx.md files

Resolve `context_output_path` relative to the profile directory. Enumerate all files with the extension `.ctx.md` recursively under that directory.

If the directory does not exist or contains no `.ctx.md` files: complete with a summary noting zero files enriched — this is not a hard stop.

### 4. Enrich each .ctx.md file

For each `.ctx.md` file found:

**a. Read the file and parse the YAML frontmatter.**

The frontmatter is the block between the opening `---` and closing `---` at the start of the file. Extract the `db_tables` field (a YAML list of table name strings). If `db_tables` is absent or empty, skip this file — no enrichment needed.

**b. Build the `## DB Schema` section.**

For each table name T in `db_tables`:

- Look up T (case-insensitive) in the table lookup map built in Step 2
- If T is not found in the map: record an informational note — "Table '<T>' in <assembly>.ctx.md not found in schema-enrichment.json — skipped" — and do not add a subsection for T; continue to the next table
- If T is found: add a `### <table_name>` subsection with:
  1. A columns table in this exact format:
     ```markdown
     | Column | Type | Nullable | Notes |
     |--------|------|----------|-------|
     | <name> | <type> | <Yes/No> | <notes> |
     ```
     - `Nullable` column: write `Yes` if `nullable: true`, `No` if `nullable: false`
     - `Notes` column: use the value of the `notes` field; write an empty cell if `notes` is absent or null
     - One row per column in the order they appear in `columns[]`
  2. If `is_lookup: true` and `lookup_values` is non-empty: add a `**Lookup values:**` subsection immediately after the columns table:
     ```markdown
     **Lookup values:**
     | Code | Description |
     |------|-------------|
     | <first-key-value> | <second-key-value> |
     ```
     - Each row corresponds to one entry in `lookup_values[]`
     - The first key of each entry object is treated as "Code", the second key as "Description"
     - One row per lookup_values entry in the order they appear

The complete `## DB Schema` section starts with `## DB Schema` on its own line, followed by one `### <table_name>` subsection per table that was found in the schema map.

**c. Replace or append the `## DB Schema` section.**

To ensure idempotency (running twice does not duplicate the section):
- Search the file content for the line `## DB Schema` (exact match, starting at the beginning of a line)
- If found: replace from that line to the end of the file with the newly built `## DB Schema` section
- If not found: append a blank line followed by the `## DB Schema` section to the end of the file

Write the updated content back to the `.ctx.md` file.

### 5. Update manifest

Add `"ingest-schema"` to the `completed_stages` array in `classification-manifest.json`. Write the updated manifest to disk.

### 6. Report summary

Output:

```
Schema ingestion complete.
  Files enriched: N
  Tables resolved: T
  Tables not found in schema: U (see notes below)
  <informational notes for each unresolved table, one per line>
```

## Error handling

- Missing `database-context.json` → hard stop: "Run /detect-databases before /ingest-schema."
- `database-context.json` `schema_version` ≠ `"1.0"` → hard stop with version mismatch message
- Missing `schema-enrichment.json` → hard stop including the attempted path
- `schema-enrichment.json` `schema_version` ≠ `"1.0"` → hard stop with version mismatch message
- Missing `classification-manifest.json` → hard stop: "Run /pre-classify and /review-drop before /ingest-schema."
- `context_output_path` directory missing or empty → complete with summary noting zero files enriched; not a hard stop
- Table in `db_tables` not in `schema-enrichment.json` → informational note in report; `.ctx.md` unchanged for that table; not a hard stop
- Two schema tables with the same name in different databases → use first occurrence; record a gap note in report

## Notes

- The `schema/` docs directory written by the previous implementation is no longer produced. `.ctx.md` enrichment is the sole output.
- `database-context.json` is validated as a prerequisite guard only — its `databases[]` content is not used by this command. The actual table data comes from `schema-enrichment.json`. The guard exists to ensure `detect-databases` has run before `ingest-schema`, establishing the pipeline ordering contract. Future versions may use access pattern data from `database-context.json` (e.g., annotating the `## DB Schema` section with which operations are performed on each table).
