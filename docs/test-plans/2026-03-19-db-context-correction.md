# Human Test Plan: db-context-correction

**Implementation plan:** `docs/implementation-plans/2026-03-19-db-context-correction/`
**Branch:** db-context-correction
**Base commit:** 8a18732
**HEAD:** 594b5d81ac57dda3bb8eac567946632017d5209a
**Generated:** 2026-03-19

---

## Prerequisites

- Working directory: project root (db-context-correction worktree)
- Branch checked out at commit `594b5d81` or later
- All fixture files present in `tests/fixtures/minimal-suite/` and `tests/fixtures/schema-fixture/`
- Familiarity with launching Claude subagents with markdown scenario files

---

## Phase 1: detect-databases Command Verification

| Step | Action | Expected |
|------|--------|----------|
| 1.1 | Create `classification-manifest.json` in `tests/fixtures/minimal-suite/` per Test Case A setup (single assembly `FakeSuite.dll`, `db_tables: ["ordertable", "orderline"]`). Run `/detect-databases` with profile `tests/fixtures/minimal-suite/test-profile.json`. | `database-context.json` is written to `tests/fixtures/minimal-suite/`. |
| 1.2 | Open `database-context.json` and verify it contains `"schema_version": "1.0"`, a `"generated_at"` timestamp, `"suite": "Fake Test Suite"`, a `"components_analyzed"` array, and a `"databases"` array. | All five top-level fields present. |
| 1.3 | Check `databases[0].tables[]` for entries named `ordertable` and `orderline`. | Both table names present. |
| 1.4 | Check `ordertable.access_patterns`. | Contains `"SELECT"` and `"INSERT"`. |
| 1.5 | Check `orderline.access_patterns`. | Empty array `[]` — the comment-only reference on line 67 of `FakeSuite.decompiled.cs` must not produce access patterns. |
| 1.6 | Check `databases[0].name`. | `"Fake Test Suite"` (fallback because `FakeSuite.decompiled.cs` has no connection string). |
| 1.7 | Clean up. Remove `database-context.json` and `classification-manifest.json`. Create manifest per Test Case B setup (single assembly `FakeSuiteWithConnString.dll`). Run `/detect-databases`. | `database-context.json` written. |
| 1.8 | Check `databases[0].name`. | `"FakeDB"` (extracted from `Initial Catalog=FakeDB` on line 11 of the fixture). |
| 1.9 | Check `statuslookup.probable_lookup`. | `true` (SELECT-only access + name ends with "lookup"). |
| 1.10 | Clean up. Create manifest per Test Case B2 setup (three assemblies all pointing to `FakeSuiteWithConnString.decompiled.cs`, each with `db_tables: ["ordertable"]`). Run `/detect-databases`. | `database-context.json` written. |
| 1.11 | Check `ordertable.probable_lookup` and `ordertable.referenced_in`. | `probable_lookup: true` and `referenced_in` contains 3 entries. |
| 1.12 | Clean up. Create manifest per Test Case C setup (single assembly `FakeSuiteDynamicSql.dll`). Run `/detect-databases`. | `database-context.json` written (dynamic SQL does not cause hard stop). |
| 1.13 | Check `databases[].unresolved_references`. | Contains entry with `"assembly": "FakeSuiteDynamicSql.dll"` and `"reason": "dynamic SQL"`. |
| 1.14 | Clean up. Create manifest per Test Case D setup (`classifier_results: {}`). Run `/detect-databases`. | Command halts with error. No `database-context.json` written. |
| 1.15 | Read the error output. | Contains reference to `/review-drop`. |
| 1.16 | Clean up. Ensure NO `classification-manifest.json` exists. Run `/detect-databases`. | Command halts with error. No `database-context.json` written. |
| 1.17 | Read the error output. | Contains references to both `/pre-classify` and `/review-drop`. |

---

## Phase 2: ingest-schema Command Verification

