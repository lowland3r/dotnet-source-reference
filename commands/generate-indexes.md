# generate-indexes

Rebuild the index tables from `classifier_results` in the manifest without re-running the assembly-classifier. This is useful when the index format changes and needs to be rebuilt, the index file was accidentally deleted, or the user wants to regenerate indexes for a subset of assemblies.

## Required inputs

- `<profile>`: path to suite profile JSON
- `classification-manifest.json` must exist in the profile directory with `classifier_results` populated (written by review-drop)

## Steps

### 1. Load inputs

Load the profile and `classification-manifest.json` from the profile directory.

If `classification-manifest.json` does not exist: hard stop with "Run /pre-classify and /review-drop before /generate-indexes."

If the manifest exists but contains no `classifier_results` key or `classifier_results` is empty: hard stop with "Run /review-drop before /generate-indexes — no classifier results found."

### 2. Determine git commit

Run `git rev-parse HEAD` from the profile directory. Capture the output. If this fails (not a git repo), use an empty string.

### 3. Load existing index tables

For each component defined in the profile, check if `<index_output_path>/index-<component>.md` exists.

If it exists:
- Parse the markdown file to extract the existing table rows
- Build a lookup: for each row, key by the assembly filename (the `File / Folder` column value, e.g., "FakeSuite.decompiled.cs")
- Extract and preserve the `First Indexed` and `First Indexed Commit` values for each row

If it does not exist, start fresh with an empty lookup for that component.

### 4. Rebuild index table for each component

For each component defined in the profile:

#### 4a. Collect assemblies for this component

From `classifier_results` in the manifest:
- Collect all entries whose `component` field matches this component name
- Also include rows from manifest's `assemblies[]` where `classification` is `"third_party"` and the assembly name appears as a key in `classifier_results` (if any)

#### 4b. Build index rows

For each assembly, construct a row using the 9-column format:
```
| File / Folder | Description | API/Business Logic Relevant | Primary Language | Key Public Types | DB Tables | First Indexed | First Indexed Commit | Stored in repo |
```

**For suite assemblies (from classifier_results):**

- **File / Folder**: Extract from the manifest entry's `decompile_output` field; use the basename (e.g., "FakeSuite.decompiled.cs")
- **Description**: Use `classifier_result.primary_purpose`. If `classifier_result.review_needed` is true, append " (Review needed)".
- **API/Business Logic Relevant**: Use `classifier_result.relevant` (true/false)
- **Primary Language**: Always "C#"
- **Key Public Types**: Comma-joined list of type names from `classifier_result.key_public_types` (extract the `name` field from each object). If empty, leave blank.
- **DB Tables**: Comma-joined list from `classifier_result.db_tables`. If empty, leave blank.
- **First Indexed**:
  - If an existing row exists for this assembly (matched by filename): preserve the existing `First Indexed` date
  - Otherwise: today's date in YYYY-MM-DD format
- **First Indexed Commit**:
  - If existing row: preserve the existing `First Indexed Commit` value
  - Otherwise: the git commit SHA from step 2 (or empty string if not a git repo)
- **Stored in repo**:
  - If existing row: preserve the existing value
  - Otherwise: "Yes" if `classifier_result.relevant` is true, "No" if false

**For third-party assemblies:**

If an assembly in the manifest has `classification: "third_party"` and has an entry in `classifier_results`, write a row:
- **File / Folder**: the assembly filename (no `.decompiled.cs` suffix, since third-party assemblies are not decompiled; use the actual DLL name if present, otherwise the assembly name)
- **Description**: "Third-party library"
- **API/Business Logic Relevant**: false
- **Primary Language**: (leave blank or "N/A")
- **Key Public Types**: (leave blank)
- **DB Tables**: (leave blank)
- **First Indexed**: preserve from existing row if it exists; otherwise today's date
- **First Indexed Commit**: preserve from existing row if it exists; otherwise git SHA
- **Stored in repo**: "No (third-party)"

#### 4c. Error handling for individual assemblies

If an assembly appears in `classifier_results` but its entry is not found in the manifest's `assemblies[]` (missing decompile_output path): log a warning "Assembly {name} in classifier_results but not found in manifest.assemblies" and skip it (do not hard stop).

### 5. Write Cross-Component Relationships section

At the bottom of each component's index file, write or replace a section:

```markdown
## Cross-Component Relationships

<list relationships>
```

Collect all `cross_component_relationships` from the `classifier_result` JSON objects for assemblies in this component. Combine into a single bulleted list for the section.

If no relationships are found across all assemblies in the component, write:
```markdown
## Cross-Component Relationships

No cross-component relationships found.
```

### 6. Write index files

For each component, write the rebuilt table to `<index_output_path>/index-<component>.md`.

File format:
```markdown
# Index: <Component Name>

| File / Folder | Description | API/Business Logic Relevant | Primary Language | Key Public Types | DB Tables | First Indexed | First Indexed Commit | Stored in repo |
|---|---|---|---|---|---|---|---|---|
| <row 1> | ... | ... | ... | ... | ... | ... | ... | ... |
| <row 2> | ... | ... | ... | ... | ... | ... | ... | ... |
...

## Cross-Component Relationships

<relationships>
```

### 7. Report summary

```
Index generation complete.
  Components: N
  Assemblies indexed: N
  Index files written: <list of file paths>
```

## Error handling

- Missing manifest → hard stop: "Run /pre-classify and /review-drop before /generate-indexes."
- Missing classifier_results → hard stop: "Run /review-drop before /generate-indexes — no classifier results found."
- Individual assembly missing from manifest.assemblies but present in classifier_results → log warning, skip assembly, continue
- Git rev-parse fails → use empty string for commit SHA, continue

## Notes

- This command does not dispatch any agents; it rebuilds indexes purely from existing `classifier_results` stored in the manifest
- The command preserves `First Indexed` and `First Indexed Commit` values for rows that already exist in the index file, ensuring those timestamps are not lost across regenerations
- Relative paths in `index_output_path` are resolved relative to the directory containing the profile file
