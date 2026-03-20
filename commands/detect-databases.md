# detect-databases

Scan each assembly's decompiled source to determine how database tables are accessed. Produce `database-context.json` with per-table access patterns, inferred database groupings, probable lookup flags, and dynamic SQL warnings.

## Required inputs

- `<profile>`: path to suite profile JSON
- `classification-manifest.json` must exist with `classifier_results` populated and `"review-drop"` in `completed_stages`

## Steps

### 1. Load inputs

Load the suite profile JSON. Load `classification-manifest.json` from the same directory as the profile.

If the manifest file is missing: hard stop — "Run /pre-classify and /review-drop before /detect-databases."

If `classifier_results` is absent, is an empty object (`{}`), is null, or has no entries with non-empty `db_tables`: hard stop — "Run /review-drop before /detect-databases — no classifier results found."

If `"review-drop"` is not present in `completed_stages`: hard stop — "Run /review-drop before /detect-databases — review-drop stage has not completed."

### 2. Build known table set

Collect all known tables from `classifier_results`:

- `classifier_results` is an object keyed by assembly filename (e.g. `"FakeSuite.dll"`); each value is the classifier result object for that assembly
- For each assembly key K, collect all entries from `classifier_results[K].db_tables` into the known table set
- Table names in `classifier_results` are already normalised (lowercase, no schema prefix, no brackets) — do not re-normalise them
- Track assembly membership per table: for each normalised table name T, record every assembly key K whose `db_tables` includes T — this is the `referenced_in` list for T (regardless of whether SQL scanning later finds a literal match)

Build an assembly-to-decompiled-path map:

- For each assembly key K in `classifier_results`, find the entry in `assemblies[]` where `name === K`
- Extract `decompile_output` from that matching `assemblies[]` entry
- If `decompile_output` is null, absent, or the file does not exist at the resolved path: skip scanning for that assembly; record a gap note: "Assembly K: decompile_output missing or file not found — SQL patterns not scanned"
- `decompile_output` is a path relative to the profile directory; resolve it relative to the directory containing the profile JSON

### 3. Scan decompiled source files

For each assembly K that has a resolvable `decompile_output` path, read the file and scan it:

**a. Identify SQL string literals.**

A SQL string literal is a C# string value — content enclosed in double-quote characters — that contains at least one of the SQL DML keywords SELECT, INSERT, UPDATE, or DELETE as a whole word (surrounded by non-word characters or at string boundaries). The keyword check is case-insensitive.

Do not treat comment content as string literals:
- Lines where the first non-whitespace characters are `//` or `///` or `/*` or `*` are comment lines — skip them entirely
- On non-comment lines, ignore any content that appears after `//`

**b. Detect access patterns per known table.**

For each known table name T:
- Check whether T appears (case-insensitive) as a standalone word — bounded by non-alphanumeric, non-underscore characters or string boundaries — within any SQL string literal in this file
- If T appears in one or more SQL string literals in this file: collect all distinct SQL DML verbs (SELECT, INSERT, UPDATE, DELETE) that appear in those same literals (case-insensitive, as whole words) — this is the `access_patterns` contribution from assembly K for table T
- If T appears only in comments, in non-SQL strings, or not at all: record no access patterns from this file for T

**c. Detect dynamic SQL.**

A dynamic SQL pattern is present when a SQL string literal appears on the same expression line as a string concatenation operator (`+`) and a non-string operand (a variable, property, method call, or field access). Examples:
- `"SELECT * FROM " + tableName` — SQL literal followed by `+` and variable
- `"SELECT " + columnName + " FROM ordertable"` — SQL literal with concatenation on either side

When a dynamic SQL pattern is detected in assembly K: add one entry to `unresolved_references` for this assembly with `reason: "dynamic SQL"`. One entry per assembly is sufficient even if multiple patterns appear in the same file.

**d. Detect stored procedure calls.**

Look for either:
- A SQL string literal containing `EXEC ` or `EXECUTE ` (case-insensitive) as a whole word, OR
- A line containing `.CommandType = CommandType.StoredProcedure`

When detected in assembly K: add an entry to `unresolved_references` for this assembly with `reason: "stored procedure"`. If the assembly already has a `"dynamic SQL"` entry, add a separate entry for the stored procedure reason.

### 4. Detect database groupings

Scan all resolvable `.decompiled.cs` files for connection string literals to infer database names:

**a. Primary: `Initial Catalog=` or `Database=` keywords in string literals**

