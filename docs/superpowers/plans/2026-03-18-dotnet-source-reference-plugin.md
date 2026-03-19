# dotnet-source-reference Plugin Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a generic ed3d Claude Code plugin that decompiles .NET assemblies, classifies them by relevance, and generates LLM-optimized context files (primary output) and optional human-readable index tables.

**Architecture:** The plugin is composed entirely of markdown instruction files (commands, agents, skills) and JSON config profiles. No code is compiled or executed by the plugin itself — it orchestrates Claude agents that read these instructions. All suite-specific details live in config profiles; commands/agents/skills use only generic .NET terminology. Testing uses subagent scenario files run against a minimal-suite fixture.

**Tech Stack:** ed3d plugin format (markdown), ilspycmd (.NET CLI decompiler), JSON (manifests/profiles), Markdown (output artifacts)

**Spec:** `docs/superpowers/specs/2026-03-17-dotnet-source-reference-plugin-design.md`

**Plugin repo location:** `C:\Users\jake.wimmer\Repositories\dotnet-source-reference\`
(Create as a new git repository separate from m2m-source-reference)

---

## File Map

All paths relative to `dotnet-source-reference/`:

| File | Responsibility |
|---|---|
| `plugin.json` | ed3d plugin manifest — declares all commands, agents, skills |
| `config/schema.json` | JSON schema for suite config profiles |
| `config/profiles/m2m.json` | M2M-specific profile (only suite-specific file in the plugin) |
| `skills/ilspy-runner.md` | Shared ILSpy invocation logic (flags, paths, error capture) |
| `commands/bootstrap-ilspy.md` | Check/install ilspycmd via dotnet tool |
| `commands/pre-classify.md` | Classify DLLs, prune third-party immediately, write manifest |
| `commands/decompile.md` | Orchestrate decompilation (parallel if over threshold) |
| `agents/decompile-batch.md` | Decompile a batch of assemblies, return results |
| `agents/assembly-classifier.md` | Assess relevance of a decompiled assembly |
| `commands/review-drop.md` | Run assembly-classifier on all assemblies, update index rows |
| `commands/prune.md` | Delete binaries/PDBs for non-retained assemblies |
| `agents/context-distiller.md` | Produce LLM-optimized .ctx.md for one assembly |
| `commands/generate-context.md` | Run context-distiller on all relevant assemblies |
| `commands/generate-indexes.md` | Enrich index tables with types/methods/cross-component section |
| `commands/detect-databases.md` | Analyze source for DB patterns, write database-context.json |
| `commands/ingest-schema.md` | Consume schema-enrichment.json, enrich indexes + ctx files |
| `commands/process-drop.md` | Orchestrate all stages, manage CHANGELOG |
| `tests/fixtures/minimal-suite/test-profile.json` | Test config profile |
| `tests/fixtures/minimal-suite/FakeSuite.decompiled.cs` | Stub decompiled source for suite assembly |
| `tests/fixtures/minimal-suite/README.md` | Documents the fixture structure |
| `tests/fixtures/schema-fixture/schema-enrichment.json` | Minimal schema enrichment for ingest-schema tests |
| `tests/scenarios/` | One scenario file per command/agent — subagent test instructions |
| `.gitignore` | Ignore classification-manifest.json, generation-errors.md |

---

## Phase 1: Repository Setup

### Task 1: Initialize repository and plugin manifest

**Files:**
- Create: `dotnet-source-reference/` (new git repo)
- Create: `.gitignore`
- Create: `plugin.json`

- [ ] **Step 1: Create the plugin repository**

```bash
cd C:\Users\jake.wimmer\Repositories
mkdir dotnet-source-reference
cd dotnet-source-reference
git init
```

- [ ] **Step 2: Create `.gitignore`**

Create `dotnet-source-reference/.gitignore`:

```
# Ephemeral pipeline artifacts
classification-manifest.json
generation-errors.md

# OS
.DS_Store
Thumbs.db
```

- [ ] **Step 3: Create `plugin.json`**

Create `dotnet-source-reference/plugin.json`:

```json
{
  "name": "dotnet-source-reference",
  "version": "0.1.0",
  "description": "Decompile .NET assemblies and generate LLM-optimized reference documentation for customization and integration development.",
  "commands": [
    { "name": "bootstrap-ilspy",   "description": "Check and install ilspycmd decompiler" },
    { "name": "pre-classify",      "description": "Classify assemblies and prune third-party binaries" },
    { "name": "decompile",         "description": "Decompile passing assemblies to C# source" },
    { "name": "review-drop",       "description": "Assess relevance of decompiled assemblies and update indexes" },
    { "name": "prune",             "description": "Delete non-retained binaries and PDB files" },
    { "name": "generate-context",  "description": "Generate LLM-optimized .ctx.md files per assembly" },
    { "name": "generate-indexes",  "description": "[Optional] Generate enriched human-readable index tables" },
    { "name": "detect-databases",  "description": "Detect database usage patterns and write database-context.json" },
    { "name": "ingest-schema",     "description": "Enrich context files using schema-enrichment.json from schema plugin" },
    { "name": "process-drop",      "description": "Orchestrate all pipeline stages for a new binary drop" }
  ],
  "agents": [
    { "name": "assembly-classifier", "description": "Assess relevance of a decompiled .NET assembly" },
    { "name": "context-distiller",   "description": "Produce LLM-optimized context documentation for an assembly" },
    { "name": "decompile-batch",     "description": "Decompile a batch of assemblies using ilspycmd" }
  ],
  "skills": [
    { "name": "ilspy-runner", "description": "Shared ILSpy CLI invocation logic" }
  ]
}
```

- [ ] **Step 4: Commit**

```bash
git add .gitignore plugin.json
git commit -m "feat: initialize plugin repository with manifest"
```

---

### Task 2: Config schema and directory structure

**Files:**
- Create: `config/schema.json`
- Create: `commands/`, `agents/`, `skills/`, `tests/fixtures/`, `tests/scenarios/` directories

- [ ] **Step 1: Create directory structure**

```bash
mkdir -p config/profiles commands agents skills tests/fixtures/minimal-suite tests/fixtures/schema-fixture tests/scenarios
```

- [ ] **Step 2: Create `config/schema.json`**

Create `dotnet-source-reference/config/schema.json`:

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "dotnet-source-reference Suite Profile",
  "type": "object",
  "required": ["suite_name", "components", "known_suite_patterns", "known_third_party_patterns", "index_output_path", "context_output_path"],
  "properties": {
    "suite_name": {
      "type": "string",
      "description": "Human-readable name of the software suite"
    },
    "components": {
      "type": "array",
      "description": "Source component folders to process together",
      "items": {
        "type": "object",
        "required": ["name", "path"],
        "properties": {
          "name": { "type": "string" },
          "path": { "type": "string", "description": "Path relative to the directory containing this profile file" }
        }
      }
    },
    "known_suite_patterns": {
      "type": "array",
      "items": { "type": "string" },
      "description": "Glob patterns matching suite-owned assembly names (e.g. 'MyApp.*')"
    },
    "known_third_party_patterns": {
      "type": "array",
      "items": { "type": "string" },
      "description": "Glob patterns matching known third-party assembly names (e.g. 'Newtonsoft.*')"
    },
    "unknown_default": {
      "type": "string",
      "enum": ["decompile", "skip"],
      "default": "decompile",
      "description": "Default action for assemblies that match neither suite nor third-party patterns"
    },
    "decompile_parallel_threshold": {
      "type": "integer",
      "default": 10,
      "description": "Number of passing assemblies above which decompilation splits into parallel batches"
    },
    "index_output_path": {
      "type": "string",
      "description": "Path (relative to profile directory) where index-<component>.md files are written"
    },
    "context_output_path": {
      "type": "string",
      "description": "Path (relative to profile directory) where <component>/<assembly>.ctx.md files are written"
    },
    "version_prompt": {
      "type": "string",
      "description": "Question to ask the user for the version/release label when running process-drop"
    }
  }
}
```

- [ ] **Step 3: Commit**

```bash
git add config/
git commit -m "feat: add config schema and directory structure"
```

---

## Phase 2: Test Fixtures

### Task 3: Create minimal-suite fixture

The minimal-suite fixture has three assemblies: one suite-owned, one third-party, one unknown. It also includes a pre-decompiled `.cs` file so decompilation can be skipped in tests that don't need it.

**Files:**
- Create: `tests/fixtures/minimal-suite/test-profile.json`
- Create: `tests/fixtures/minimal-suite/FakeSuite.decompiled.cs`
- Create: `tests/fixtures/minimal-suite/README.md`
- Create: `tests/fixtures/minimal-suite/FakeSuite.dll` (zero-byte stub)
- Create: `tests/fixtures/minimal-suite/Newtonsoft.Json.dll` (zero-byte stub)
- Create: `tests/fixtures/minimal-suite/Unknown.Library.dll` (zero-byte stub)

- [ ] **Step 1: Create stub DLL files**

