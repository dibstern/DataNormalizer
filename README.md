# DataNormalizer

A .NET source generator that normalizes nested object graphs into flat, deduplicated representations.

[![NuGet](https://img.shields.io/nuget/v/DataNormalizer.svg)](https://www.nuget.org/packages/DataNormalizer)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

[Documentation](https://dibstern.github.io/DataNormalizer/) | [API Reference](https://dibstern.github.io/DataNormalizer/api/)

## What It Does

**Before** — nested objects with shared references:

```csharp
var sharedAddress = new Address { City = "Seattle", Zip = "98101" };

var people = new[]
{
    new Person { Name = "Alice", Home = sharedAddress },
    new Person { Name = "Bob",   Home = sharedAddress },
};
```

**After** — flat, deduplicated DTOs with integer index references:

```csharp
// NormalizedPerson { Name = "Alice", HomeIndex = 0 }
// NormalizedPerson { Name = "Bob",   HomeIndex = 0 }  ← same index, deduplicated
//
// Address collection: [ NormalizedAddress { City = "Seattle", Zip = "98101" } ]
```

Shared `Address` instances are stored once. References become integer indices into flat collections.

## Installation

```
dotnet add package DataNormalizer
```

## Quick Start

### 1. Define your domain types

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

### 2. Create a configuration class

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

### 3. Normalize and denormalize

```csharp
var result = AppNormalization.Normalize(person);
var restored = AppNormalization.Denormalize(result);
```

The source generator produces the `Normalize` and `Denormalize` static methods at compile time.

## Target Frameworks

The runtime library targets `net8.0`, `net9.0`, and `net10.0`. The source generator targets `netstandard2.0` (a Roslyn requirement) and is bundled in the same NuGet package.

## Configuration Options

### Auto-discovery

`NormalizeGraph<T>()` walks the type graph starting from `T` and discovers all referenced complex types automatically.

```csharp
builder.NormalizeGraph<Person>(); // discovers Address, etc.
```

### Opt-out (Inline)

Keep a type inline instead of normalizing it into a separate collection:

```csharp
builder.NormalizeGraph<Person>(graph =>
{
    graph.Inline<Metadata>(); // Metadata stays nested, not extracted
});
```

### Ignore a property

Exclude a property from the generated DTO:

```csharp
builder.ForType<Person>(p => p.IgnoreProperty(x => x.Secret));
```

Or use the attribute:

```csharp
public class Person
{
    public string Name { get; set; }

    [NormalizeIgnore]
    public string Secret { get; set; }
}
```

### ExplicitOnly mode

Only include properties that are explicitly opted-in:

```csharp
builder.ForType<Person>(p =>
{
    p.UsePropertyMode(PropertyMode.ExplicitOnly);
    p.IncludeProperty(x => x.Name);
});
```

Or use attributes:

```csharp
public class Person
{
    [NormalizeInclude]
    public string Name { get; set; }

    public string NotIncluded { get; set; }
}
```

### Multiple root types

Register multiple roots to generate overloaded `Normalize()`/`Denormalize()` methods:

```csharp
protected override void Configure(NormalizeBuilder builder)
{
    builder.NormalizeGraph<Person>();
    builder.NormalizeGraph<Order>();
}
```

## Generated Code

The source generator produces:

- **`Normalized{TypeName}`** — partial classes implementing `IEquatable<T>` for value-based deduplication.
- **`{Name}Index`** (`int`) — replaces normalized nested object references.
- **`{Name}Indices`** (`int[]`) — replaces normalized collection references.
- **Inlined properties** keep their original type.
- All generated DTOs are `partial`, so you can extend them with additional members.

## NormalizedResult API

```csharp
var result = AppNormalization.Normalize(person);

result.Root              // The root normalized DTO
result.RootIndex         // Index of the root in its type collection

result.GetCollection<NormalizedAddress>("Address")  // Flat collection by type key
result.GetCollection<NormalizedAddress>()            // Same, using typeof(T).Name

result.CollectionNames   // All type keys

result.Resolve<NormalizedAddress>("Address", 0)      // Resolve by type key + index
```

## Circular References

- The generator detects cycles at compile time and emits a **DN0001** warning.
- Suppress with `<NoWarn>$(NoWarn);DN0001</NoWarn>` in your `.csproj` if the cycle is intentional.
- Normalization handles cycles correctly via value-equality-based deduplication.
- Denormalization uses a two-pass approach: create all objects first, then resolve references.

## Diagnostics

| ID     | Severity | Description                          | Resolution                                               |
| ------ | -------- | ------------------------------------ | -------------------------------------------------------- |
| DN0001 | Warning  | Circular reference detected          | Add `<NoWarn>DN0001</NoWarn>` if intentional             |
| DN0002 | Error    | Configuration class must be `partial` | Add the `partial` keyword to the class declaration       |
| DN0003 | Error    | Type has no public properties        | Add public properties or exclude the type                |
| DN0004 | Info     | Unmapped complex type will be inlined | Use `graph.Inline<T>()` explicitly, or add to the graph  |

## Known Constraints

- For circular types, back-edge properties (those creating the cycle) use shape-based comparison (null/non-null, collection count) rather than full structural comparison. All non-circular properties — including nested complex subtrees — are fully compared. False dedup only occurs if two objects in a cycle have identical simple properties, identical non-circular subtree structure, AND identical circular reference shapes.

## License

MIT — see [LICENSE](LICENSE).
