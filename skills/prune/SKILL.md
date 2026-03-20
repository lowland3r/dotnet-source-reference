---
name: prune
description: Use when deleting non-retained binaries, PDB files, and irrelevant decompiled sources after review-drop has completed - retains only files marked "Stored in repo: Yes" in the index
argument-hint: "<profile>"
---

# prune

Delete non-reference files from component folders after decompilation and review. Retains only files marked "Stored in repo: Yes" in the index.

## Required inputs
- `<profile>`: path to suite profile JSON
- Index tables must exist (written by review-drop)

## Deletion rules

These file types are ALWAYS deleted (regardless of index):
- `.dll` files — binary assemblies (source is in `.decompiled.cs`)
- `.exe` files — compiled executables
- `.pdb` files — debug symbols

These files are deleted if their index row says `Stored in repo: No`:
- `.decompiled.cs` — decompiled source
- `.xml` — XML documentation files
- `.config`, `.exe.config` — configuration files

These files are NEVER deleted by prune (not covered by this command):
- Non-.NET files (e.g., `.pak`, `.dat`, `.bin`, native DLLs like `libcef.dll`)
  These should have been handled by pre-classify for known third-party patterns, or can be manually removed.

## Steps

### 1. Load inputs

Load the profile. Load all index tables from `<index_output_path>`. Build a map of filename → `Stored in repo` value.

### 2. Check git working tree

Run `git status --short` from the profile directory. If there are uncommitted changes, output a warning:
```
Warning: git working tree has uncommitted changes. Consider committing before pruning.
(Continuing — this is a warning, not a hard stop.)
```

### 3. Pre-flight conflict check

Before deleting anything, scan for potential conflicts:
- A `.decompiled.cs`, `.xml`, or `.config` file is present on disk and its index row says `Stored in repo: Yes`, BUT the file would be deleted by another rule.

This should not occur with normal pipeline execution. If it does: hard stop with:
```
ERROR: Prune conflict detected.
File '<filename>' is marked 'Stored in repo: Yes' in the index but the prune rules would delete it.
Resolve manually or re-run /review-drop to correct the index before pruning.
```

### 4. Delete files

Process each component folder. For each file:
- If extension is `.dll` or `.exe` or `.pdb` → delete
- If extension is `.decompiled.cs`, `.xml`, `.config` → check index; delete if `Stored in repo: No`
- Otherwise → leave untouched

### 5. Update manifest

Add "prune" to `completed_stages`.

### 6. Report summary

```
Prune complete.
  Deleted: N files
  Retained: N files

Retained files:
  <list of retained .decompiled.cs and config files>
```
