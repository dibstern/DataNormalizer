# Configuration Guide

DataNormalizer uses a fluent API and attributes to control how your object graph is normalized. All configuration is defined in a class that inherits from `NormalizationConfig`.

## Auto-discovery

`NormalizeGraph<T>()` walks the type graph starting from `T` and discovers all referenced complex types automatically:

```csharp
builder.NormalizeGraph<Team>();  // discovers Person, Address, etc.
```

Any complex type reachable from `Team` is automatically included in the normalization graph and gets its own flat collection.

## Opt-out (Inline)

Keep a type inline instead of normalizing it into a separate collection:

```csharp
builder.NormalizeGraph<Person>(graph =>
{
    graph.Inline<Metadata>(); // Metadata stays nested, not extracted
});
```

Inlined types retain their original structure within the parent DTO rather than being replaced by an index reference.

## Ignore a property

Exclude a property from the generated DTO entirely.

### Fluent API

```csharp
builder.ForType<Person>(p => p.IgnoreProperty(x => x.Secret));
```

### Attribute

```csharp
public class Person
{
    public string Name { get; set; }

    [NormalizeIgnore]
    public string Secret { get; set; }
}
```

Ignored properties are omitted from the generated `Normalized{TypeName}` class.

## ExplicitOnly mode

Only include properties that are explicitly opted-in. All other properties are excluded.

### Fluent API

```csharp
builder.ForType<Person>(p =>
{
    p.UsePropertyMode(PropertyMode.ExplicitOnly);
    p.IncludeProperty(x => x.Name);
});
```

### Attribute

```csharp
public class Person
{
    [NormalizeInclude]
    public string Name { get; set; }

    public string NotIncluded { get; set; }
}
```

When `ExplicitOnly` mode is active, only properties marked with `[NormalizeInclude]` or registered via `IncludeProperty()` appear in the generated DTO.

## Multiple root types

Register multiple roots to generate overloaded `Normalize()`/`Denormalize()` methods for each root type:

```csharp
protected override void Configure(NormalizeBuilder builder)
{
    builder.NormalizeGraph<Team>();   // → NormalizedTeamResult
    builder.NormalizeGraph<Order>();  // → NormalizedOrderResult
}
```

Each root type gets its own `Normalize` and `Denormalize` overload on the configuration class.

## Container Result API

Each `NormalizeGraph<T>()` produces a container DTO (`Normalized{RootType}Result`) that provides access to the flat, deduplicated collections as typed arrays:

```csharp
var result = AppNormalization.Normalize(team);

result.TeamList[0]                       // The root DTO (always at index 0)

result.TeamList                          // NormalizedTeam[] (typed array)
result.PersonList                        // NormalizedPerson[] (typed array)
result.AddressList                       // NormalizedAddress[] (typed array)
// All collections are typed properties — no string-keyed lookups.
// The container serializes directly with System.Text.Json.
```

For full API details, see the [API Reference](../api/index.md).

## Circular references

The generator handles circular references in the type graph:

- The generator detects cycles at compile time and emits a **DN0001** warning.
- Suppress with `<NoWarn>$(NoWarn);DN0001</NoWarn>` in your `.csproj` if the cycle is intentional.
- Normalization handles cycles correctly via value-equality-based deduplication.
- Denormalization uses a two-pass approach: create all objects first, then resolve references.

See [Diagnostics Reference](diagnostics.md) for details on DN0001 and other diagnostics.