These are zero-byte files — they exist only so pre-classify can see them by filename. ilspycmd will fail on them (expected — handled by error handling).

```bash
cd tests/fixtures/minimal-suite
New-Item FakeSuite.dll -ItemType File
New-Item Newtonsoft.Json.dll -ItemType File
New-Item Unknown.Library.dll -ItemType File
```

- [ ] **Step 2: Create `test-profile.json`**

Create `tests/fixtures/minimal-suite/test-profile.json`:

```json
{
  "suite_name": "Fake Test Suite",
  "components": [
    { "name": "main", "path": "." }
  ],
  "known_suite_patterns": ["FakeSuite*", "FakeCore*"],
  "known_third_party_patterns": ["Newtonsoft.*", "Microsoft.*", "System.*"],
  "unknown_default": "decompile",
  "decompile_parallel_threshold": 10,
  "index_output_path": "output/reference",
  "context_output_path": "output/context",
  "version_prompt": "What test version is this?"
}
```

- [ ] **Step 3: Create `FakeSuite.decompiled.cs`**

Create `tests/fixtures/minimal-suite/FakeSuite.decompiled.cs`:

```csharp
// Decompiled with ilspycmd
// FakeSuite v1.0.0

using System;
using System.Collections.Generic;

namespace FakeSuite.Orders
{
    /// <summary>
    /// Manages order lifecycle for the Fake Suite application.
    /// </summary>
    public class OrderManager
    {
        private readonly IOrderRepository _repository;

        public OrderManager(IOrderRepository repository)
        {
            _repository = repository;
        }

        /// <summary>
        /// Retrieves an order by its identifier.
        /// </summary>
        public Order GetOrder(string orderId)
        {
            return _repository.FindById(orderId);
        }

        /// <summary>
        /// Creates a new order and persists it.
        /// </summary>
        public Order CreateOrder(string customerId, IEnumerable<OrderLine> lines)
        {
            var order = new Order { CustomerId = customerId, Lines = new List<OrderLine>(lines) };
            _repository.Save(order);
            return order;
        }
    }

    public interface IOrderRepository
    {
        Order FindById(string id);
        void Save(Order order);
    }

    public class Order
    {
        public string Id { get; set; }
        public string CustomerId { get; set; }
        public List<OrderLine> Lines { get; set; }
        public string Status { get; set; }
    }

    public class OrderLine
    {
        public string ItemId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }
}

namespace FakeSuite.Data
{
    using System.Data.SqlClient;

    /// <summary>
    /// ADO.NET data access for orders. Reads from ordertable and orderline.
    /// </summary>
    public class SqlOrderRepository : FakeSuite.Orders.IOrderRepository
    {
        private readonly string _connectionString;

        public SqlOrderRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public FakeSuite.Orders.Order FindById(string id)
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();
            var cmd = new SqlCommand("SELECT * FROM ordertable WHERE fcorderid = @id", conn);
            cmd.Parameters.AddWithValue("@id", id);
            // ... mapping omitted for brevity
            return null;
        }

        public void Save(FakeSuite.Orders.Order order)
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();
            var cmd = new SqlCommand("INSERT INTO ordertable (fcorderid, fccustid, fcstatus) VALUES (@id, @cust, @status)", conn);
            cmd.Parameters.AddWithValue("@id", order.Id);
            cmd.Parameters.AddWithValue("@cust", order.CustomerId);
            cmd.Parameters.AddWithValue("@status", order.Status ?? "OP");
            cmd.ExecuteNonQuery();
        }
    }
}
```

- [ ] **Step 4: Create `README.md`**

Create `tests/fixtures/minimal-suite/README.md`:

```markdown
# Minimal Suite Fixture

Test fixture for the dotnet-source-reference plugin pipeline.

## Contents

| File | Classification | Purpose |
|---|---|---|
| `FakeSuite.dll` | suite | Matches `FakeSuite*` pattern in test-profile |
| `Newtonsoft.Json.dll` | third_party | Matches `Newtonsoft.*` pattern |
| `Unknown.Library.dll` | unknown | Matches neither pattern |
| `FakeSuite.decompiled.cs` | — | Pre-decompiled source; used by stages that don't need real decompilation |
| `test-profile.json` | — | Suite profile for this fixture |

## Usage

Pass `tests/fixtures/minimal-suite/test-profile.json` as the `<profile>` argument when testing any command against this fixture.
```

- [ ] **Step 5: Commit**

```bash
git add tests/
git commit -m "test: add minimal-suite fixture and test profile"
```

---

### Task 4: Create schema fixture

**Files:**
- Create: `tests/fixtures/schema-fixture/schema-enrichment.json`

- [ ] **Step 1: Create `schema-enrichment.json`**

Create `tests/fixtures/schema-fixture/schema-enrichment.json`:

```json
{
  "schema_version": "1.0",
  "generated_at": "2026-03-18T00:00:00Z",
  "suite": "Fake Test Suite",
  "databases": [
    {
      "name": "FakeDB",
      "tables": [
        {
          "name": "ordertable",
          "columns": [
            { "name": "fcorderid", "type": "varchar(10)", "nullable": false, "notes": "PK — order identifier" },
            { "name": "fccustid",  "type": "varchar(10)", "nullable": false, "notes": "FK → custtable.fccustid" },
            { "name": "fcstatus",  "type": "char(2)",     "nullable": false, "notes": "FK → statuslookup.fcstatus" }
          ],
          "foreign_keys": [
            { "column": "fcstatus", "references_table": "statuslookup", "references_column": "fcstatus" }
          ],
          "is_lookup": false
        },
        {
          "name": "statuslookup",
          "columns": [
            { "name": "fcstatus",      "type": "char(2)",     "nullable": false, "notes": "PK" },
            { "name": "fcdescription", "type": "varchar(50)", "nullable": true,  "notes": "Display label" }
          ],
          "foreign_keys": [],
          "is_lookup": true,
          "lookup_values": [
            { "fcstatus": "OP", "fcdescription": "Open" },
            { "fcstatus": "CL", "fcdescription": "Closed" },
            { "fcstatus": "HD", "fcdescription": "On Hold" }
          ]
        }
      ]
    }
  ]
}
```

- [ ] **Step 2: Commit**

```bash
git add tests/fixtures/schema-fixture/
git commit -m "test: add schema-enrichment fixture for ingest-schema testing"
```

---

## Phase 3: ILSpy Foundation

### Task 5: ilspy-runner skill

**Files:**
- Create: `skills/ilspy-runner.md`
- Create: `tests/scenarios/test-bootstrap-ilspy.md`

- [ ] **Step 1: Write the test scenario first**

Create `tests/scenarios/test-bootstrap-ilspy.md`:

```markdown
# Test Scenario: bootstrap-ilspy

## Setup
You are testing the `bootstrap-ilspy` command. The command's instruction file is at `commands/bootstrap-ilspy.md`.

## Test Case A: ilspycmd already installed
Simulate that `ilspycmd` is already available on PATH by pretending `dotnet tool list -g` returns a line containing `ilspycmd`.

Expected behavior:
- Command reports: "ilspycmd is already installed" (or similar confirmation)
- Command does NOT attempt to run `dotnet tool install`
- Command exits successfully

## Test Case B: ilspycmd not installed
Simulate that `dotnet tool list -g` returns output with no mention of ilspycmd.

Expected behavior:
- Command runs: `dotnet tool install -g ilspycmd`
- If install succeeds: reports "ilspycmd installed successfully"
- If `dotnet` is not on PATH: hard stop with message "dotnet SDK is required. Install from https://dot.net and re-run."
- If install fails for any other reason: surface the exact error from `dotnet tool install` verbatim and halt

## Pass Criteria
- Correct detection of installed vs. not-installed state
- No install attempt when already installed
- Correct hard-stop message when dotnet SDK missing
- Verbatim error surfacing when install fails
```

- [ ] **Step 2: Write `skills/ilspy-runner.md`**

Create `dotnet-source-reference/skills/ilspy-runner.md`:

```markdown
# ilspy-runner

Shared skill for invoking the ilspycmd decompiler. Use this skill whenever you need to run ilspycmd.

## Invocation Pattern

To decompile a single assembly to a single `.decompiled.cs` file, use the `-o` flag with an explicit output file path (NOT a directory). This produces one flat `.cs` file:

```bash
ilspycmd "<assembly-path>" -o "<output-path>.decompiled.cs"
```

For a DLL at `source/MyAssembly.dll`, the command is:
```bash
ilspycmd "source/MyAssembly.dll" -o "source/MyAssembly.decompiled.cs"
```

This writes exactly `source/MyAssembly.decompiled.cs` as a single file.

**Do NOT use `--outputdir`** — that flag writes a directory tree of per-namespace `.cs` files, which breaks the single-file path assumptions used throughout the pipeline.

The output file name convention is always: `<AssemblyNameWithoutExtension>.decompiled.cs` in the same directory as the source assembly.

## Checking if ilspycmd is Installed

```bash
dotnet tool list -g
```

If the output contains a line with `ilspycmd`, it is installed. Otherwise it is not.

## Error Capture

Always capture stderr alongside stdout. A non-zero exit code from ilspycmd indicates failure. Capture the full output and include it in any error report.

Common failure modes:
- "Unable to find assembly" — the path is wrong or the file is not a valid .NET assembly
- "Not a valid PE file" — the file is not a .NET assembly (may be a native DLL, data file, etc.)
- Truncated output / partial `.decompiled.cs` — treat as failure; delete partial output

## Output Validation

After running ilspycmd, verify the output file:
1. Exists at the expected path
2. Is non-empty (size > 0)
3. Contains at least one `namespace` or `class` declaration

If any check fails, treat as a decompile failure.
```

