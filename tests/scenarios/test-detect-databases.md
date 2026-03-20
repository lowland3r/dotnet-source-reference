# Test Scenario: detect-databases

## Setup

Place the following files in the working directory before running any test case:

- Suite profile: `tests/fixtures/minimal-suite/test-profile.json`
- Fixture: `tests/fixtures/minimal-suite/FakeSuite.decompiled.cs`
- Fixture: `tests/fixtures/minimal-suite/FakeSuiteWithConnString.decompiled.cs`
- Fixture: `tests/fixtures/minimal-suite/FakeSuiteDynamicSql.decompiled.cs`
- Command specification: `commands/detect-databases.md`

Each test case defines its own `classification-manifest.json`. Place it in the same directory as the profile (`tests/fixtures/minimal-suite/`). After each test case, remove the manifest and any generated `database-context.json` before proceeding to the next test case.

## Test Case A: Access pattern detection and DB grouping fallback to suite_name

### Setup

Create `classification-manifest.json` in `tests/fixtures/minimal-suite/` with:

- `schema_version: "1.0"`
- `suite: "Fake Test Suite"`
- `completed_stages: ["pre-classify", "decompile", "review-drop"]`
- `assemblies[]` containing one entry for `FakeSuite.dll`:
  - `name: "FakeSuite.dll"`
  - `component: "main"`
  - `classification: "suite"`
  - `decompile_status: "success"`
  - `decompile_output: "tests/fixtures/minimal-suite/FakeSuite.decompiled.cs"`
- `classifier_results["FakeSuite.dll"]`:
  - `relevant: true`
  - `component: "main"`
  - `primary_purpose: "Order management"`
  - `db_tables: ["ordertable", "orderline"]`
  - `cross_component_relationships: []`

Run `detect-databases` with `tests/fixtures/minimal-suite/test-profile.json` as the profile argument, following the steps in `commands/detect-databases.md`.

### Expected Behavior

1. The command reads `FakeSuite.decompiled.cs` and scans it for SQL string literals
2. It detects `ordertable` in a SELECT literal (line 82: `"SELECT * FROM ordertable WHERE fcorderid = @id"`) and in an INSERT literal (line 92: `"INSERT INTO ordertable (fcorderid, fccustid, fcstatus) VALUES (@id, @cust, @status)"`)
3. It detects `orderline` only in the XML doc comment on line 67 (`/// ADO.NET data access for orders. Reads from ordertable and orderline.`) — not in any SQL string literal — and records no access patterns for it
4. No connection string literal (`Initial Catalog=`, `Database=`) is found in the file; all tables are grouped under a database named after the suite: `"Fake Test Suite"`
5. `database-context.json` is written to `tests/fixtures/minimal-suite/`
6. The manifest gains `"detect-databases"` in `completed_stages`

### Pass Criteria

- `database-context.json` contains `"schema_version": "1.0"` — verifies AC1.1
- `database-context.json` contains `"generated_at"`, `"suite"`, `"components_analyzed"`, and `"databases"` fields — verifies AC1.1
- `ordertable` appears in `databases[].tables[]` — verifies AC1.2
- `orderline` appears in `databases[].tables[]` — verifies AC1.2
- `ordertable.access_patterns` contains `"SELECT"` — verifies AC1.3
- `ordertable.access_patterns` contains `"INSERT"` — verifies AC1.4
- `orderline.access_patterns` is `[]` (empty — comment-only reference produces no patterns) — verifies AC1.5
- `databases[0].name` is `"Fake Test Suite"` (suite_name from profile, because no connection string was found) — verifies AC1.9

## Test Case B: Connection string DB name inference and probable lookup detection

### Setup

Create `classification-manifest.json` in `tests/fixtures/minimal-suite/` with:

- `schema_version: "1.0"`
- `suite: "Fake Test Suite"`
- `completed_stages: ["pre-classify", "decompile", "review-drop"]`
- `assemblies[]` containing one entry for `FakeSuiteWithConnString.dll`:
  - `name: "FakeSuiteWithConnString.dll"`
  - `component: "main"`
  - `classification: "suite"`
  - `decompile_status: "success"`
  - `decompile_output: "tests/fixtures/minimal-suite/FakeSuiteWithConnString.decompiled.cs"`
- `classifier_results["FakeSuiteWithConnString.dll"]`:
  - `relevant: true`
  - `component: "main"`
  - `db_tables: ["ordertable", "statuslookup"]`

Run `detect-databases` with `tests/fixtures/minimal-suite/test-profile.json`.

### Expected Behavior

