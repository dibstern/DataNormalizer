# Getting Started

This guide walks you through installing DataNormalizer, defining your domain types, and normalizing your first object graph.

## Installation

```
dotnet add package DataNormalizer
```

The NuGet package includes both the runtime library and the source generator. No additional packages are needed.

## 1. Define your domain types

Start with plain C# classes that form an object graph:

```csharp
public class Person
{
    public string Name { get; set; }
    public Address Home { get; set; }
}

public class Address
{
    public string City { get; set; }
    public string Zip { get; set; }
}
```

## 2. Create a configuration class

Create a class that inherits from `NormalizationConfig`, mark it with `[NormalizeConfiguration]`, and make it `partial`:

```csharp
using DataNormalizer.Attributes;
using DataNormalizer.Configuration;

[NormalizeConfiguration]
public partial class AppNormalization : NormalizationConfig
{
    protected override void Configure(NormalizeBuilder builder)
    {
        builder.NormalizeGraph<Person>();
    }
}
```

`NormalizeGraph<Person>()` tells the source generator to walk the type graph starting from `Person` and discover all referenced complex types (`Address`, etc.) automatically.

## 3. Normalize and denormalize

```csharp
var sharedAddress = new Address { City = "Seattle", Zip = "98101" };

var person = new Person { Name = "Alice", Home = sharedAddress };

// Normalize: nested graph → flat, deduplicated DTOs
var result = AppNormalization.Normalize(person);

// Denormalize: flat DTOs → restored nested graph
var restored = AppNormalization.Denormalize(result);
```

The `Normalize` and `Denormalize` static methods are generated at compile time by the source generator.

## What the source generator produces

For each type in the graph, the generator creates:

- **`Normalized{TypeName}`** — a partial class implementing `IEquatable<T>` for value-based deduplication.
- **`{Name}Index`** (`int`) — replaces nested object references with integer indices.
- **`{Name}Indices`** (`int[]`) — replaces collection references with integer index arrays.
- **Inlined properties** keep their original type (for types marked as inline).
- All generated DTOs are `partial`, so you can extend them with additional members.

## Working with the result

The `Normalize` method returns a container DTO (`Normalized{RootType}Result`) with typed arrays for every entity type in the graph:

```csharp
var result = AppNormalization.Normalize(person);

result.PersonList[0]                     // The root DTO (always at index 0)

result.PersonList                        // NormalizedPerson[] (typed array)
result.AddressList                       // NormalizedAddress[] (typed array)
// All collections are typed properties — no string-keyed lookups.
// The container serializes directly with System.Text.Json.
```

For full API details, see the [API Reference](../api/index.md).

## Next steps

- [Configuration Guide](configuration.md) — All configuration options including opt-out, ignore, and explicit-only mode
- [Diagnostics Reference](diagnostics.md) — Compiler diagnostics DN0001–DN0004