- [ ] **Step 3: Write `commands/bootstrap-ilspy.md`**

Create `dotnet-source-reference/commands/bootstrap-ilspy.md`:

```markdown
# bootstrap-ilspy

Check whether ilspycmd is installed and install it if not.

## Steps

1. Run the following command and capture its output:
   ```
   dotnet tool list -g
   ```

2. **If `dotnet` is not found on PATH:**
   Hard stop. Output exactly:
   ```
   ERROR: dotnet SDK is required to use this plugin.
   Install the .NET SDK from https://dot.net, then re-run /bootstrap-ilspy.
   ```

3. **If output contains a line with `ilspycmd`:**
   Output: "✓ ilspycmd is already installed."
   Done — no further action needed.

4. **If output does not contain `ilspycmd`:**
   Run:
   ```
   dotnet tool install -g ilspycmd
   ```
   Capture all output (stdout + stderr).

5. **If the install command exits with code 0:**
   Output: "✓ ilspycmd installed successfully."

6. **If the install command exits with a non-zero code:**
   Hard stop. Output:
   ```
   ERROR: Failed to install ilspycmd. dotnet tool install output:
   <paste full captured output here verbatim>

   Resolve the error above and re-run /bootstrap-ilspy.
   ```
```

- [ ] **Step 4: Run test scenario to verify**

Dispatch a subagent with `tests/scenarios/test-bootstrap-ilspy.md` as instructions and `commands/bootstrap-ilspy.md` as context. Verify both test cases produce the expected behavior.

- [ ] **Step 5: Commit**

```bash
git add skills/ilspy-runner.md commands/bootstrap-ilspy.md tests/scenarios/test-bootstrap-ilspy.md
git commit -m "feat: add ilspy-runner skill and bootstrap-ilspy command"
```

---

## Phase 4: Pre-Classification

### Task 6: pre-classify command

**Files:**
- Create: `commands/pre-classify.md`
- Create: `tests/scenarios/test-pre-classify.md`

- [ ] **Step 1: Write test scenario**

Create `tests/scenarios/test-pre-classify.md`:

```markdown
# Test Scenario: pre-classify

## Setup
Profile: `tests/fixtures/minimal-suite/test-profile.json`
Source folder: `tests/fixtures/minimal-suite/`

The folder contains:
- `FakeSuite.dll` — matches `FakeSuite*` (suite)
- `Newtonsoft.Json.dll` — matches `Newtonsoft.*` (third_party)
- `Unknown.Library.dll` — matches neither pattern (unknown)

## Expected Behavior

1. `Newtonsoft.Json.dll` is immediately deleted from disk
2. `FakeSuite.dll` and `Unknown.Library.dll` remain on disk
3. `classification-manifest.json` is written with:
   - `FakeSuite.dll` → classification: "suite"
   - `Newtonsoft.Json.dll` → classification: "third_party", decompile_status: "skipped"
   - `Unknown.Library.dll` → classification: "unknown", awaiting_user_decision: true
4. User is shown a summary of unknowns and asked to classify each as "suite" or "skip"
5. After user responds, manifest is updated with `user_classification` and `awaiting_user_decision: false`
6. `completed_stages` includes "pre-classify"

## Pass Criteria
- Third-party file physically deleted
- Suite file untouched
- Unknown file prompts user for decision
- Manifest written with correct structure (validate against spec schema)
```

- [ ] **Step 2: Write `commands/pre-classify.md`**

Create `dotnet-source-reference/commands/pre-classify.md`:

```markdown
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
```

- [ ] **Step 3: Run test scenario to verify**

Dispatch a subagent with `tests/scenarios/test-pre-classify.md` as instructions and `commands/pre-classify.md` as context. Verify pre-classify behavior against the minimal-suite fixture.

- [ ] **Step 4: Commit**

```bash
git add commands/pre-classify.md tests/scenarios/test-pre-classify.md
git commit -m "feat: add pre-classify command"
```

---

## Phase 5: Decompilation

### Task 7: decompile-batch agent

**Files:**
- Create: `agents/decompile-batch.md`
- Create: `tests/scenarios/test-decompile-batch.md`

- [ ] **Step 1: Write test scenario first**

Create `tests/scenarios/test-decompile-batch.md`:

```markdown
# Test Scenario: decompile-batch agent

## Setup
batch_id: "batch-1"
assemblies:
  - { name: "FakeSuite.dll", path: "tests/fixtures/minimal-suite/FakeSuite.dll", component: "main" }
  - { name: "FakeCore.dll",  path: "tests/fixtures/minimal-suite/FakeCore.dll",  component: "main" }
profile_dir: "tests/fixtures/minimal-suite/"

Note: both DLL files are zero-byte stubs — ilspycmd will fail on them.

## Expected Behavior

1. ilspycmd is attempted for each assembly
2. Each fails (zero-byte stubs are not valid .NET assemblies)
3. Failures are recorded in the result for each assembly — NOT a hard abort
4. Agent returns a JSON array with two entries, both with decompile_status: "failed"
5. decompile_errors contains the ilspycmd error output

## Test Case B: pre-existing .decompiled.cs
If FakeSuite.decompiled.cs already exists alongside the DLL and is non-empty with a namespace declaration:
- Agent should skip ilspycmd for that assembly
- decompile_status: "skipped"

## Pass Criteria
- Returns JSON array (not a hard stop) even when all assemblies fail
- Pre-existing valid output is detected and skipped
- Each result record contains: name, component, path, decompile_status, decompile_output, decompile_errors
```

- [ ] **Step 2: Write `agents/decompile-batch.md`**

Create `dotnet-source-reference/agents/decompile-batch.md`:

```markdown
# decompile-batch

Decompile a batch of .NET assemblies using ilspycmd. Called by the decompile command when parallel processing is needed.

## Inputs (provided by the calling command)
- `batch_id`: identifier for this batch (e.g., "batch-1")
- `assemblies`: list of objects, each with `name`, `path`, `component`
- `profile_dir`: path to the directory containing the suite profile (used to resolve relative paths)

## Steps

Use the `ilspy-runner` skill for all ilspycmd invocations.

For each assembly in the batch:

1. Resolve the full path: `<profile_dir>/<assembly.path>`
2. Check if `<assembly.name>.decompiled.cs` already exists alongside the assembly. If it does and is non-empty and contains at least one `namespace` or `class` declaration: **skip** (already decompiled), set status to "success".
3. Run ilspycmd using the `-o` flag with an explicit output file path (NOT `--outputdir` which writes a directory tree):
   `ilspycmd "<full-path>" -o "<directory-of-assembly>/<AssemblyNameWithoutExtension>.decompiled.cs"`
   Example: for `source/main/MyAssembly.dll` → `ilspycmd "source/main/MyAssembly.dll" -o "source/main/MyAssembly.decompiled.cs"`
4. Capture exit code and all output.
5. Validate the output file exists, is non-empty, and contains at least one `namespace` or `class`.
6. Record result for this assembly.

## Output

Return a JSON result record for each assembly:

```json
[
  {
    "name": "MyAssembly.dll",
    "component": "main",
    "path": "source/main/MyAssembly.dll",
    "decompile_status": "success | failed | skipped",
    "decompile_output": "source/main/MyAssembly.decompiled.cs",
    "decompile_errors": ["error text if failed"]
  }
]
```

After processing all assemblies in the batch, output the complete JSON result array. The calling `decompile` command will merge these results into `classification-manifest.json`.

## Notes

- Never abort the entire batch due to one assembly failing. Log the failure and continue.
- If ilspycmd is not found on PATH, immediately stop and output: "ERROR: ilspycmd not found. Run /bootstrap-ilspy first."
```

- [ ] **Step 3: Run test scenario**

Dispatch a subagent with `tests/scenarios/test-decompile-batch.md` as instructions and `agents/decompile-batch.md` + `skills/ilspy-runner.md` as context. Verify both test cases.

- [ ] **Step 4: Commit**

```bash
git add agents/decompile-batch.md tests/scenarios/test-decompile-batch.md
git commit -m "feat: add decompile-batch agent"
```

---

### Task 8: decompile command