1. The command scans `FakeSuiteWithConnString.decompiled.cs`
2. It finds `"Data Source=fake-server;Initial Catalog=FakeDB;Integrated Security=True"` — a connection string literal containing `Initial Catalog=FakeDB` — and infers database name `"FakeDB"`
3. `ordertable` is found in a SELECT literal (`"SELECT * FROM ordertable WHERE id = @id"`) — `access_patterns: ["SELECT"]`
4. `statuslookup` is found in a SELECT literal (`"SELECT code, description FROM statuslookup"`) — `access_patterns: ["SELECT"]`
5. `statuslookup` has SELECT-only access and its name ends with the suffix `"lookup"` → `probable_lookup: true`
6. `database-context.json` is written with `databases[0].name` equal to `"FakeDB"`

### Pass Criteria

- `databases[0].name` is `"FakeDB"` (inferred from `Initial Catalog=FakeDB` in the decompiled source) — verifies AC1.8
- `statuslookup.probable_lookup` is `true` (SELECT-only access and name ends with "lookup" suffix) — verifies AC1.6 (suffix sub-path)

## Test Case B2: Probable lookup detection via 3+ referencing assemblies

### Setup

Create `classification-manifest.json` in `tests/fixtures/minimal-suite/` with:

- `schema_version: "1.0"`
- `suite: "Fake Test Suite"`
- `completed_stages: ["pre-classify", "decompile", "review-drop"]`
- `assemblies[]` containing three entries, all pointing to the same decompiled file:
  - Entry 1: `name: "FakeSuiteA.dll"`, `component: "main"`, `classification: "suite"`, `decompile_status: "success"`, `decompile_output: "tests/fixtures/minimal-suite/FakeSuiteWithConnString.decompiled.cs"`
  - Entry 2: `name: "FakeSuiteB.dll"`, `component: "main"`, `classification: "suite"`, `decompile_status: "success"`, `decompile_output: "tests/fixtures/minimal-suite/FakeSuiteWithConnString.decompiled.cs"`
  - Entry 3: `name: "FakeSuiteC.dll"`, `component: "main"`, `classification: "suite"`, `decompile_status: "success"`, `decompile_output: "tests/fixtures/minimal-suite/FakeSuiteWithConnString.decompiled.cs"`
- `classifier_results["FakeSuiteA.dll"]`: `relevant: true`, `component: "main"`, `db_tables: ["ordertable"]`
- `classifier_results["FakeSuiteB.dll"]`: `relevant: true`, `component: "main"`, `db_tables: ["ordertable"]`
- `classifier_results["FakeSuiteC.dll"]`: `relevant: true`, `component: "main"`, `db_tables: ["ordertable"]`

Run `detect-databases` with `tests/fixtures/minimal-suite/test-profile.json`.

### Expected Behavior

1. The command scans `FakeSuiteWithConnString.decompiled.cs` for each of the three assemblies
2. `ordertable` is found only in SELECT literals in that file — no INSERT, UPDATE, or DELETE — so combined `access_patterns: ["SELECT"]`
3. `ordertable` does NOT match any lookup suffix (its name is `"ordertable"`)
4. `ordertable.referenced_in` contains all three assembly names (FakeSuiteA.dll, FakeSuiteB.dll, FakeSuiteC.dll) — 3+ assemblies
5. Because access is SELECT-only AND the assembly count is 3+: `probable_lookup: true`

### Pass Criteria

- `ordertable.probable_lookup` is `true` (SELECT-only access referenced by 3 assemblies, no lookup suffix — the assembly count sub-path) — verifies AC1.6 (3+ assembly sub-path)
- `ordertable.referenced_in` contains exactly 3 entries — verifies the assembly count basis for the flag

## Test Case C: Dynamic SQL detection

### Setup

Create `classification-manifest.json` in `tests/fixtures/minimal-suite/` with:

- `schema_version: "1.0"`
- `suite: "Fake Test Suite"`
- `completed_stages: ["pre-classify", "decompile", "review-drop"]`
- `assemblies[]` containing one entry for `FakeSuiteDynamicSql.dll`:
  - `name: "FakeSuiteDynamicSql.dll"`
  - `component: "main"`
  - `classification: "suite"`
  - `decompile_status: "success"`
  - `decompile_output: "tests/fixtures/minimal-suite/FakeSuiteDynamicSql.decompiled.cs"`
- `classifier_results["FakeSuiteDynamicSql.dll"]`:
  - `relevant: true`
  - `component: "main"`
  - `db_tables: ["ordertable"]`

