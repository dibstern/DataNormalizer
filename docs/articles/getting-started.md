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
public class Team
{
    public string Name { get; set; }
    public Person[] Members { get; set; }
}

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
public partial class SearchNormalizer : NormalizationConfig
{
    protected override void Configure(NormalizeBuilder builder)
    {
        builder.NormalizeGraph<Team>();  // discovers Person, Address
    }
}
```

`NormalizeGraph<Team>()` tells the source generator to walk the type graph starting from `Team` and discover all referenced complex types (`Person`, `Address`, etc.) automatically.

## 3. Normalize and denormalize

```csharp
var sharedAddress = new Address { City = "Seattle", Zip = "98101" };

var team = new Team
{
    Name = "Engineering",
    Members = new[]
    {
        new Person { Name = "Alice", Home = sharedAddress },
        new Person { Name = "Bob",   Home = sharedAddress },
    },
};

// Normalize: nested graph → flat, deduplicated DTOs
var result = SearchNormalizer.Normalize(team);

// Denormalize: flat DTOs → restored nested graph
var restored = SearchNormalizer.Denormalize(result);
```

The `Normalize` and `Denormalize` static methods are generated at compile time by the source generator.

## How It Works

The source generator analyzes your `Configure` method at compile time and produces:

- **Per-type DTOs (`{TypeName}Dto`)** — partial classes implementing `IEquatable<T>` for value-based dedup. Object references become `int` (`{Name}Index`), collections become `int[]` (`{Name}Indices`). Types marked as inline keep their original structure.
- **Container result (`{RootType}ResultDto`)** — holds a `Result` property for the root entity and typed arrays for every other entity type. If the root type is also referenced by other types, a root list array is included too.
- **`Normalize(T)` / `Denormalize({RootType}ResultDto)`** — static methods on your configuration class. `Normalize` flattens the object graph with value-equality dedup. `Denormalize` reconstructs the full graph with shared references preserved.
- **Naming and JSON serialization** — DTOs get a `Dto` suffix by default. `[JsonPropertyName]` attributes are emitted for camelCase JSON. Both are configurable via `UseNaming()`. Wire format is customizable with `UseJsonContract()` and `Reference().JsonName()`.
- All generated types are `partial`, so you can extend them with additional members.

## Working with the result

The `Normalize` method returns a container DTO (`TeamResultDto`) with a `Result` property for the root entity and typed arrays for every other entity type:

```csharp
var result = SearchNormalizer.Normalize(team);

result.Result                            // TeamDto — the root entity (always present)

result.PersonDtos                        // PersonDto[] (typed array)
result.AddressDtos                       // AddressDto[] (typed array)
// All collections are typed properties — no string-keyed lookups.
// The container serializes directly with System.Text.Json.
```

For full API details, see the [API Reference](../api/index.md).

## Next steps

- [Configuration Guide](configuration.md) — All configuration options including opt-out, ignore, and explicit-only mode
- [Naming & JSON Contracts](naming-and-contracts.md) — Customize DTO suffixes, JSON property names, and wire format
- [Diagnostics Reference](diagnostics.md) — Compiler diagnostics DN0001–DN1002
