# Test Scenario: assembly-classifier agent

## Setup
assembly_name: "FakeSuite.dll"
component: "main"
decompiled_source: <full text of tests/fixtures/minimal-suite/FakeSuite.decompiled.cs>
all_component_sources: [
  { name: "FakeSuite.dll", component: "main", decompiled_source: <same as above> },
  { name: "FakeApi.dll", component: "webapi", decompiled_source: "
using FakeSuite.Orders;
namespace FakeApi.Controllers {
  public class OrderController {
    private readonly OrderManager _manager;
    public OrderController(OrderManager manager) { _manager = manager; }
    public Order Get(string id) => _manager.GetOrder(id);
  }
}" }
]
profile: <tests/fixtures/minimal-suite/test-profile.json>

## Expected Behavior

The agent should return a JSON object where:
- relevant: true (FakeSuite contains OrderManager, business domain types, SQL data access)
- confidence: ≥ 0.8
- primary_purpose: mentions order management or data access
- key_public_types: includes OrderManager and IOrderRepository
- db_tables: includes "ordertable" (from SQL literal in FakeSuite.Data.SqlOrderRepository)
- review_needed: false (confidence is high)
- cross_component_relationships: at least one entry noting that FakeApi.dll references FakeSuite.Orders.OrderManager

## Test Case B: Generic utility assembly

Use this decompiled_source (inline — no file needed):
```csharp
// Decompiled with ilspycmd
// StringHelper v1.0.0

namespace StringHelper
{
    public static class StringExtensions
    {
        public static string ToTitleCase(this string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            return char.ToUpper(input[0]) + input.Substring(1).ToLower();
        }

        public static bool IsNullOrWhitespace(this string input)
        {
            return string.IsNullOrWhiteSpace(input);
        }
    }
}
```

Expected:
- relevant: false (only generic string utilities, no domain concepts)
- db_tables: []
- confidence: ≥ 0.8 (should be confident this is not relevant)
- review_needed: false

## Pass Criteria
- Output is valid JSON matching the documented schema
- All required fields present: assembly, component, relevant, confidence, primary_purpose, key_public_types, db_tables, cross_component_relationships, review_needed
- ordertable detected from SQL in FakeSuite.decompiled.cs
- review_needed: true when confidence < 0.7
- cross_component_relationships is non-empty (FakeApi.dll reference detected)