Run `detect-databases` with `tests/fixtures/minimal-suite/test-profile.json`.

### Expected Behavior

1. The command scans `FakeSuiteDynamicSql.decompiled.cs`
2. It finds the pattern `"SELECT * FROM " + tableName` — a SQL string literal (`"SELECT * FROM "`) concatenated with a non-string operand (`tableName`) via the `+` operator
3. `FakeSuiteDynamicSql.dll` is added to `unresolved_references` with `reason: "dynamic SQL"`
4. The command completes and writes `database-context.json` — dynamic SQL detection does not cause a hard stop

### Pass Criteria

- `database-context.json` is written (dynamic SQL does not prevent output) — verifies AC1.7 precondition
- `databases[].unresolved_references` contains an entry with `"assembly": "FakeSuiteDynamicSql.dll"` and `"reason": "dynamic SQL"` — verifies AC1.7

## Test Case D: Missing or empty classifier_results hard stop

### Setup

Create `classification-manifest.json` in `tests/fixtures/minimal-suite/` with:

- `schema_version: "1.0"`
- `suite: "Fake Test Suite"`
- `completed_stages: ["pre-classify", "decompile", "review-drop"]`
- `assemblies[]`: empty array
- `classifier_results: {}` (empty object — no assembly entries)

Run `detect-databases` with `tests/fixtures/minimal-suite/test-profile.json`.

### Expected Behavior

1. The command detects that `classifier_results` is an empty object
2. It halts with an error message referencing `/review-drop`
3. No `database-context.json` is written

### Pass Criteria

- The command halts before producing any output — verifies AC1.10
- The error message contains a reference to `/review-drop` — verifies AC1.10

## Test Case E: Missing manifest hard stop

### Setup

Ensure no `classification-manifest.json` exists in `tests/fixtures/minimal-suite/`. (Do not create one.)

Run `detect-databases` with `tests/fixtures/minimal-suite/test-profile.json`.

### Expected Behavior

1. The command cannot find `classification-manifest.json` in the profile directory
2. It halts with an error message referencing `/pre-classify` and `/review-drop`
3. No `database-context.json` is written

### Pass Criteria

- The command halts before producing any output — verifies AC1.11
- The error message contains a reference to `/pre-classify` — verifies AC1.11
- The error message contains a reference to `/review-drop` — verifies AC1.11

## Self-Verification

After completing all test cases above, evaluate each criterion and output a PASS/FAIL verdict using this exact format:

```
PASS: db-context-correction.AC1.1 — <criterion text>
FAIL: db-context-correction.AC1.3 — <criterion text> — Reason: <brief explanation>
```

Criteria to evaluate (one line each):

- db-context-correction.AC1.1: Output contains `schema_version: "1.0"`, `generated_at`, `suite`, `components_analyzed`, `databases[]` — verified by Test Case A
- db-context-correction.AC1.2: Tables from `classifier_results` appear in `database-context.json` — verified by Test Case A
- db-context-correction.AC1.3: SELECT access pattern detected from SQL literal on `ordertable` in `FakeSuite.decompiled.cs` — verified by Test Case A
- db-context-correction.AC1.4: INSERT access pattern detected from SQL literal on `ordertable` in `FakeSuite.decompiled.cs` — verified by Test Case A
- db-context-correction.AC1.5: Table name appearing only in a comment does not produce `access_patterns` — verified by Test Case A (`orderline`)
- db-context-correction.AC1.6: `probable_lookup: true` set for SELECT-only access — suffix sub-path verified by Test Case B (`statuslookup`); 3+ assembly sub-path verified by Test Case B2 (`ordertable` with 3 assemblies)
- db-context-correction.AC1.7: String concatenation in SQL context produces `unresolved_references` entry with reason `"dynamic SQL"` — verified by Test Case C
- db-context-correction.AC1.8: Connection string with `Initial Catalog=X` → `databases[].name` is X — verified by Test Case B (`FakeDB`)
- db-context-correction.AC1.9: No detectable connection string → tables grouped under suite_name — verified by Test Case A (`"Fake Test Suite"`)
- db-context-correction.AC1.10: Missing or empty `classifier_results` → hard stop with message referencing `/review-drop` — verified by Test Case D
- db-context-correction.AC1.11: Missing manifest → hard stop with message referencing `/pre-classify` and `/review-drop` — verified by Test Case E

After evaluating all criteria, output a summary line:

```
OVERALL: PASS (11/11)
```

or, if any criteria failed:

```
OVERALL: FAIL (N/11 passed) — failing criteria: <comma-separated list of criterion IDs>
```
