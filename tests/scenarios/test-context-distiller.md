# Test Scenario: context-distiller agent

## Setup

All test cases use these inputs:

**assembly_name**: "FakeSuite.dll"
**component**: "main"
**decompiled_source**: <full text of tests/fixtures/minimal-suite/FakeSuite.decompiled.cs>
**profile**: <tests/fixtures/minimal-suite/test-profile.json>
**classifier_result**: Built from assembly-classifier output for FakeSuite

```json
{
  "assembly": "FakeSuite.dll",
  "component": "main",
  "relevant": true,
  "confidence": 0.95,
  "primary_purpose": "Order management facade and data access layer",
  "key_public_types": [
    { "name": "OrderManager", "description": "Manages order creation, retrieval, and status transitions" },
    { "name": "IOrderRepository", "description": "Repository contract for order persistence" }
  ],
  "db_tables": ["ordertable", "orderline"],
  "cross_component_relationships": [],
  "review_needed": false
}
```

---

## Test Case A: Normal Distillation (Relevant Assembly)

**Inputs**: Use the setup above with `classifier_result.relevant: true` and non-empty `db_tables`.

**Expected Behavior**:

1. **File written**: `.ctx.md` file created at `<context_output_path>/main/FakeSuite.ctx.md`

2. **Front-matter**: Present with all 7 required fields:
   - `assembly: FakeSuite.dll`
   - `component: main`
   - `generated: <today's date in YYYY-MM-DD format>`
   - `primary_language: C#`
   - `relevant: true`
   - `key_types: [OrderManager, IOrderRepository]` (list of strings)
   - `db_tables: [ordertable, orderline]` (list of strings)

3. **Heading**: `# FakeSuite` (assembly stem, title-cased)

4. **Summary section**: Non-empty prose (2–4 sentences) describing order management and data access. May reference the purpose from classifier result.

5. **Public API section**: Documents both key types:
   - `### OrderManager` — lists GetOrder and CreateOrder methods with signatures and brief descriptions
   - `### IOrderRepository` — lists FindById and Save methods with signatures and brief descriptions
   - No private members or compiler artifacts

6. **SQL / DB Usage section**: Present with SQL queries grouped by table:
   - **ordertable**: Shows SELECT and INSERT query strings from the source
   - Queries are wrapped in fenced code blocks with `sql` tag

7. **Cross-Component References section**: ABSENT (classifier_result.cross_component_relationships is empty)

8. **JSON result**:
   ```json
   {
     "assembly_name": "FakeSuite.dll",
     "ctx_path": "<absolute or relative path to .ctx.md file>",
     "status": "ok",
     "error": null
   }
   ```
   (error field may be omitted or null when status is "ok")

---

## Test Case B: Irrelevant Assembly

**Inputs**: Use setup but with `classifier_result.relevant: false`.

Example classifier_result:
```json
{
  "assembly": "StringHelper.dll",
  "component": "utilities",
  "relevant": false,
  "confidence": 0.9,
  "primary_purpose": "Generic string utility extensions",
  "key_public_types": [],
  "db_tables": [],
  "cross_component_relationships": [],
  "review_needed": false
}
```

**Provided decompiled source**: Can be minimal:
```csharp
namespace StringHelper {
  public static class StringExtensions {
    public static string ToTitleCase(this string input) { ... }
  }
}
```

**Expected Behavior**:

1. **File written**: `.ctx.md` file created (e.g., `<context_output_path>/utilities/StringHelper.ctx.md`)

2. **Front-matter**: Present with all 7 fields:
   - `relevant: false`
   - `key_types: []` (empty list)
   - `db_tables: []` (empty list)

3. **Heading**: `# StringHelper`

4. **Summary section only**: Single line: "Marked as not relevant to suite business logic."

5. **No other sections**: `## Public API`, `## SQL / DB Usage`, and `## Cross-Component References` are completely omitted.

6. **JSON result**: `status: "ok"`, file path non-empty.

---

## Test Case C: Empty Decompiled Source

**Inputs**: Use setup but with `decompiled_source: ""` (empty string).

**Expected Behavior**:

1. **No file written**: Do not create `.ctx.md` file.

2. **JSON result**:
   ```json
   {
     "assembly_name": "FakeSuite.dll",
     "ctx_path": "",
     "status": "error",
     "error": "decompiled_source is empty"
   }
   ```

---

## Test Case D: Assembly with Cross-Component References

**Inputs**: Use setup but with a non-empty `cross_component_relationships` list:

```json
{
  ...
  "cross_component_relationships": [
    "Referenced by FakeApi.dll — uses OrderManager",
    "References Consona.Data for ORM utilities"
  ]
}
```

**Expected Behavior**:

1. **Cross-Component References section**: PRESENT with two bullet points:
   - "Referenced by FakeApi.dll — uses OrderManager"
   - "References Consona.Data for ORM utilities"

2. All other sections remain as in Test Case A.

---

## Test Case E: Assembly with No SQL

**Inputs**: Use setup but with `db_tables: []` (empty list).

Example classifier_result:
```json
{
  "assembly": "ConfigModel.dll",
  "component": "main",
  "relevant": true,
  "primary_purpose": "Configuration model for the suite",
  "key_public_types": [
    { "name": "SuiteConfig", "description": "Root configuration object" }
  ],
  "db_tables": [],
  "cross_component_relationships": [],
  "review_needed": false
}
```

**Expected Behavior**:

1. **SQL / DB Usage section**: OMITTED entirely (no empty section)

2. **Public API section**: PRESENT (documents SuiteConfig)

3. All other sections as expected.

---

## Pass Criteria

### Required for all test cases:
- Output is valid JSON matching documented schema
- `assembly_name` field in JSON matches input
- `ctx_path` field is a non-empty string (or empty only when status is "error")
- `status` field is either "ok" or "error"

### Test Case A (Normal):
- All 7 front-matter fields present
- Heading uses correct assembly name
- Summary is non-empty prose (2+ sentences)
- Public API section documents OrderManager and IOrderRepository
- SQL / DB Usage section contains SELECT and INSERT queries for ordertable and orderline
- Cross-Component References section is ABSENT
- status: "ok"

### Test Case B (Irrelevant):
- All 7 front-matter fields present with relevant: false
- Summary is exactly one line: "Marked as not relevant to suite business logic."
- Public API, SQL, and Cross-Component sections are completely omitted
- status: "ok"

### Test Case C (Empty source):
- No file written
- status: "error"
- error field contains message about empty source

### Test Case D (With cross-component refs):
- Cross-Component References section PRESENT with all provided relationships as bullet points
- Rest of document matches Test Case A structure

### Test Case E (No SQL):
- SQL / DB Usage section is OMITTED
- Public API section is PRESENT
- All other sections present as needed

---

## Notes for Implementation

- The context-distiller should run without user interaction (unattended)
- Output path should be created if it does not exist
- File format should be markdown with YAML front-matter
- Date generation should use the current system date in YYYY-MM-DD format
- Type names extracted from `key_public_types` should preserve the order from classifier result
