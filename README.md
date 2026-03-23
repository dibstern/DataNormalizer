# DataNormalizer

A .NET source generator that normalizes nested object graphs into flat, deduplicated, JSON-serializable containers.

[![NuGet](https://img.shields.io/nuget/v/DataNormalizer.svg)](https://www.nuget.org/packages/DataNormalizer)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

[Documentation](https://dibstern.github.io/DataNormalizer/) | [API Reference](https://dibstern.github.io/DataNormalizer/api/)

## What It Does

Given a nested object graph with shared references:

```csharp
var sharedAddress = new Address { City = "Seattle", Zip = "98101" };

var alice = new Person { Name = "Alice", Home = sharedAddress };
var bob   = new Person { Name = "Bob",   Home = sharedAddress };
```

One call normalizes the graph into a flat, deduplicated container:

```csharp
var result = AppNormalization.Normalize(alice);
var json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
```

```json
{
  "RootIndex": 0,
  "PersonList": [
    { "Name": "Alice", "HomeIndex": 0 },
    { "Name": "Bob",   "HomeIndex": 0 }
  ],
  "AddressList": [
    { "City": "Seattle", "Zip": "98101" }
  ]
}
```

The shared `Address` is stored once. References become integer indices into typed arrays. The container serializes directly with `System.Text.Json` and is easy to reverse on any frontend.

## Installation

```
dotnet add package DataNormalizer
```

Supports **.NET 6**, **.NET 7**, **.NET 8**, **.NET 9**, and **.NET 10**.

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

### 3. Normalize, use, and denormalize

```csharp
// Normalize
var result = AppNormalization.Normalize(person);

// Access the root entity via RootIndex
var root = result.PersonList[result.RootIndex];
Console.WriteLine(root.Name);           // "Alice"
Console.WriteLine(root.HomeIndex);      // 0

// Access entity lists directly
Console.WriteLine(result.PersonList.Length);   // 2
Console.WriteLine(result.AddressList.Length);  // 1 (deduplicated)

// Serialize the entire container to JSON
var json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });

// Denormalize back to the original object graph
var restored = AppNormalization.Denormalize(result);
```

The source generator produces `Normalize` and `Denormalize` static methods, per-type DTOs, and the container result class at compile time.

## How It Works

For each `NormalizeGraph<T>()` call, the generator produces:

1. **Per-type DTOs** (`Normalized{TypeName}`) — partial classes implementing `IEquatable<T>` for value-based deduplication. Nested object references become `int` indices (`{Name}Index`), collections become `int[]` (`{Name}Indices`).

2. **A container result** (`Normalized{TypeName}Result`) — holds `RootIndex` and a `{TypeName}List` array for every entity type in the graph. This is the primary output of `Normalize()` and the input to `Denormalize()`.

3. **`Normalize(T)` / `Denormalize(Normalized{T}Result)`** — static methods on the configuration class.

All generated types are `partial`, so you can extend them with additional members.

## Target Frameworks

| Component | Targets |
|---|---|
| Runtime library | `net6.0`, `net7.0`, `net8.0`, `net9.0`, `net10.0` |
| Source generator | `netstandard2.0` (Roslyn requirement, bundled in NuGet package) |

The generator runs at compile time regardless of your target framework. The runtime library provides the `NormalizationContext` used internally by generated code.

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

Register multiple roots to generate separate container types and `Normalize()`/`Denormalize()` overloads:

```csharp
protected override void Configure(NormalizeBuilder builder)
{
    builder.NormalizeGraph<Person>();  // → NormalizedPersonResult
    builder.NormalizeGraph<Order>();   // → NormalizedOrderResult
}
```

Each container includes only the entity lists reachable from its root type.

## Reversing Normalized Data

Any consumer (frontend, API client, other language) can reconstruct the original object graph from the serialized container:

1. Parse JSON into the container shape
2. Reconstruct leaf entities from their lists
3. Reconstruct composite entities by resolving index references into entity lists
4. Resolve the root entity via `RootIndex` into its entity list

Shared references are preserved: multiple indices pointing to the same list entry reconstruct as the same object reference.

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
