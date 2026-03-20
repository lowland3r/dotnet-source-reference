---
assembly: FakeSuite.dll
component: main
generated: 2026-03-19
primary_language: C#
relevant: true
key_types:
  - OrderManager
  - IOrderRepository
db_tables:
  - ordertable
  - statuslookup
---

# FakeSuite

## Summary

Manages order lifecycle for the Fake Suite application. Provides ADO.NET-based data access for order and lookup table operations. Core business logic component for purchase order processing.

## Public API

### OrderManager

Manages order lifecycle. Methods: `GetOrder(string orderId)`, `CreateOrder(string customerId, IEnumerable<OrderLine> lines)`.

### IOrderRepository

Data access interface for order persistence. Methods: `FindById(string id)`, `Save(Order order)`.

## SQL / DB Usage

### ordertable

```sql
SELECT * FROM ordertable WHERE fcorderid = @id
INSERT INTO ordertable (fcorderid, fccustid, fcstatus) VALUES (@id, @cust, @status)
```

### statuslookup

```sql
SELECT code, description FROM statuslookup
```

## Cross-Component References

None detected.