**Files:**
- Create: `commands/decompile.md`
- Create: `tests/scenarios/test-decompile.md`

- [ ] **Step 1: Write test scenario**

Create `tests/scenarios/test-decompile.md`:

```markdown
# Test Scenario: decompile

## Setup
Profile: `tests/fixtures/minimal-suite/test-profile.json`
Pre-existing manifest: a `classification-manifest.json` where:
- `FakeSuite.dll` → classification: "suite", decompile_status: "pending"
- `Newtonsoft.Json.dll` → classification: "third_party", decompile_status: "skipped"
- `Unknown.Library.dll` → unknowns → user_classification: "skip"

Note: `FakeSuite.dll` is a zero-byte stub — ilspycmd will fail on it.

## Expected Behavior

1. Only `FakeSuite.dll` is attempted (suite + user-classified-as-suite entries only)
2. ilspycmd fails on the zero-byte stub (expected)
3. Failure is logged to `decompile_errors` in the manifest
4. `decompile_status` is updated to "failed" for FakeSuite.dll
5. Summary is shown: "1 assembly decompiled, 0 succeeded, 1 failed"
6. `completed_stages` now includes "decompile"
7. Process does NOT abort — failure is surfaced as a summary

## Pass Criteria
- Skipped assemblies not attempted
- Failed decompile logged to manifest, not a hard stop
- completed_stages updated
- Summary output shown
```

- [ ] **Step 2: Write `commands/decompile.md`**

Create `dotnet-source-reference/commands/decompile.md`:

```markdown
# decompile

Decompile all suite-classified assemblies using ilspycmd. Reads classification-manifest.json to determine which assemblies to process. Supports parallel batch execution for large drops.

## Required inputs
- `<profile>`: path to suite profile JSON file
- `classification-manifest.json` must exist in the same directory as the profile (written by pre-classify)

## Steps

### 1. Read inputs

Load the profile and `classification-manifest.json` from the profile directory.

If `classification-manifest.json` does not exist: hard stop with "Run /pre-classify before /decompile."

### 2. Collect assemblies to decompile

From `assemblies[]`: collect entries where `classification: "suite"` and `decompile_status: "pending"`.
From `unknowns[]`: collect entries where `user_classification: "suite"`.

If this list is empty: output "Nothing to decompile — all suite assemblies already processed." and exit.

### 3. Check ilspycmd

Run `dotnet tool list -g`. If `ilspycmd` is not present: hard stop with "ilspycmd not installed. Run /bootstrap-ilspy first."

### 4. Determine execution mode

Count the assemblies to decompile. Compare against `decompile_parallel_threshold` from the profile (default: 10).

- **Sequential (count ≤ threshold):** Use the `ilspy-runner` skill to decompile each assembly directly.
- **Parallel (count > threshold):** Partition assemblies into batches of `ceil(count / N)` where N is `ceil(count / threshold)`. Dispatch one `decompile-batch` subagent per batch in parallel. Wait for all batches to complete and collect their JSON result arrays.

### 5. Update manifest

For each result returned (sequential or from batches):
- Update the matching entry in `assemblies[]` (or `unknowns[]`) with `decompile_status`, `decompile_output`, and `decompile_errors`.
- Add "decompile" to `completed_stages`.

Write the updated manifest back to disk.

### 6. Report summary

```
Decompilation complete.
  Total attempted: N
  Succeeded: N
  Failed: N  (see classification-manifest.json for details)
  Skipped (already done): N
```

If any assemblies failed, list their names and the first error line.

## Error handling

- ilspycmd not on PATH → hard stop (see step 3)
- Individual assembly decompile failure → log to manifest, continue (never abort the whole run)
- Truncated output (file exists but fails validation) → treat as failure
```

- [ ] **Step 3: Run test scenario**

Dispatch subagent with `tests/scenarios/test-decompile.md` and `commands/decompile.md`. Verify behavior.

- [ ] **Step 4: Commit**

```bash
git add commands/decompile.md tests/scenarios/test-decompile.md
git commit -m "feat: add decompile command with parallel batch support"
```

---

## Phase 6: Review, Classification, and Prune

### Task 9: assembly-classifier agent

**Files:**
- Create: `agents/assembly-classifier.md`
- Create: `tests/scenarios/test-assembly-classifier.md`

- [ ] **Step 1: Write test scenario first**

Create `tests/scenarios/test-assembly-classifier.md`:

```markdown
# Test Scenario: assembly-classifier agent

## Setup
assembly_name: "FakeSuite.dll"
component: "main"
decompiled_source: <full text of tests/fixtures/minimal-suite/FakeSuite.decompiled.cs>
all_component_sources: [{ name: "FakeSuite.dll", component: "main", decompiled_source: <same> }]
profile: <tests/fixtures/minimal-suite/test-profile.json>

## Expected Behavior

The agent should return a JSON object where:
- relevant: true (FakeSuite contains OrderManager, business domain types, SQL data access)
- confidence: ≥ 0.8
- primary_purpose: mentions order management or data access
- key_public_types: includes OrderManager and IOrderRepository
- db_tables: includes "ordertable" (from SQL literal in FakeSuite.Data.SqlOrderRepository)
- review_needed: false (confidence is high)

## Test Case B: Generic utility assembly
Provide a decompiled_source containing only extension methods for strings with no domain concepts.

Expected:
- relevant: false
- db_tables: []
- key_public_types: (may be empty or minimal)

## Pass Criteria
- Output is valid JSON matching the documented schema
- All required fields present: assembly, component, relevant, confidence, primary_purpose, key_public_types, db_tables, cross_component_relationships, review_needed
- ordertable detected from SQL in FakeSuite.decompiled.cs
- review_needed: true when confidence < 0.7
```

- [ ] **Step 2: Write `agents/assembly-classifier.md`**

Create `dotnet-source-reference/agents/assembly-classifier.md`:

```markdown
# assembly-classifier

Assess the relevance of a decompiled .NET assembly for API and business logic documentation. Called by the review-drop command.

## Inputs (provided by the calling command)
- `assembly_name`: filename of the assembly (e.g., "OrderManager.dll")
- `decompiled_source`: full text of the `.decompiled.cs` file
- `all_component_sources`: a list of `{ name, component, decompiled_source }` for all other retained assemblies (for cross-component analysis)
- `profile`: the suite profile JSON

## Assessment criteria

Determine:

**1. API/Business Logic Relevant (true/false)**
An assembly is relevant if it contains any of:
- Public classes or interfaces that represent business domain concepts (orders, customers, products, invoices, etc.)
- Data access logic (ADO.NET, ORM, SQL strings)
- Service or business logic classes (managers, processors, handlers, services)
- Configuration or startup logic for the application
- Communication contracts (WCF, REST, message schemas)

An assembly is NOT relevant if it contains only:
- Generic utility/extension methods with no domain concepts
- Third-party library shims or adapters
- Auto-generated XML serializer code (look for `XmlSerializers` in the name)
- Test fixtures

**2. Primary purpose** (one sentence)
Describe what this assembly does and why it exists.

**3. Key public types** (list up to 8)
The most important public classes and interfaces a developer would interact with. Include the type name and a 5-10 word description.

**4. DB tables touched** (list)
Scan the source for SQL string literals, parameterized query patterns, and ORM mappings. Extract table names. If none found, return an empty list.

**5. Cross-component relationships** (list)
Check `all_component_sources` for references to this assembly's types. Note which other assemblies reference or are referenced by this assembly.

**6. Confidence score** (0.0–1.0)
How confident are you in the relevance determination? Below 0.7 → flag as "Review needed".

## Output

Return a JSON object:

```json
{
  "assembly": "OrderManager.dll",
  "component": "main",
  "relevant": true,
  "confidence": 0.95,
  "primary_purpose": "Business logic layer for order lifecycle management",
  "key_public_types": [
    { "name": "OrderManager", "description": "Manages order creation, retrieval, and status transitions" },
    { "name": "IOrderRepository", "description": "Repository contract for order persistence" }
  ],
  "db_tables": ["ordertable", "orderline"],
  "cross_component_relationships": [
    "Referenced by FakeCore.WebApi (order endpoint controllers)"
  ],
  "review_needed": false
}
```

If confidence < 0.7, set `"review_needed": true` and include a `"review_reason"` field explaining the uncertainty.
```

- [ ] **Step 3: Run test scenario**

Dispatch a subagent with `tests/scenarios/test-assembly-classifier.md` as instructions and `agents/assembly-classifier.md` as context. Verify both test cases.

- [ ] **Step 4: Commit**

```bash
git add agents/assembly-classifier.md tests/scenarios/test-assembly-classifier.md
git commit -m "feat: add assembly-classifier agent"
```

---

### Task 10: review-drop command

**Files:**
- Create: `commands/review-drop.md`
- Create: `tests/scenarios/test-review-drop.md`

- [ ] **Step 1: Write test scenario**

Create `tests/scenarios/test-review-drop.md`:

