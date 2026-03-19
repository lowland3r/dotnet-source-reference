# pre-classify

Classify all assemblies in the source component folders against the suite profile. Immediately delete third-party binaries. Write classification-manifest.json.

## Required inputs
- `<profile>`: path to a suite profile JSON file (see config/schema.json for format)

## Steps

### 1. Load the profile

Read the profile JSON from `<profile>`. Resolve all component `path` values relative to the directory containing the profile file.

### 2. Scan all component folders

For each component in the profile, list all files with extensions: `.dll`, `.exe`, `.pdb`, `.xml`.

### 3. Classify each file

For each file, match its filename (without path) against the profile patterns using glob matching:

- If filename matches any pattern in `known_suite_patterns` → classification: **suite**
- If filename matches any pattern in `known_third_party_patterns` → classification: **third_party**
- If it matches neither → classification: **unknown**

Pattern matching rules:
- `*` matches any sequence of characters within a filename segment
- `.` is literal
- Match is case-insensitive

### 4. Delete third-party files immediately

For each file classified as **third_party**: delete the file from disk. This includes `.dll`, `.exe`, `.pdb`, and `.xml` files with third-party names.

Do NOT delete `.config` or `.exe.config` files at this stage — those may contain useful configuration.

### 5. Collect unknowns and prompt user

For each file classified as **unknown**, show the user a numbered list:
```
Unknown assemblies found (matched neither suite nor third-party patterns):
  1. Unknown.Library.dll (component: main)
  2. ...

For each, enter: [s]uite, [k]ip, or [d]ecompile (same as suite):
```

Wait for the user to respond for each unknown. Record their decisions.

If `unknown_default` in the profile is `"skip"` and the user does not respond, default to skip.

### 6. Write classification-manifest.json

Write `classification-manifest.json` to the directory containing the profile file.

Schema:
```json
{
  "schema_version": "1.0",
  "generated_at": "<ISO8601 timestamp>",
  "suite": "<profile.suite_name>",
  "components_analyzed": ["<component names>"],
  "completed_stages": ["pre-classify"],
  "assemblies": [
    {
      "name": "<filename>",
      "component": "<component name>",
      "path": "<relative path from profile dir>",
      "classification": "suite | third_party",
      "decompile_status": "pending | skipped",
      "decompile_output": null,
      "decompile_errors": []
    }
  ],
  "unknowns": [
    {
      "name": "<filename>",
      "component": "<component name>",
      "path": "<relative path>",
      "awaiting_user_decision": false,
      "user_classification": "suite | skip"
    }
  ],
  "classifier_results": {}
}
```

- `assemblies[]` contains all suite-classified and third-party files — recorded at the assembly (`.dll`/`.exe`) level only. Associated `.pdb` and `.xml` files deleted alongside a third-party assembly are not individually recorded in the manifest; they are tracked implicitly (same base name as the deleted assembly). The deletion count reported in the summary includes `.pdb`/`.xml` files for transparency.
- `unknowns[]` contains files with neither pattern; `user_classification` is the resolved decision
- `decompile_status` for third-party and skip entries is `"skipped"`; for suite entries is `"pending"`
- `classifier_results` is populated by `review-drop` — keyed by assembly filename, value is the full JSON result from `assembly-classifier`. Downstream stages (`generate-context`) read from here instead of parsing the markdown index table, avoiding fragile text parsing. Example entry:
```json
"FakeSuite.dll": {
  "relevant": true,
  "confidence": 0.95,
  "primary_purpose": "Business logic layer for order lifecycle management",
  "key_public_types": [{ "name": "OrderManager", "description": "Manages order lifecycle" }],
  "db_tables": ["ordertable", "orderline"],
  "cross_component_relationships": [],
  "review_needed": false
}
```

### 7. Report summary

Output a summary:
```
Pre-classification complete.
  Suite assemblies (will decompile): N
  Third-party (deleted): N
  Unknown → suite: N
  Unknown → skip: N

classification-manifest.json written.
```

## Error handling

- If no `.dll` or `.exe` files are found in any component folder: hard stop with "No assemblies found in component folders. Check that the profile paths are correct."
- If the profile file cannot be read or fails JSON validation: hard stop and show the validation error.
