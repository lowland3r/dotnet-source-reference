# Test Scenario: process-drop

## Setup

Profile: `tests/fixtures/minimal-suite/test-profile.json`
Source folder: `tests/fixtures/minimal-suite/`

The folder contains the same fixture as the minimal-suite:
- `FakeSuite.dll` — matches `FakeSuite*` (suite)
- `Newtonsoft.Json.dll` — matches `Newtonsoft.*` (third_party)
- Associated `.pdb` and `.xml` files for some assemblies
- Pre-written `FakeSuite.decompiled.cs` fixture file

No existing `classification-manifest.json` at the start of Test Case A (clean run).

## Test Case A: Clean run from scratch

### Setup

Remove any existing `classification-manifest.json` from the profile directory.

### Expected Behavior

1. process-drop starts and detects no existing manifest ("Starting process-drop from scratch.")
2. All 6 base stages run in order:
   - pre-classify: third-party file deleted, manifest written
   - decompile: FakeSuite.dll decompiled
   - review-drop: index file created, classifier_results populated
   - generate-context: .ctx.md files generated
   - prune: non-reference binaries deleted
   - detect-databases: database-context.json written
3. Each stage completes successfully and is marked in `completed_stages`
4. Final summary shows all 6 stages completed:
   ```
   process-drop complete.
     Stages completed: pre-classify, decompile, review-drop, generate-context, prune, detect-databases
     Stages skipped: (none)
     Output:
       Index: <index_output_path>
       Context: <context_output_path>
       DB detection: <profile dir>/database-context.json
   ```
5. `classification-manifest.json` exists with `completed_stages: ["pre-classify", "decompile", "review-drop", "generate-context", "prune", "detect-databases"]`
6. All expected output files exist:
   - Index tables (index-<component>.md)
   - Context files (.ctx.md)
   - database-context.json
   - classification-manifest.json

### Pass Criteria

- All 6 stages complete successfully
- Manifest `completed_stages` contains all 6 stages
- All output files are present and valid
- No error messages in output (except expected user prompts)

---

## Test Case B: Resume from partial progress

### Setup

Create a scenario where the manifest already has `completed_stages: ["pre-classify", "decompile"]`.

This simulates a user who ran process-drop earlier, completed classification and decompilation, but then stopped (or encountered an issue).

### Expected Behavior

1. process-drop starts and loads the existing manifest
2. Detects completed stages and prints:
   ```
   Resuming from existing manifest. Skipping: pre-classify, decompile
   ```
3. Skips pre-classify and decompile; runs review-drop, generate-context, prune, detect-databases in sequence
4. After each resumed stage completes, the manifest is updated
5. Final summary shows which stages were skipped and which were newly completed:
   ```
   process-drop complete.
     Stages completed: pre-classify, decompile, review-drop, generate-context, prune, detect-databases
     Stages skipped: pre-classify, decompile
     Output:
       Index: <index_output_path>
       Context: <context_output_path>
       DB detection: <profile dir>/database-context.json
   ```

### Pass Criteria

- pre-classify and decompile are not re-executed (no user prompts, no file scanning)
- resume message is printed
- Remaining stages run in correct order
- Final manifest contains all 6 stages in completed_stages

---

## Test Case C: --skip-prune flag

### Setup

Fresh run (no existing manifest). Invoke process-drop with `--skip-prune` flag.

### Expected Behavior

