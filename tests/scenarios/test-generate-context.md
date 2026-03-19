# Test Scenario: generate-context

## Setup

Profile: `tests/fixtures/minimal-suite/test-profile.json`

Manifest (classification-manifest.json) has:
- `FakeSuite.dll` → `decompile_status: "success"`, `decompile_output: "tests/fixtures/minimal-suite/FakeSuite.decompiled.cs"`, and `classifier_results["FakeSuite.dll"]` exists with:
  - `relevant: true`
  - `key_public_types: [{ name: "OrderManager", description: "..." }, { name: "IOrderRepository", description: "..." }]`
  - `db_tables: ["ordertable", "orderline"]`
  - `primary_purpose: "Manages orders and their related business logic"`
  - `cross_component_relationships: []` (empty or null)
- `Newtonsoft.Json.dll` → `classification: "third_party"`, no `decompile_status`, no `classifier_results` entry (should be skipped — not in classifier_results)
- `completed_stages` includes `"review-drop"`

The decompiled source file exists and contains valid C# code.

No existing context output directory exists.

## Expected Behavior

1. `context-distiller` agent is dispatched **only** for `FakeSuite.dll` (not for Newtonsoft.Json.dll, which has no classifier_results entry)
2. A `.ctx.md` file is written at `tests/fixtures/minimal-suite/output/context/main/FakeSuite.ctx.md`
3. The manifest is updated:
   - `assemblies["FakeSuite.dll"].ctx_output` is set to the file path
   - `"generate-context"` is added to `completed_stages`
4. A summary is printed showing: Total attempted: 1, Succeeded: 1, Failed: 0
5. The generated `.ctx.md` file contains:
   - YAML front-matter with assembly, component, generated date, relevant: true, key_types: ["OrderManager", "IOrderRepository"], db_tables: ["ordertable", "orderline"]
   - `# FakeSuite` heading
   - `## Summary` section with prose description
   - `## Public API` section documenting the key types
   - `## SQL / DB Usage` section documenting SQL patterns for ordertable and orderline
   - No `## Cross-Component References` section (relationships list is empty)

## Test Case A: Normal run with one relevant assembly

Setup as above.

Expected behavior:
- Context-distiller dispatched for FakeSuite.dll
- Output file created with all sections
- Manifest updated with ctx_output path
- completed_stages includes "generate-context"
- Summary shows 1 attempted, 1 succeeded, 0 failed

Pass criteria:
- File exists at expected path
- File contains valid YAML front-matter and markdown sections
- Manifest ctx_output field points to correct path
- No error entries in manifest for this assembly

## Test Case B: Assembly marked as not relevant

Modify the classifier_result for FakeSuite.dll: set `relevant: false`

Expected behavior:
- Context-distiller is still dispatched (relevant and irrelevant assemblies are both processed)
- Output file is created with:
  - YAML front-matter with `relevant: false`
  - `# FakeSuite` heading
  - `## Summary` section containing only: "Marked as not relevant to suite business logic."
  - No other sections (no Public API, SQL/DB Usage, Cross-Component References)
- Manifest ctx_output is populated
- Summary shows 1 attempted, 1 succeeded, 0 failed

Pass criteria:
- File created for irrelevant assembly
- Summary section is minimal (single line)
- All other sections omitted
- ctx_output populated in manifest

## Test Case C: Missing classifier_results in manifest

Modify the manifest: remove the `classifier_results` key entirely (simulate review-drop never run).

Expected behavior:
- Command hard stops with message: "Run /review-drop before /generate-context."
- No context-distiller agents dispatched
- Manifest is not modified

Pass criteria:
- Hard stop occurs before any agent dispatch
- Error message clearly indicates review-drop is required

## Test Case D: Missing decompile_output file

Modify FakeSuite.dll entry: set `decompile_output` to a non-existent file path.

Expected behavior:
- No agent dispatched for FakeSuite.dll
- Manifest entry gets a `ctx_errors` field (string or array): "decompile_output file not found: <path>"
- Summary shows: Total attempted: 1, Succeeded: 0, Failed: 1
- Error listing includes assembly name and error message
- Manifest is updated with error entry

Pass criteria:
- Missing file is detected and logged, not silently ignored
- Command completes without abort
- Error appears in manifest and summary

## Test Case E: Multiple assemblies (mix of success and failure)

Setup with two assemblies:
- `FakeSuite.dll` → valid decompile_output, classifier_results exists
- `BadAssembly.dll` → decompile_output file missing, classifier_results exists

Expected behavior:
- Context-distiller dispatched for FakeSuite.dll only
- No agent for BadAssembly.dll (file missing)
- Manifest updated with:
  - FakeSuite.dll: ctx_output populated, no errors
  - BadAssembly.dll: ctx_errors populated with missing file message
- Summary shows: Total attempted: 2, Succeeded: 1, Failed: 1
- Error listing shows BadAssembly.dll and its message

Pass criteria:
- One assembly processed successfully while another fails
- Errors do not abort the run
- Summary and manifest both reflect mixed results
- ctx_output and ctx_errors fields are independently used

## General Pass Criteria

- Context-distiller not dispatched for assemblies without classifier_results entries
- Context-distiller not dispatched for assemblies without decompile_output files
- ctx_output field populated in manifest for each succeeded assembly
- ctx_errors field populated for each failed assembly
- `completed_stages` updated to include "generate-context" (once)
- Missing `classifier_results` key in manifest triggers hard stop
- Individual assembly failures logged, never abort whole run
- Summary report shows accurate attempt/success/failure counts
- Generated `.ctx.md` files follow the format specified in context-distiller.md
