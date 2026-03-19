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