```markdown
# Test Scenario: review-drop

## Setup
Profile: `tests/fixtures/minimal-suite/test-profile.json`
Source folder contains: `FakeSuite.decompiled.cs` (the pre-written fixture)
Manifest has: `FakeSuite.dll` → classification: "suite", decompile_status: "success"

No existing index file exists.

## Expected Behavior

1. assembly-classifier is dispatched for FakeSuite.dll with FakeSuite.decompiled.cs as input
2. A new index file is created at the path specified by `index_output_path` in the profile
3. The index file contains a markdown table row for FakeSuite.dll with:
   - File / Folder column: FakeSuite.decompiled.cs
   - API/Business Logic Relevant: true
   - Primary Language: C#
   - Key Public Types: includes OrderManager and IOrderRepository
   - DB Tables: ordertable, orderline (from SQL in the fixture)
   - First Indexed: today's date
   - First Indexed Commit: result of `git rev-parse HEAD` (or blank if not a git repo)
   - Stored in repo: (left for prune to set — default "Yes" for suite assemblies)
4. `completed_stages` in manifest includes "review-drop"

## Pass Criteria
- Index file created with correct columns
- assembly-classifier called for each suite assembly
- review_needed flag respected (row flagged if confidence < 0.7)
```

- [ ] **Step 2: Write `commands/review-drop.md`**

Create `dotnet-source-reference/commands/review-drop.md`:

```markdown
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
```

- [ ] **Step 3: Run test scenario**

Dispatch subagent with scenario and command file. Verify.

- [ ] **Step 4: Commit**

```bash
git add commands/review-drop.md tests/scenarios/test-review-drop.md
git commit -m "feat: add review-drop command"
```

---

### Task 11: prune command

**Files:**
- Create: `commands/prune.md`
- Create: `tests/scenarios/test-prune.md`

- [ ] **Step 1: Write test scenario**

Create `tests/scenarios/test-prune.md`:

```markdown
# Test Scenario: prune

## Setup
Source folder contains:
- `FakeSuite.dll` — suite assembly, Stored in repo: Yes
- `FakeSuite.pdb` — debug symbols
- `FakeSuite.decompiled.cs` — retained source
- `FakeSuite.dll.config` — config file

Index marks `FakeSuite.dll.config` as `Stored in repo: Yes` (relevant config).
Index marks `FakeSuite.dll` as `Stored in repo: Yes`.
Index marks `FakeSuite.pdb` as `Stored in repo: No`.

## Test Case A: Normal prune
Expected:
- `FakeSuite.pdb` is deleted
- `FakeSuite.dll` is deleted (binary even though index says Yes — binaries are always pruned after decompilation)
- `FakeSuite.decompiled.cs` is retained
- `FakeSuite.dll.config` is retained

## Test Case B: Conflict — prune would delete a file marked Stored in repo: Yes
Inject a scenario where a `.decompiled.cs` file is marked `Stored in repo: Yes` in the index but is not present on disk.
Expected: hard stop with conflict message.

## Pass Criteria
- Binaries (.dll, .exe) always deleted after decompilation regardless of index
- .pdb always deleted
- .decompiled.cs retained if Stored in repo: Yes
- Config files retained if Stored in repo: Yes
- Conflict triggers hard stop
```

- [ ] **Step 2: Write `commands/prune.md`**

Create `dotnet-source-reference/commands/prune.md`:

```markdown
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
```

- [ ] **Step 3: Run test scenario**

Dispatch subagent with scenario and command. Verify both test cases.

- [ ] **Step 4: Commit**

```bash
git add commands/prune.md tests/scenarios/test-prune.md
git commit -m "feat: add prune command"
```

---

## Phase 7: Context Generation

### Task 12: context-distiller agent

**Files:**
- Create: `agents/context-distiller.md`
- Create: `tests/scenarios/test-context-distiller.md`

- [ ] **Step 1: Write test scenario first**

Create `tests/scenarios/test-context-distiller.md`:

```markdown
# Test Scenario: context-distiller agent

## Setup
assembly_name: "FakeSuite.dll"
component: "main"
suite_name: "Fake Test Suite"
indexed_date: "2026-03-18"
decompiled_source: <full text of tests/fixtures/minimal-suite/FakeSuite.decompiled.cs>
classifier_result: {
  "relevant": true,
  "primary_purpose": "Business logic and data access for order management",
  "key_public_types": [
    { "name": "OrderManager", "description": "Manages order lifecycle" },
    { "name": "IOrderRepository", "description": "Repository contract" },
    { "name": "SqlOrderRepository", "description": "ADO.NET implementation" }
  ],
  "db_tables": ["ordertable"],
  "cross_component_relationships": []
}
all_component_sources: [{ name: "FakeSuite.dll", component: "main", decompiled_source: <same> }]
schema_tables: null

## Expected Behavior

The agent produces a .ctx.md string (to be written to file by the calling command) where:

Frontmatter:
- assembly: FakeSuite
- component: main
- suite: Fake Test Suite
- indexed: 2026-03-18
- relevant: true
- db_tables: [ordertable]

Body sections (all required):
- ## Purpose — mentions order management
- ## Public API Surface — includes OrderManager and IOrderRepository
- ## Integration Points — mentions ordertable
- ## Key Patterns — at least one pattern described
- ## Notes — present (may note decompilation limitations)

No ## DB Schema section (schema_tables is null).

## Test Case B: With schema_tables
Provide schema_tables with an entry for "ordertable" including columns and a lookup table.
Expected: ## DB Schema section appears at the end with table definition.

## Pass Criteria
- All 6 frontmatter keys present
- All 5 required body sections present
- No suite-specific language beyond what appears in the provided source
- File is within ~800-1200 token range (concise, not a full dump)
- With schema_tables: DB Schema section present
```

- [ ] **Step 2: Write `agents/context-distiller.md`**

Create `dotnet-source-reference/agents/context-distiller.md`:

```markdown
# context-distiller

Produce an LLM-optimized `.ctx.md` file for a single .NET assembly. The output is designed for injection into an LLM context window to support development of customizations and integrations.

## Inputs (provided by the calling command)
- `assembly_name`: filename of the assembly
- `component`: component name from the profile
- `suite_name`: from the profile
- `indexed_date`: today's date (YYYY-MM-DD)
- `decompiled_source`: full text of the `.decompiled.cs` file
- `classifier_result`: JSON result from assembly-classifier (key types, DB tables, relationships)
- `all_component_sources`: list of `{ name, component, decompiled_source }` for cross-referencing
- `schema_tables`: optional; map of table name → schema definition (provided when ingest-schema has been run)

## Output format

Write a `.ctx.md` file with this exact structure. Do not add or remove top-level sections.

```markdown
---
assembly: <assembly name without extension>
component: <component name>
suite: <suite name>
indexed: <YYYY-MM-DD>
relevant: true
db_tables: [<comma-separated table names, or empty>]
---

## Purpose
<One concise paragraph. What does this assembly do and why does it exist? Who calls it?
Focus on what a developer building a customization or integration needs to know.>

## Public API Surface
<List the key public classes, interfaces, and methods.
Format: `ClassName` — brief description
Under each class, list important methods:
  - `MethodName(params) → ReturnType` — what it does
Limit to the 5-10 most important types. Skip internals, generated code, and trivial helpers.>

## Integration Points
<Describe:
- What other assemblies/components call this one
- What this assembly calls (dependencies)
- Which DB tables it reads/writes (from classifier_result.db_tables)
- Cross-component relationships (from classifier_result.cross_component_relationships)>

## Key Patterns
<Describe 2-5 implementation patterns that a developer integrating with or customizing this assembly should know about.
Examples: how DI is wired, how errors are handled, naming conventions, how config is loaded.>

## Notes
<Edge cases, known limitations, gotchas, or gaps in the decompiled source.
If any types or methods appear truncated or unclear from decompilation: call it out.>
```

If `schema_tables` is provided for tables in `classifier_result.db_tables`, append this section:

```markdown
## DB Schema
<For each referenced table in schema_tables:>

### <table_name>
| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
<rows from schema_tables>

<If the table is a lookup table and has lookup_values, include:>
**Lookup values:** <list key → description pairs>
```

## Quality criteria

- **Concise:** The entire file should fit in 800-1200 tokens. Cut ruthlessly.
- **Signal over noise:** Describe what a developer needs to use or extend this assembly. Skip implementation minutiae.
- **Generic language:** Do not reference any suite-specific context from your training. Describe only what is in the provided source.
- **No speculation:** If you cannot determine something from the source, say "not determinable from decompiled source."
```

- [ ] **Step 3: Run test scenario**

Dispatch a subagent with `tests/scenarios/test-context-distiller.md` as instructions and `agents/context-distiller.md` as context. Verify both test cases (with and without schema_tables).

- [ ] **Step 4: Commit**

```bash
git add agents/context-distiller.md tests/scenarios/test-context-distiller.md
git commit -m "feat: add context-distiller agent"
```

