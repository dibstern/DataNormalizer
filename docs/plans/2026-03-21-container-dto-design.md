# Container DTO Design (v2 — post-audit revision)

## Problem

The current normalization output uses `NormalizedResult<TRoot>` — a root DTO plus an opaque `NormalizationContext` with untyped `Dictionary<string, List<object>>` collections accessed by string keys. This doesn't produce a single, self-contained, JSON-serializable DTO that contains all normalized data.

The original `data-normalization` project output a flat `Dictionary<string, List<Dictionary<string, object>>>` that serialized directly to JSON with named type arrays. DataNormalizer should produce a strongly-typed equivalent.

## Design

### Container = Separate Result Type with RootIndex

The container is a **separate class** from per-type DTOs. It holds:
- `RootIndex` — index into the root type's entity list
- `{TypeName}List` array properties for ALL types in the graph (including root)

Per-type DTOs are generated for ALL types (including root) as before with `IEquatable<T>`.

### Why separate from per-type DTOs?

100% of existing usage is entity-as-root (`NormalizeGraph<Person>()`). If the container were the same class as the per-type DTO, the entity list property would create a recursive type reference (`NormalizedPerson[] PersonList` on `NormalizedPerson` itself), causing wasteful nested empty arrays in JSON.

### Example

Given:
```csharp
public class Person { public string Name { get; set; } public Address Home { get; set; } }
public class Address { public string City { get; set; } public string Zip { get; set; } }

// Config: builder.NormalizeGraph<Person>()
```

Per-type DTOs (unchanged):
```csharp
public partial class NormalizedPerson : IEquatable<NormalizedPerson>
{
    public string Name { get; set; } = default!;
    public int HomeIndex { get; set; }
}
public partial class NormalizedAddress : IEquatable<NormalizedAddress>
{
    public string City { get; set; } = default!;
    public string Zip { get; set; } = default!;
}
```

Generated container:
```csharp
public partial class NormalizedPersonResult
{
    public int RootIndex { get; set; }
    public NormalizedPerson[] PersonList { get; set; } = Array.Empty<NormalizedPerson>();
    public NormalizedAddress[] AddressList { get; set; } = Array.Empty<NormalizedAddress>();
}
```

Serialized JSON:
```json
{
  "RootIndex": 0,
  "PersonList": [
    { "Name": "Alice", "HomeIndex": 0 },
    { "Name": "Bob", "HomeIndex": 0 }
  ],
  "AddressList": [
    { "City": "Seattle", "Zip": "98101" }
  ]
}
```

### Container Naming

- Container class: `Normalized{RootTypeName}Result`
- Entity list properties: `{TypeName}List` (uses short type name, not custom key from `WithName()`)
- `WithName()` affects only the `NormalizationContext` type key, not the container property name

### Normalizer

```csharp
public static NormalizedPersonResult Normalize(Person source)
{
    var context = new NormalizationContext(typeCount);
    var rootIndex = NormalizePerson(source, context);
    var result = new NormalizedPersonResult();
    result.RootIndex = rootIndex;
    // Populate entity lists from context
    result.PersonList = CopyCollection<NormalizedPerson>(context, "Person");
    result.AddressList = CopyCollection<NormalizedAddress>(context, "Address");
    return result;
}
```

`NormalizePerson()` helper is unchanged — root type is NOT special-cased. All `NormalizeXxx()` helpers are generated for all types.

### Denormalizer

```csharp
public static Person Denormalize(NormalizedPersonResult normalized)
{
    // Pass 1: Create objects, populate simple + inlined properties
    var addresses = new Address[normalized.AddressList.Length];
    for (var i = 0; i < normalized.AddressList.Length; i++)
    {
        addresses[i] = new Address();
        addresses[i].City = normalized.AddressList[i].City;
        addresses[i].Zip = normalized.AddressList[i].Zip;
    }

    var persons = new Person[normalized.PersonList.Length];
    for (var i = 0; i < normalized.PersonList.Length; i++)
    {
        persons[i] = new Person();
        persons[i].Name = normalized.PersonList[i].Name;
    }

    // Pass 2: Resolve references
    for (var i = 0; i < normalized.PersonList.Length; i++)
        persons[i].Home = addresses[normalized.PersonList[i].HomeIndex];

    // Resolve root by index
    return persons[normalized.RootIndex];
}
```

## What Changes

### Removed
- `NormalizedResult<TRoot>` — container result replaces it

### Kept
- `NormalizationContext` — public (generated code references it), used during normalization then discarded
- `CastingReadOnlyList<T>` — stays inside NormalizationContext
- All `NormalizeXxx()` helpers — unchanged, including for root type
- Per-type DTOs with `IEquatable<T>` — ALL types including root

### New
- `ContainerEmitter` — generates the container result class
- Modified `NormalizerEmitter.EmitPublicNormalizeMethod()` — returns container result
- Modified `DenormalizerEmitter.EmitDenormalizeMethod()` — accepts container result

## Reversibility

Any consumer can reconstruct the original object graph:

1. Parse JSON into container shape
2. Reconstruct leaf entities from their lists
3. Reconstruct composite entities by resolving index references into entity lists
4. Resolve root entity via `RootIndex` into root type's entity list

Shared references are preserved: multiple indices pointing to the same list entry reconstruct as the same object reference.

## Multi-Root

Each `NormalizeGraph<T>()` call produces its own container type (`Normalized{T}Result`), `Normalize(T)` method, and `Denormalize(Normalized{T}Result)` method. Per-type DTOs are shared across roots if graphs overlap.
