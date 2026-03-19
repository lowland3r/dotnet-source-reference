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