---

### Task 13: generate-context command

**Files:**
- Create: `commands/generate-context.md`
- Create: `tests/scenarios/test-generate-context.md`

- [ ] **Step 1: Write test scenario**

Create `tests/scenarios/test-generate-context.md`:

```markdown
# Test Scenario: generate-context

## Setup
Profile: `tests/fixtures/minimal-suite/test-profile.json`
Source: `FakeSuite.decompiled.cs` exists
Index: FakeSuite is marked relevant, with types `OrderManager`, `IOrderRepository`, DB tables `ordertable`, `orderline`

## Expected Behavior

1. context-distiller is dispatched for FakeSuite.dll
2. A `.ctx.md` file is written to `<context_output_path>/main/FakeSuite.ctx.md`
3. The file contains:
   - Valid YAML frontmatter with all required keys: assembly, component, suite, indexed, relevant, db_tables
   - All 5 required body sections: Purpose, Public API Surface, Integration Points, Key Patterns, Notes
   - Purpose mentions order management
   - Public API Surface includes OrderManager and IOrderRepository
   - DB tables in frontmatter matches [ordertable, orderline]
4. completed_stages includes "generate-context"

## Pass Criteria
- .ctx.md created at correct path
- All 6 frontmatter keys present
- All 5 body sections present (Purpose, Public API Surface, Integration Points, Key Patterns, Notes)
- File size roughly 800-1200 tokens (not too sparse, not too verbose)
```

- [ ] **Step 2: Write `commands/generate-context.md`**

Create `dotnet-source-reference/commands/generate-context.md`:

```markdown
# generate-context

Generate LLM-optimized `.ctx.md` context files for each relevant assembly. This is the primary output of the pipeline.

## Required inputs
- `<profile>`: path to suite profile JSON
- Index tables must exist (from review-drop)
- `.decompiled.cs` files must be present for relevant assemblies (after prune)

## Steps

### 1. Load inputs

Load the profile. Load all index tables. Load classification-manifest.json.

Identify all assemblies where:
- Index row exists with `API/Business Logic Relevant: true`
- `.decompiled.cs` file exists on disk

### 2. Load all decompiled sources

Read all `.decompiled.cs` files for relevant assemblies across all components. Build `all_component_sources` list.

Load classifier results from `classification-manifest.json` → `classifier_results` map (populated by review-drop). Do NOT parse classifier data from the markdown index table — use the structured JSON from the manifest.

### 3. Check for schema tables (optional enrichment)

Look for `schema-enrichment.json` in the profile directory. If it exists and its `schema_version` matches "1.0": load it. Build a map of table name → table definition. Pass as `schema_tables` to context-distiller for assemblies whose DB tables appear in the schema.

If `schema-enrichment.json` does not exist: proceed without schema enrichment (not an error).

### 4. Dispatch context-distiller per assembly

For each relevant assembly, dispatch the `context-distiller` agent with:
- `assembly_name`, `component`, `suite_name`, `indexed_date` (today)
- `decompiled_source`: the text of its `.decompiled.cs`
- `classifier_result`: parsed from the index table row for this assembly
- `all_component_sources`: the full list
- `schema_tables`: matching tables from schema enrichment (or empty if not available)

Write the returned `.ctx.md` content to: `<context_output_path>/<component>/<assembly-name-without-extension>.ctx.md`

Create the output directory if it does not exist.

### 5. Handle failures

If context-distiller fails or times out for an assembly:
- Log to `generation-errors.md` in the profile directory: append a line with timestamp, assembly name, and error
- Continue with remaining assemblies

### 6. Update manifest

Add "generate-context" to `completed_stages`.

### 7. Report summary

```
Context generation complete.
  .ctx.md files written: N
  Failed: N  (see generation-errors.md)

Output: <context_output_path>/
```
```

- [ ] **Step 3: Run test scenario**

Dispatch subagent. Verify.

- [ ] **Step 4: Commit**

```bash
git add agents/context-distiller.md commands/generate-context.md tests/scenarios/test-generate-context.md
git commit -m "feat: add generate-context command and context-distiller agent"
```

---

### Task 14: generate-indexes command (optional feature)

**Files:**
- Create: `commands/generate-indexes.md`
- Create: `tests/scenarios/test-generate-indexes.md`

- [ ] **Step 1: Write test scenario**

Create `tests/scenarios/test-generate-indexes.md`:

```markdown
# Test Scenario: generate-indexes

## Setup
Profile: `tests/fixtures/minimal-suite/test-profile.json`
Existing index: FakeSuite row with basic columns populated
FakeSuite.decompiled.cs: exists

## Expected Behavior

1. Index table is enriched: Key Public Types and DB Tables columns populated for FakeSuite
2. A "## Cross-Component Relationships" section is appended (may note "No cross-component relationships detected" for a single-component fixture)
3. completed_stages includes "generate-indexes"

## Pass Criteria
- Key Public Types column populated with type names
- DB Tables column populated
- Cross-Component Relationships section present at bottom of index
```

- [ ] **Step 2: Write `commands/generate-indexes.md`**

Create `dotnet-source-reference/commands/generate-indexes.md`:

```markdown
# generate-indexes

[OPTIONAL] Enrich human-readable index tables with detailed type information and cross-component relationships. Run with `--with-indexes` flag in process-drop, or invoke directly.

This command enriches the index tables written by review-drop. It is optional — the primary output of the pipeline is `.ctx.md` files from generate-context.

## Required inputs
- `<profile>`: path to suite profile JSON
- Index tables must exist (from review-drop)
- `.decompiled.cs` files should be present for accurate enrichment

## Steps

### 1. Load inputs

Load profile and all index tables from `<index_output_path>`.

### 2. Enrich each row

For each row in each index table where a `.decompiled.cs` file exists:

- **Key Public Types**: List up to 8 public class/interface names from the source, comma-separated. If already populated from review-drop, update only if the existing value is blank.
- **DB Tables**: List table names found in SQL patterns in the source. If already populated, update only if blank.
- **Description**: If the row has a placeholder description, replace with a one-sentence summary derived from the source.

### 3. Append or replace Cross-Component Relationships section

At the bottom of each index file, write:

```markdown
## Cross-Component Relationships

<For each shared assembly that appears in multiple components, note:>
- `AssemblyName` — shared across <list of components>; <brief note on the relationship>

<For assemblies that call across component boundaries, note:>
- `ComponentA.AssemblyX` calls into `ComponentB.AssemblyY` for <purpose>

<If no cross-component relationships detected:>
No cross-component relationships detected.
```

Derive relationships by checking if the same assembly name appears in multiple components, and by scanning for type references across component sources.

### 4. Update manifest

Add "generate-indexes" to `completed_stages`.

### 5. Report summary

```
Index enrichment complete.
  Index files updated: <list>
```
```

- [ ] **Step 3: Run test scenario**

Dispatch subagent. Verify.

- [ ] **Step 4: Commit**

```bash
git add commands/generate-indexes.md tests/scenarios/test-generate-indexes.md
git commit -m "feat: add generate-indexes command (optional enrichment)"
```

---

## Phase 8: Database Enrichment

### Task 15: detect-databases command

**Files:**
- Create: `commands/detect-databases.md`
- Create: `tests/scenarios/test-detect-databases.md`

- [ ] **Step 1: Write test scenario**

Create `tests/scenarios/test-detect-databases.md`:

```markdown
# Test Scenario: detect-databases

## Setup
Source: `FakeSuite.decompiled.cs` (contains SQL referencing ordertable, orderline, and statuslookup)

## Expected Behavior

1. database-context.json is written
2. It contains:
   - schema_version: "1.0"
   - databases array with at least one entry
   - ordertable listed as a table with access_patterns including SELECT and INSERT
   - statuslookup listed with probable_lookup: true (read-only, frequently joined)
3. completed_stages includes "detect-databases"

## Pass Criteria
- database-context.json written with valid structure
- ordertable and statuslookup detected
- probable_lookup correctly identified for statuslookup
```

- [ ] **Step 2: Write `commands/detect-databases.md`**

Create `dotnet-source-reference/commands/detect-databases.md`:

