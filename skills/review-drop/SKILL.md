---
name: review-drop
description: Use when assessing relevance of decompiled assemblies and updating index tables after decompilation - dispatches assembly-classifier agents and populates classifier_results in the manifest
argument-hint: "<profile>"
---

# review-drop

Assess each decompiled assembly for relevance using the assembly-classifier agent. Create or update the index table for each component. All component sources are loaded together to enable cross-component analysis.

## Required inputs
- `<profile>`: path to suite profile JSON
- `classification-manifest.json` must exist and contain at least one assembly with `decompile_status: "success"`

## Steps

### 1. Load all decompiled sources

For every assembly in the manifest with `decompile_status: "success"`, read the full text of its `.decompiled.cs` file. Build a list: `{ name, component, decompiled_source }`.

### 2. Run assembly-classifier for each assembly

For each assembly, dispatch the `assembly-classifier` agent with:
- `assembly_name`: the assembly filename
- `component`: the assembly's component name
- `decompiled_source`: the text of its `.decompiled.cs`
- `all_component_sources`: the full list from step 1 (all components, for cross-reference)
- `profile`: the profile JSON

Collect the JSON result.

### 3. Determine git commit

Run `git rev-parse HEAD` from the profile directory. Capture the output. If this fails (not a git repo), use an empty string.

### 4. Write or update index tables

For each component defined in the profile, write or update `<index_output_path>/index-<component>.md`.

Index table format (9 columns):
```
| File / Folder | Description | API/Business Logic Relevant | Primary Language | Key Public Types | DB Tables | First Indexed | First Indexed Commit | Stored in repo |
```

Rules:
- If an assembly already has a row in the index (matched by filename): update `Key Public Types` and `DB Tables` columns only; preserve `First Indexed`, `First Indexed Commit`, `Stored in repo`.
- If no existing row: add a new row. Set `First Indexed` to today's date (YYYY-MM-DD). Set `First Indexed Commit` to the git commit SHA from step 3. Set `Stored in repo` to "Yes" for relevant assemblies, "No" for irrelevant.
- If `review_needed: true` from the classifier: append "(Review needed)" to the Description column.

Also write rows for third-party assemblies (from the manifest), marking them `Stored in repo: No (third-party)`.

### 5. Append Cross-Component Relationships section

At the bottom of each index file, write or replace a section:
```markdown
## Cross-Component Relationships
<list relationships collected from assembly-classifier results>
```

### 6. Update manifest

Add "review-drop" to `completed_stages`. Also populate `classifier_results` in the manifest: for each assembly reviewed, store the full JSON result from assembly-classifier keyed by assembly filename. This allows downstream stages to read structured classifier data without parsing the markdown index table.

### 7. Report summary

```
Review complete.
  Assemblies reviewed: N
  Relevant: N
  Irrelevant: N
  Flagged for review: N
  Index files updated: <list>
```

## Error handling

- Missing manifest → hard stop: "Run /pre-classify and /decompile before /review-drop"
- No successfully decompiled assemblies → warn and exit cleanly (nothing to review)
- assembly-classifier returns low confidence → flag row, do not hard stop
