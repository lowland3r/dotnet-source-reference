# generate-context

Generates `.ctx.md` context files for all relevant assemblies using the `context-distiller` agent. Each agent distills a decompiled assembly into a focused, LLM-optimized context document for downstream analysis or decision-making.

## Required inputs

- `<profile>`: path to suite profile JSON
- `classification-manifest.json` must exist in the profile directory and contain `classifier_results` (written by review-drop)

## Steps

### 1. Load inputs

Load the profile and `classification-manifest.json` from the profile directory.

If `classification-manifest.json` does not exist: hard stop with "Run /pre-classify and /review-drop before /generate-context."

If the manifest exists but contains no `classifier_results` key: hard stop with "Run /review-drop before /generate-context."

### 2. Collect assemblies to distill

From `assemblies[]` in the manifest: collect entries where:
- `decompile_status: "success"` AND
- `classifier_results[assembly_name]` exists in the manifest

This will include both relevant and irrelevant assemblies (context-distiller writes minimal files for irrelevant ones).

If no assemblies to process: output "Nothing to distill — no successfully classified assemblies." and exit cleanly.

### 3. Dispatch context-distiller for each assembly

For each assembly to distill, dispatch one `context-distiller` agent with:
- `assembly_name`: assembly filename (e.g., "FakeSuite.dll")
- `component`: the assembly's component (from manifest entry)
- `decompiled_source`: read the full text of the assembly's `decompile_output` file
- `classifier_result`: the JSON object from `manifest.classifier_results[assembly_name]`
- `profile`: the profile JSON

Dispatch all agents **in parallel** (do not wait for each to finish before starting the next).

Wait for all agents to complete and collect all JSON results.

**Error handling for individual assemblies**: If a `decompile_output` file does not exist for an assembly, log it as an error and skip that assembly (do not dispatch agent for it).

### 4. Update manifest

For each result returned from context-distiller:
- If `status: "ok"`: store the `ctx_path` value in the manifest entry under the `ctx_output` field
- If `status: "error"`: store the `error` value in a new `ctx_errors` field (array or string) for that assembly entry

Add `"generate-context"` to `completed_stages` (once, after all results are collected).

Write the updated manifest back to disk.

### 5. Report summary

```
Context generation complete.
  Total attempted: N
  Succeeded: N
  Failed: N
```

If any failed, list assembly names and error messages.

## Error handling

- Missing manifest → hard stop: "Run /pre-classify and /review-drop before /generate-context."
- Missing `classifier_results` in manifest → hard stop: "Run /review-drop before /generate-context."
- Individual assembly error (missing decompile_output, or agent returns error) → log to manifest, never abort whole run
- Agent error for an assembly → store error in `ctx_errors`, continue to next assembly

## Notes

- Context generation does not depend on the "prune" stage; it processes all successfully decompiled and classified assemblies
- Both relevant and irrelevant assemblies are processed; context-distiller handles the distinction
- The `.ctx.md` files are written by the context-distiller agent to the path specified in the profile under `context_output_path`