```markdown
# detect-databases

Analyze decompiled source for database usage patterns. Write database-context.json for use by the companion schema extraction plugin.

## Required inputs
- `<profile>`: path to suite profile JSON
- `.decompiled.cs` files must be present (after decompile/prune)

## Steps

### 1. Load all decompiled sources

Read all `.decompiled.cs` files across all component folders.

Also read all `.config` and `.exe.config` files — connection strings often appear there.

### 2. Detect database providers and connection strings

Scan all files for:
- `connectionString` or `ConnectionString` attributes in config files
- `SqlConnection`, `OleDbConnection`, `OdbcConnection`, `NpgsqlConnection` class usages
- Provider names in connection strings (e.g., "System.Data.SqlClient", "Npgsql")

For each detected database, note the database name (from connection string `Initial Catalog` or `Database` attribute if present, otherwise use the provider name as identifier).

### 3. Extract table references

Scan all `.decompiled.cs` files for:
- SQL string literals containing `FROM`, `INTO`, `UPDATE`, `JOIN`, `TABLE` keywords
- Extract table names using pattern: SQL keyword followed by whitespace and identifier
- ADO.NET `SqlCommand` text parameters
- ORM table mappings (Entity Framework, NHibernate) — look for `[Table("name")]` attributes or `HasTableName("name")` calls

For each table, record:
- Which assemblies reference it
- Which SQL operations are used (`SELECT`, `INSERT`, `UPDATE`, `DELETE`)

### 4. Identify probable lookup tables

A table is a probable lookup if ALL of these signals are present:
- Only `SELECT` operations observed (no `INSERT`, `UPDATE`, `DELETE`)
- Referenced by 3 or more other queries (frequently joined)
- Used with a small, specific column set (typically an ID and description column)

### 5. Identify dynamic SQL and stored procedures

Flag any locations where SQL is constructed with string concatenation. Note as unresolved reference with reason "dynamic SQL."

Flag stored procedure calls (`EXEC`, `ExecuteStoredProcedure`) as unresolved with reason "stored procedure — internals not visible from source."

### 6. Write database-context.json

Write to the profile directory. Schema:
```json
{
  "schema_version": "1.0",
  "generated_at": "<ISO8601>",
  "suite": "<suite_name>",
  "components_analyzed": ["<component names>"],
  "databases": [
    {
      "name": "<database name>",
      "detected_from": ["<how detected>"],
      "tables": [
        {
          "name": "<table>",
          "referenced_in": ["<assembly names>"],
          "access_patterns": ["SELECT", "INSERT", ...],
          "probable_lookup": false
        }
      ],
      "unresolved_references": [
        { "assembly": "<name>", "reason": "<why unresolved>" }
      ]
    }
  ],
  "gaps": ["<human-readable notes on limitations>"]
}
```

### 7. Update manifest and report

Add "detect-databases" to `completed_stages`. Output summary:
```
Database detection complete.
  Databases detected: N
  Tables identified: N
  Probable lookup tables: N
  Unresolved references: N

database-context.json written.
```

If no SQL patterns found: write `"databases": []` with a note in gaps. Not an error.
```

- [ ] **Step 3: Run test scenario**

Dispatch subagent. Verify.

- [ ] **Step 4: Commit**

```bash
git add commands/detect-databases.md tests/scenarios/test-detect-databases.md
git commit -m "feat: add detect-databases command"
```

---

### Task 16: ingest-schema command

**Files:**
- Create: `commands/ingest-schema.md`
- Create: `tests/scenarios/test-ingest-schema.md`

- [ ] **Step 1: Write test scenario**

Create `tests/scenarios/test-ingest-schema.md`:

```markdown
# Test Scenario: ingest-schema

## Setup
Profile: `tests/fixtures/minimal-suite/test-profile.json`
Schema file: `tests/fixtures/schema-fixture/schema-enrichment.json`
Existing .ctx.md: `FakeSuite.ctx.md` with db_tables: [ordertable, statuslookup]

## Expected Behavior

1. FakeSuite.ctx.md is updated with a "## DB Schema" section
2. DB Schema section contains table definitions for ordertable and statuslookup
3. statuslookup includes lookup_values (OP/Open, CL/Closed, HD/On Hold)
4. No error for orderline (referenced in ctx but not in schema — informational note only)

## Test Case B: Missing schema file
Expected: hard stop with message referencing schema-enrichment.json

## Test Case C: Wrong schema_version
Expected: hard stop with message about schema_version mismatch

## Pass Criteria
- DB Schema section added to .ctx.md
- Lookup values present for lookup tables
- Missing table: informational note, not hard stop
- Missing file: hard stop
- Wrong version: hard stop
```

- [ ] **Step 2: Write `commands/ingest-schema.md`**

Create `dotnet-source-reference/commands/ingest-schema.md`:

```markdown
# ingest-schema

Enrich `.ctx.md` files and index tables using schema data from the companion schema extraction plugin. Run after generate-context.

## Required inputs
- `<profile>`: path to suite profile JSON
- `schema-enrichment.json`: must exist in the profile directory (produced by the schema extraction plugin)
- `database-context.json`: must exist in the profile directory (produced by detect-databases)
- `.ctx.md` files must exist (produced by generate-context)

## Steps

### 1. Validate inputs

Load `schema-enrichment.json` from the profile directory.
- If not found: hard stop — "schema-enrichment.json not found. Run the schema extraction plugin and place its output in the profile directory before running /ingest-schema."
- If `schema_version` is absent or not "1.0": hard stop — "schema-enrichment.json has unsupported schema_version. Expected 1.0."

Load `database-context.json` from the profile directory.
- If not found or incompatible `schema_version`: hard stop — "database-context.json not found or incompatible. Run /detect-databases before /ingest-schema, or re-run if schema_version changed."

### 2. Build table lookup

From `schema-enrichment.json`, build a map: `table_name → table_definition`.

### 3. Enrich each .ctx.md file

For each `.ctx.md` file in `<context_output_path>`:

1. Read the frontmatter `db_tables` field (list of table names).
2. For each table name in `db_tables`:
   - Look it up in the table map.
   - If found: include its schema (columns, foreign keys, lookup_values if is_lookup).
   - If not found in schema but IS in database-context.json: log as informational note (the schema plugin may not have extracted all tables).
   - If not found in either: log as informational note.
3. Write or replace the `## DB Schema` section at the end of the `.ctx.md` file.

DB Schema section format:
```markdown
## DB Schema

### <table_name>
| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| <col>  | <type> | <yes/no> | <notes> |

<If is_lookup and lookup_values present:>
**Lookup values:**
| Code | Description |
|------|-------------|
| <code> | <description> |
```

### 4. Enrich index tables (if they exist)

For each index table, update the `DB Tables` column for rows where the table definition is now known from the schema. Append `*` to the value to indicate schema-enriched (e.g., "ordertable*, statuslookup*").

### 5. Report summary

```
Schema ingestion complete.
  .ctx.md files enriched: N
  Tables resolved from schema: N
  Tables not in schema (informational): N
```
```

- [ ] **Step 3: Run test scenario**

Dispatch subagent. Verify all three test cases.

- [ ] **Step 4: Commit**

```bash
git add commands/ingest-schema.md tests/scenarios/test-ingest-schema.md
git commit -m "feat: add ingest-schema command"
```

---

## Phase 9: Orchestration

### Task 17: process-drop command

**Files:**
- Create: `commands/process-drop.md`
- Create: `tests/scenarios/test-process-drop.md`

- [ ] **Step 1: Write test scenario**

Create `tests/scenarios/test-process-drop.md`:

```markdown
# Test Scenario: process-drop (Integration Test)

## Setup
Profile: `tests/fixtures/minimal-suite/test-profile.json`
Source folder: minimal-suite fixture as-is (FakeSuite.dll, Newtonsoft.Json.dll, Unknown.Library.dll)
Note: FakeSuite.dll is a zero-byte stub; decompilation will fail.
Pre-place `FakeSuite.decompiled.cs` in the folder before running so generate-context can proceed.

## Expected Behavior (full pipeline run)

1. User is prompted for version label → user enters "Test v1.0"
2. bootstrap-ilspy runs (or reports already installed)
3. pre-classify:
   - Newtonsoft.Json.dll deleted
   - FakeSuite.dll classified as suite
   - Unknown.Library.dll prompts user → user responds "skip"
   - Manifest written
4. decompile:
   - FakeSuite.dll decompilation fails (zero-byte stub) — logged, not aborted
5. review-drop: uses pre-placed FakeSuite.decompiled.cs — index created
6. prune: binaries and PDBs deleted
7. generate-context: FakeSuite.ctx.md written
8. generate-indexes NOT run (no --with-indexes flag)
9. detect-databases prompt: user declines
10. CHANGELOG.md entry written: "Test v1.0", today's date, components processed

## Expected artifacts
- classification-manifest.json (with completed_stages including all run stages)
- output/reference/index-main.md
- output/context/main/FakeSuite.ctx.md
- CHANGELOG.md with one entry
- Newtonsoft.Json.dll deleted from fixture

## Pass Criteria
- All stages run in sequence
- CHANGELOG written with correct entry
- generate-indexes skipped (no flag)
- decompile failure does not abort pipeline
- All expected output files present
```

- [ ] **Step 2: Write `commands/process-drop.md`**

Create `dotnet-source-reference/commands/process-drop.md`:

```markdown
# process-drop

Orchestrate the complete dotnet-source-reference pipeline for a new binary drop. Processes all components defined in the profile together.

## Required inputs
- `<profile>`: path to suite profile JSON file
- Optional flag: `--with-indexes` — also run generate-indexes after generate-context

## Pre-flight checks

