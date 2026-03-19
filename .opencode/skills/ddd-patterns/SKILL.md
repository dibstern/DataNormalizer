---
name: ddd-patterns
description: Domain-Driven Design patterns for the DataNormalizer project. Covers aggregate roots, entities with identity, value objects as records, repository interfaces with CQRS split, domain exceptions, IClock for time abstraction, and command/query separation.
---

# Domain-Driven Design Patterns

While DataNormalizer is a library (not a domain application), these patterns inform how domain types should be structured for normalization. Understanding these patterns helps the generator handle real-world domain models correctly.

## Value Objects as Records

Value objects have no identity — they are defined by their properties. Use C# records.

```csharp
// Value object - perfect candidate for normalization deduplication
public sealed record Address
{
    public required string Street { get; init; }
    public required string City { get; init; }
    public required string State { get; init; }
    public required string ZipCode { get; init; }
}

// Value object with behavior
public sealed record Money(decimal Amount, string Currency)
{
    public Money Add(Money other)
    {
        if (Currency != other.Currency)
            throw new DomainException($"Cannot add {Currency} and {other.Currency}");
        return this with { Amount = Amount + other.Amount };
    }
}

// Value objects get structural equality for free via records
// DataNormalizer leverages this: IEquatable<T> on the source type
// is used for deduplication during normalization
```

## Entities with Identity

Entities have a unique identity that persists across state changes. Two entities with the same properties but different IDs are different.

```csharp
public sealed class Customer
{
    public Guid Id { get; init; }
    public string Name { get; set; } = "";
    public Address BillingAddress { get; set; } = new();
    public Address? ShippingAddress { get; set; }

    // Entity equality is identity-based
    public override bool Equals(object? obj) => obj is Customer c && c.Id == Id;
    public override int GetHashCode() => Id.GetHashCode();
}
```

**DataNormalizer implication:** When normalizing entities, the generator should respect `IEquatable<T>` if implemented. For entities, equality is typically ID-based, meaning two entities with the same ID but different property values are considered duplicates.

## Aggregate Roots

An aggregate root is the entry point to a cluster of domain objects. External code should only reference the root.

```csharp
public sealed class Order  // Aggregate root
{
    public Guid Id { get; init; }
    public Customer Customer { get; init; } = null!;
    public List<OrderLine> Lines { get; init; } = [];
    public Money Total => Lines.Aggregate(Money.Zero, (sum, line) => sum.Add(line.Subtotal));

    public void AddLine(Product product, int quantity)
    {
        if (quantity <= 0) throw new DomainException("Quantity must be positive");
        Lines.Add(new OrderLine { Product = product, Quantity = quantity });
    }
}

public sealed class OrderLine  // Entity within the aggregate
{
    public Product Product { get; init; } = null!;
    public int Quantity { get; init; }
    public Money Subtotal => Product.Price with { Amount = Product.Price.Amount * Quantity };
}
```

**DataNormalizer implication:** `builder.NormalizeGraph<Order>()` would walk the entire aggregate: `Order` → `OrderLine` → `Product`, `Customer` → `Address`. Shared references (e.g., same `Customer` across orders) are deduplicated.

## Repository Interfaces (CQRS Split)

Separate read and write operations for clarity.

```csharp
// Query side - read-only
public interface IOrderReadRepository
{
    Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Order>> GetByCustomerAsync(Guid customerId, CancellationToken cancellationToken = default);
}

// Command side - mutations
public interface IOrderWriteRepository
{
    Task AddAsync(Order order, CancellationToken cancellationToken = default);
    Task UpdateAsync(Order order, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

// Combined when separation isn't needed
public interface IOrderRepository : IOrderReadRepository, IOrderWriteRepository { }
```

## Command/Query Separation

Commands change state but return nothing (or a Result). Queries return data but don't change state.

```csharp
// Command
public sealed record NormalizeOrderCommand(Guid OrderId);

public sealed class NormalizeOrderHandler(
    IOrderReadRepository orders,
    IResultStore results)
{
    public async Task<Result<Guid>> HandleAsync(
        NormalizeOrderCommand command,
        CancellationToken cancellationToken = default)
    {
        var order = await orders.GetByIdAsync(command.OrderId, cancellationToken)
            .ConfigureAwait(false);

        if (order is null)
            return Result<Guid>.Fail($"Order {command.OrderId} not found");

        var normalized = OrderNormalization.Normalize(order);
        var resultId = await results.StoreAsync(normalized, cancellationToken)
            .ConfigureAwait(false);

        return Result<Guid>.Ok(resultId);
    }
}

// Query
public sealed record GetNormalizedOrderQuery(Guid ResultId);

public sealed class GetNormalizedOrderHandler(IResultStore results)
{
    public async Task<NormalizedResult<NormalizedOrder>?> HandleAsync(
        GetNormalizedOrderQuery query,
        CancellationToken cancellationToken = default)
    {
        return await results.GetAsync<NormalizedOrder>(query.ResultId, cancellationToken)
            .ConfigureAwait(false);
    }
}
```

## Domain Exceptions

Use specific exception types for domain rule violations.

```csharp
public sealed class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
    public DomainException(string message, Exception innerException) : base(message, innerException) { }
}

public sealed class EntityNotFoundException(string entityType, object id)
    : DomainException($"{entityType} with ID '{id}' was not found")
{
    public string EntityType => entityType;
    public object Id => id;
}

// Usage
public async Task<Order> GetRequiredAsync(Guid id, CancellationToken ct = default)
{
    return await GetByIdAsync(id, ct).ConfigureAwait(false)
        ?? throw new EntityNotFoundException(nameof(Order), id);
}
```

## IClock for Time Abstraction

Never use `DateTime.Now` or `DateTime.UtcNow` directly. Inject a clock for testability.

```csharp
// Abstraction
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

// Production implementation
public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

// Test implementation
public sealed class FakeClock(DateTimeOffset fixedTime) : IClock
{
    public DateTimeOffset UtcNow => fixedTime;
    public void Advance(TimeSpan duration) => fixedTime = fixedTime.Add(duration);
}

// Usage in domain
public sealed class AuditService(IClock clock)
{
    public AuditEntry CreateEntry(string action) => new()
    {
        Action = action,
        Timestamp = clock.UtcNow,
    };
}

// In .NET 8+, consider using TimeProvider instead:
public sealed class AuditService(TimeProvider timeProvider)
{
    public AuditEntry CreateEntry(string action) => new()
    {
        Action = action,
        Timestamp = timeProvider.GetUtcNow(),
    };
}
```

## DataNormalizer-Specific DDD Considerations

### Normalizing Domain Models

When using DataNormalizer with DDD models:

1. **Value objects** deduplicate naturally (same properties → same identity)
2. **Entities** deduplicate by identity if `IEquatable<T>` is implemented
3. **Aggregate boundaries** map to `NormalizeGraph<T>()` calls
4. **Inlined types** (via `graph.Inline<T>()`) are good for small value objects that shouldn't be normalized separately

```csharp
[NormalizeConfiguration]
public partial class DomainNormalization : NormalizationConfig
{
    protected override void Configure(NormalizeBuilder builder)
    {
        // Each aggregate root is a separate graph
        builder.NormalizeGraph<Order>(graph =>
        {
            graph.Inline<Money>();  // Small value object, keep inline
            graph.ForType<Customer>(t =>
            {
                t.IgnoreProperty(x => x.PasswordHash);  // Exclude sensitive data
            });
        });
    }
}
```