- Look for string literals (content inside double quotes, not in comments) containing the substring `Initial Catalog=` or `Database=` (case-insensitive for the keyword)
- Extract the value: everything after `=` up to the next `;`, `"`, or end of string; trim whitespace from both ends
- Example: `"Data Source=fake-server;Initial Catalog=FakeDB;Integrated Security=True"` → database name `FakeDB`
- Example: `"Server=.;Database=OrdersDB;Trusted_Connection=True"` → database name `OrdersDB`

**b. Secondary: SqlConnectionStringBuilder**

- Look for `.InitialCatalog = "<name>"` or `.DataSource = "<name>"` property assignment patterns
- Extract the string literal value as the database name

**c. Grouping decision:**

- If exactly one distinct database name is inferred across all scanned files: assign all tables to that single named database
- If multiple distinct database names are inferred: use namespace segments as heuristics to assign tables to databases (e.g., tables accessed in classes under namespace `Data.Reporting` belong to the reporting database); when assignment is ambiguous, place the table in the first inferred database name (alphabetically)
- If no connection string is found in any scanned file: group all tables under a single database named after `suite_name` from the profile

### 5. Compute probable_lookup flags

For each table T, after collecting all access patterns across all assemblies:

Set `probable_lookup: true` when ALL of the following hold:
- The combined `access_patterns` for T (union across all assemblies) contains ONLY `"SELECT"` — no INSERT, UPDATE, or DELETE appears anywhere for this table, AND
- At least one of: (a) `referenced_in` contains 3 or more distinct assembly names, OR (b) T's name ends with one of the suffixes `lookup`, `status`, `type`, `code`, `ref`, or `list` (case-insensitive suffix match)

Set `probable_lookup: false` for all other tables.

### 6. Write database-context.json

Write `database-context.json` to the directory containing the manifest (same directory as `classification-manifest.json`) with the following schema:

```json
{
  "schema_version": "1.0",
  "generated_at": "<ISO8601 timestamp>",
  "suite": "<suite_name from profile>",
  "components_analyzed": ["<component names>"],
  "databases": [
    {
      "name": "<inferred db name or suite_name>",
      "tables": [
        {
          "name": "<normalised table name>",
          "referenced_in": ["<assembly filenames>"],
          "access_patterns": ["SELECT", "INSERT"],
          "probable_lookup": false
        }
      ],
      "unresolved_references": [
        {
          "assembly": "<assembly filename>",
          "reason": "dynamic SQL | stored procedure"
        }
      ]
    }
  ],
  "gaps": ["<human-readable detection limitation notes>"]
}
```

Populating the output:
- `components_analyzed`: the unique `component` field values from `classifier_results` entries (the classifier result object for each assembly has a `component` field if present; otherwise use the assembly key name); deduplicate and sort alphabetically
- `databases[].tables[]`: include every table from the known table set, even those with no detected SQL literals (empty `access_patterns: []` is valid)
- `databases[].unresolved_references[]`: only assemblies that triggered a dynamic SQL or stored procedure warning; omit this array (or write `[]`) when no warnings occurred
- `gaps[]`: include one entry per assembly whose `decompile_output` was missing or the file was not found; include one entry per table that appears in `classifier_results` but was not found in any SQL string literal (possible indirect access or classifier false positive); omit the `gaps` key (or write `[]`) when there are no gaps
- Sort `tables[]` alphabetically by `name`; sort each `referenced_in[]` alphabetically; sort `unresolved_references[]` by `assembly`

### 7. Update manifest

Add `"detect-databases"` to the `completed_stages` array in the manifest. Write the updated manifest to disk.

### 8. Report summary

Output:

```
Database detection complete.
  Suite: <suite_name>
  Databases inferred: N
  Tables detected: T (across N databases)
  Probable lookups: P
  Unresolved references: U assemblies
  Output: <path to database-context.json>
```

## Error handling

- Missing manifest → hard stop: "Run /pre-classify and /review-drop before /detect-databases."
- Missing or empty `classifier_results` → hard stop: "Run /review-drop before /detect-databases — no classifier results found."
- `"review-drop"` not in `completed_stages` → hard stop: "Run /review-drop before /detect-databases — review-drop stage has not completed."
- Assembly with null/missing `decompile_output` or file not found at resolved path → skip scanning; record in `gaps[]`; do not hard stop
- Table found in `classifier_results` but not matched in any SQL literal → include in output with `access_patterns: []`; record in `gaps[]`; do not hard stop