Before starting any stage:
1. Verify the profile file exists and is valid JSON conforming to config/schema.json.
2. Resolve all `components[].path` values relative to the directory containing the profile file.
3. Verify that each component path exists on disk. If any path is missing: hard stop listing the missing paths.

## Step 1: Prompt for version label

Ask the user: the `version_prompt` string from the profile (e.g., "What version/service pack is this binary drop?")

Record the response as `<version_label>`.

## Step 2: Run bootstrap-ilspy

Execute the `bootstrap-ilspy` command. If it hard-stops, abort the pipeline.

## Step 3: Run pre-classify

Execute `/pre-classify <profile>`. If it hard-stops, abort and display:
"Pipeline aborted at pre-classify. Fix the issue above and re-run /process-drop, or run /pre-classify manually to resume."

## Step 4: Run decompile

Execute `/decompile <profile>`. Decompile failures for individual assemblies do not abort. If the command itself hard-stops, abort pipeline with resume instructions.

## Step 5: Run review-drop

Execute `/review-drop <profile>`.

## Step 6: Run prune

Execute `/prune <profile>`. Hard stop on conflict (see prune command docs).

## Step 7: Run generate-context

Execute `/generate-context <profile>`.

## Step 8: Conditionally run generate-indexes

If the `--with-indexes` flag was passed: execute `/generate-indexes <profile>`.
Otherwise: skip (output "Skipping generate-indexes — pass --with-indexes to include human-readable index enrichment.")

## Step 9: Prompt for database enrichment

Ask the user:
"Database enrichment is available. Run /detect-databases now to analyze DB usage patterns? (y/n)"

If yes: execute `/detect-databases <profile>`.

Then ask:
"Do you have a schema-enrichment.json file from the schema extraction plugin to ingest? (y/n)"

If yes: execute `/ingest-schema <profile>`.

## Step 10: Write CHANGELOG.md

Append an entry to `CHANGELOG.md` in the profile directory:

```markdown
| <today YYYY-MM-DD> | <version_label> | <comma-separated component names> | Pipeline run via /process-drop |
```

If CHANGELOG.md does not exist, create it with a header first:
```markdown
# Source Reference Changelog

| Date | Suite Version / Release | Components Processed | Notes |
|------|------------------------|----------------------|-------|
```

## Step 11: Final summary

Output:
```
Process-drop complete.
  Version: <version_label>
  Components: <list>
  Stages run: <list>
  Output:
    Context files: <context_output_path>/
    Index files: <index_output_path>/  (if --with-indexes)
    CHANGELOG.md updated
```

## Resume instructions

If the pipeline aborts mid-run, the `classification-manifest.json` checkpoint records which stages completed. The user can re-run individual commands starting from the failed stage. Each command checks `completed_stages` and skips work already done.
```

- [ ] **Step 3: Run integration test scenario**

Dispatch subagent with `tests/scenarios/test-process-drop.md` and all command/agent/skill files as context. Verify the full pipeline runs correctly.

- [ ] **Step 4: Commit**

```bash
git add commands/process-drop.md tests/scenarios/test-process-drop.md
git commit -m "feat: add process-drop orchestrator command"
```

---

## Phase 10: M2M Profile

### Task 18: M2M config profile

**Files:**
- Create: `config/profiles/m2m.json`

This is the only file in the plugin that references Made2Manage or Aptean by name.

- [ ] **Step 1: Write `config/profiles/m2m.json`**

Create `dotnet-source-reference/config/profiles/m2m.json`:

```json
{
  "suite_name": "Aptean Made2Manage",
  "components": [
    { "name": "erpserver", "path": "source-m2merpserver" },
    { "name": "idserver",  "path": "source-m2midserver"  },
    { "name": "webapi",    "path": "source-m2mwebapi"    }
  ],
  "known_suite_patterns": [
    "M2M*",
    "Aptean.*",
    "Consona.*",
    "BDL*",
    "PSDomain.*",
    "Notification*",
    "Reporting.*",
    "SMSIntegration.*",
    "ErrorAndValidation*",
    "CloudPrinter.*"
  ],
  "known_third_party_patterns": [
    "Microsoft.*",
    "System.*",
    "Newtonsoft.*",
    "DevExpress.*",
    "CefSharp.*",
    "libcef.*",
    "chrome_*",
    "libGLES*",
    "libEGL*",
    "libcef.dll",
    "d3dcompiler*",
    "vk_swiftshader*",
    "vulkan-1.*",
    "Google.*",
    "Owin.*",
    "Serilog.*",
    "RestSharp.*",
    "PayPal.*",
    "Stripe.*",
    "Twilio.*",
    "AuthorizeNet.*",
    "BouncyCastle.*",
    "DocumentFormat.*",
    "WindowsBase.*",
    "IdentityServer3.*",
    "IdentityModel.*",
    "Unity.*",
    "WebApiThrottle.*",
    "CommonServiceLocator.*",
    "netstandard.*",
    "uniPoint.*",
    "eSELECTplus*",
    "Hyak.*",
    "Std.*"
  ],
  "unknown_default": "decompile",
  "decompile_parallel_threshold": 10,
  "index_output_path": ".github/agents/reference",
  "context_output_path": ".github/agents/context",
  "version_prompt": "What M2M version/service pack is this binary drop? (e.g., SP24)"
}
```

**Note on `m2m-sample/` fixture:** The spec calls for a `tests/fixtures/m2m-sample/` directory containing a small real M2M assembly subset. This plan intentionally omits committing real M2M binaries to the plugin repository (they are proprietary and large). Instead, the M2M smoke test below points directly at the live source folders in the `m2m-source-reference` repository. This is documented as an intentional deviation — the smoke test is a local developer test, not a portable CI fixture.

- [ ] **Step 2: M2M profile smoke test**

Run the following manually (no file modifications — read-only test):

Dispatch a subagent with instructions to:
1. Load `config/profiles/m2m.json`
2. Load the file listing from `C:\Users\jake.wimmer\Repositories\m2m-source-reference\source-m2merpserver\` (list of filenames only)
3. Apply pre-classify pattern matching rules to each filename
4. Verify that the following are classified as `third_party`:
   - `CefSharp.Core.dll`, `Google.Apis.dll`, `Newtonsoft.Json.dll`, `Serilog.dll`, `Microsoft.Graph.dll`
5. Verify that the following are classified as `suite`:
   - `M2MBusinessServer.dll`, `Aptean.Made2Manage.Email.dll`, `Consona.Business.dll`, `BDLBase.dll`
6. Report any files that would be classified as `unknown`

Expected: zero unknowns for files in the existing source folders (all should be suite or third-party). If unknowns appear, update the profile's pattern lists.

- [ ] **Step 3: Update profile if needed**

If the smoke test reveals unknown files, add their patterns to the appropriate list in `m2m.json`.

- [ ] **Step 4: Commit**

```bash
git add config/profiles/m2m.json
git commit -m "feat: add M2M suite config profile"
```

---

## Phase 11: Final Verification

### Task 19: End-to-end verification

- [ ] **Step 1: Verify generality constraint**

Review every file in `commands/`, `agents/`, and `skills/`. Confirm none contain references to:
- "Made2Manage", "M2M", "Aptean", "Consona"
- Any specific assembly name from the M2M suite
- Any database name (M2MDATA05, M2MSystem)

The only permitted M2M references are in `config/profiles/m2m.json`.

If any violations found: edit the file to use generic terminology.

- [ ] **Step 2: Verify plugin.json completeness**

Confirm `plugin.json` lists all commands, agents, and skills. Compare against the file tree.

- [ ] **Step 3: Final commit and tag**

```bash
git add -A
git commit -m "feat: complete dotnet-source-reference plugin v0.1.0"
git tag v0.1.0
```

---

## Appendix: Testing Quick Reference

| Command / Agent | Test Scenario File | Key Fixture |
|---|---|---|
| bootstrap-ilspy | tests/scenarios/test-bootstrap-ilspy.md | — |
| pre-classify | tests/scenarios/test-pre-classify.md | minimal-suite/ |
| decompile | tests/scenarios/test-decompile.md | minimal-suite/ |
| decompile-batch (agent) | tests/scenarios/test-decompile-batch.md | minimal-suite/ |
| assembly-classifier (agent) | tests/scenarios/test-assembly-classifier.md | minimal-suite/ |
| review-drop | tests/scenarios/test-review-drop.md | minimal-suite/ |
| prune | tests/scenarios/test-prune.md | minimal-suite/ |
| context-distiller (agent) | tests/scenarios/test-context-distiller.md | minimal-suite/ |
| generate-context | tests/scenarios/test-generate-context.md | minimal-suite/ |
| generate-indexes | tests/scenarios/test-generate-indexes.md | minimal-suite/ |
| detect-databases | tests/scenarios/test-detect-databases.md | minimal-suite/ |
| ingest-schema | tests/scenarios/test-ingest-schema.md | schema-fixture/ |
| process-drop | tests/scenarios/test-process-drop.md | minimal-suite/ |