1. process-drop builds pipeline with prune explicitly skipped
2. Stages run in order: pre-classify, decompile, review-drop, generate-context, [skip prune], detect-databases
3. After prune would normally run, binaries (.dll, .exe, .pdb) remain on disk in the component folder
4. Decompiled source files (.decompiled.cs) are also retained (since prune stage didn't delete them)
5. Final summary shows:
   ```
   process-drop complete.
     Stages completed: pre-classify, decompile, review-drop, generate-context, detect-databases
     Stages skipped: prune
     Output:
       ...
   ```
6. Manifest does NOT include "prune" in completed_stages

### Pass Criteria

- prune stage is skipped
- Binary files remain on disk
- .decompiled.cs files remain on disk
- Final manifest does not include "prune"
- All other stages execute normally

---

## Test Case D: --dry-run flag

### Setup

Fresh run. Invoke process-drop with `--dry-run` flag.

### Expected Behavior

1. process-drop loads profile (if no manifest exists, that's OK — it just won't know what to skip)
2. Prints the stages that would be executed:
   ```
   --dry-run mode. Would execute:
     1. pre-classify
     2. decompile
     3. review-drop
     4. generate-context
     5. prune
     6. detect-databases
   ```
3. Exits without running anything
4. No commands executed (no pre-classify prompts, no decompile output, no files modified)
5. Manifest is NOT created or modified
6. No output files generated

### Pass Criteria

- Dry-run output printed
- No actual execution
- No manifest changes
- No output files created
- Exit cleanly

---

## Test Case E: --regenerate-indexes flag

### Setup

Fresh run. Invoke process-drop with `--regenerate-indexes` flag.

### Expected Behavior

1. Stages run in order: pre-classify, decompile, review-drop, generate-context, prune, detect-databases, generate-indexes
2. After detect-databases completes, generate-indexes runs as the final stage
3. generate-indexes rebuilds index tables from classifier_results
4. Final summary shows all 7 stages:
   ```
   process-drop complete.
     Stages completed: pre-classify, decompile, review-drop, generate-context, prune, detect-databases, generate-indexes
     Stages skipped: (none)
     Output:
       Index: <index_output_path>
       Context: <context_output_path>
       DB detection: <profile dir>/database-context.json
   ```
5. Manifest includes "generate-indexes" in completed_stages

### Pass Criteria

- generate-indexes runs after detect-databases
- generate-indexes completes successfully
- Manifest includes all 7 stages
- Index files are regenerated with correct content

---

## Test Case F: Stage failure and resume

### Setup

Simulate a failure at the generate-context stage (by providing invalid input or disrupting the agent). Invoke process-drop.

### Expected Behavior

1. Stages run: pre-classify, decompile, review-drop complete successfully
2. generate-context is invoked but fails (agent error or missing input)
3. process-drop catches the failure and prints:
   ```
   process-drop failed at stage: generate-context
     Error: <error message from generate-context>
     Resume: Fix the issue and run process-drop again with the same profile — it will resume from where it left off.
   ```
4. Manifest is updated with `completed_stages: ["pre-classify", "decompile", "review-drop"]`
5. Exit with failure status

Later, after the issue is fixed, user re-runs process-drop:

6. process-drop detects the partial manifest
7. Prints:
   ```
   Resuming from existing manifest. Skipping: pre-classify, decompile, review-drop
   ```
8. Resumes at generate-context and runs remaining stages successfully
9. Final summary shows all stages completed

### Pass Criteria

- Failure at a stage stops the pipeline
- Error message printed with context
- Resume advice given
- Manifest preserves partial progress
- Re-run resumes from the correct point

---

## Combined Test: Multiple flags

### Setup

Invoke process-drop with both `--skip-prune` and `--regenerate-indexes` flags.

### Expected Behavior

1. Pipeline includes generate-indexes (due to --regenerate-indexes)
2. Pipeline excludes prune (due to --skip-prune)
3. Stages run: pre-classify, decompile, review-drop, generate-context, detect-databases, generate-indexes
4. Binaries remain on disk (prune was skipped)
5. Final summary lists correct stages

### Pass Criteria

- Both flags are honored
- Correct stages run and correct stages skip
- No conflicts between flags
- All output files present (except those that would have been deleted by prune)

---

## Pass Criteria Summary

- **Test A**: All 6 base stages run in order; manifest contains all 6 stages
- **Test B**: Resume message shown; pre-classify and decompile skipped; remaining stages run
- **Test C**: --skip-prune honored; binaries remain on disk; manifest does not include prune
- **Test D**: --dry-run prints what would run; nothing actually executed; manifest unchanged
- **Test E**: --regenerate-indexes adds generate-indexes as final stage; index rebuilt
- **Test F**: Stage failure stops pipeline with error; resume works correctly after fix
- **Combined**: Multiple flags honored; correct stages run and skip; no conflicts