| Step | Action | Expected |
|------|--------|----------|
| 2.1 | Place `database-context.json` fixture in `tests/fixtures/minimal-suite/`. Create manifest with `completed_stages` including `"detect-databases"`. Place fresh un-enriched `FakeSuite.ctx.md` at `tests/fixtures/minimal-suite/output/context/FakeSuite.ctx.md`. Run `/ingest-schema` with profile and schema enrichment path `tests/fixtures/schema-fixture/schema-enrichment.json`. | Command completes. `FakeSuite.ctx.md` modified. |
| 2.2 | Open `output/context/FakeSuite.ctx.md`. | Contains `## DB Schema` section. Existing sections (`## Summary`, `## Public API`, `## SQL / DB Usage`, `## Cross-Component References`) are all still present and unmodified. |
| 2.3 | Check `### ordertable` subsection within `## DB Schema`. | Contains markdown table with headers `Column \| Type \| Nullable \| Notes` and rows for `fcorderid` (varchar(10), not nullable, PK), `fccustid` (varchar(10), not nullable, FK), `fcstatus` (char(2), not nullable, FK to statuslookup). |
| 2.4 | Check `### statuslookup` subsection. | Contains columns table (2 rows) plus `**Lookup values:**` with entries: OP/Open, CL/Closed, HD/On Hold. |
| 2.5 | Without resetting the file, run `/ingest-schema` again with the same arguments. | Command completes. |
| 2.6 | Count occurrences of `## DB Schema` in `FakeSuite.ctx.md`. | Exactly 1 (idempotent replace, not duplicate append). |
| 2.7 | Reset `FakeSuite.ctx.md` to un-enriched state. Edit frontmatter to `db_tables: [ordertable, unknowntable]`. Run `/ingest-schema`. | Command completes (no hard stop). |
| 2.8 | Check output summary and the enriched file. | Informational note about `unknowntable` not found in schema. File contains `### ordertable` but no `### unknowntable`. |
| 2.9 | Remove `database-context.json` from the fixture directory. Run `/ingest-schema`. | Hard stop. Error references `/detect-databases`. |
| 2.10 | Restore `database-context.json`. Run `/ingest-schema` with a non-existent schema enrichment path (e.g., `tests/fixtures/schema-fixture/nonexistent-schema.json`). | Hard stop. Error includes the attempted file path. |
| 2.11 | Create a copy of `schema-enrichment.json` with `schema_version: "2.0"`. Run `/ingest-schema` pointing to it. | Hard stop. Error mentions version mismatch. |
| 2.12 | Replace `database-context.json` with a version having `schema_version: "2.0"`. Run `/ingest-schema` with the correct schema enrichment path. | Hard stop. Error mentions version mismatch. Schema enrichment file is never loaded. |

---

## Phase 3: File Rename Consistency Verification

| Step | Action | Expected |
|------|--------|----------|
| 3.1 | Run `grep -r "db-detection.json" commands/ tests/scenarios/ tests/fixtures/` from the project root. | Zero matches (exit code 1). The old filename has been fully eliminated. |

---

## End-to-End: Full Pipeline (detect-databases → ingest-schema)

**Purpose:** Validate that the output of `detect-databases` is consumed correctly by `ingest-schema`, confirming the contract between the two commands.

| Step | Action | Expected |
|------|--------|----------|
| E2E.1 | Start clean. Create manifest per Test Case A (FakeSuite.dll, single assembly). Run `/detect-databases`. | `database-context.json` written with `databases[0].name: "Fake Test Suite"`, tables `ordertable` and `orderline`. |
| E2E.2 | Place un-enriched `FakeSuite.ctx.md` at `output/context/`. Supply `schema-enrichment.json`. Run `/ingest-schema`. | `FakeSuite.ctx.md` gains `## DB Schema` with column tables matching the schema enrichment data. |
| E2E.3 | Verify round-trip consistency: table names in `database-context.json` match table subsection headings in the enriched `.ctx.md`. | `ordertable` and `statuslookup` appear in both artifacts. `orderline` is in `database-context.json` but not in `db_tables` frontmatter, so it does not appear in `## DB Schema`. |

---

## Human Verification Notes

These aspects benefit from human review beyond automated checks:

| Aspect | Why Manual | Steps |
|--------|------------|-------|
| Error message clarity | Automated tests verify messages _reference_ specific commands but cannot judge whether they are clear to end users. | During steps 1.15, 1.17, and 2.9–2.12, read the full error message text and confirm it provides actionable guidance. |
| Markdown rendering quality | Automated tests verify structure but cannot judge visual rendering. | After step 2.4, open the enriched `FakeSuite.ctx.md` in a markdown previewer and confirm column tables and lookup values render correctly. |
| Comment exclusion edge cases | Test Case A covers `///` XML doc comments. Other comment styles (`//`, `/* */`) are not exercised by fixtures. | Manually inspect `commands/detect-databases.md` to confirm the comment exclusion logic covers all C# comment patterns. |

---

## Traceability

| Acceptance Criterion | Automated Test | Manual Step |
|----------------------|----------------|-------------|
| AC1.1 | test-detect-databases.md Test Case A | 1.2 |
| AC1.2 | Test Case A | 1.3 |
| AC1.3 | Test Case A | 1.4 |
| AC1.4 | Test Case A | 1.4 |
| AC1.5 | Test Case A | 1.5 |
| AC1.6 | Test Case B + Test Case B2 | 1.9, 1.11 |
| AC1.7 | Test Case C | 1.13 |
| AC1.8 | Test Case B | 1.8 |
| AC1.9 | Test Case A | 1.6 |
| AC1.10 | Test Case D | 1.14, 1.15 |
| AC1.11 | Test Case E | 1.16, 1.17 |
| AC2.1 | test-ingest-schema.md Test Case A | 2.2 |
| AC2.2 | Test Case A | 2.3 |
| AC2.3 | Test Case A | 2.4 |
| AC2.4 | Test Case B | 2.5, 2.6 |
| AC2.5 | Test Case C | 2.7, 2.8 |
| AC2.6 | Test Case D | 2.9 |
| AC2.7 | Test Case E | 2.10 |
| AC2.8 | Test Case F | 2.11 |
| AC2.9 | Test Case G | 2.12 |
| AC3.1 | grep (zero matches) | 3.1 |
| AC4.1 | Structural (Self-Verification section with 11 criteria) | — |
| AC4.2 | Structural (Self-Verification section with 9 criteria) | — |
