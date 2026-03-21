# Container DTO Design

## Problem

The current normalization output uses `NormalizedResult<TRoot>` — a root DTO plus an opaque `NormalizationContext` with untyped `Dictionary<string, List<object>>` collections accessed by string keys. This doesn't produce a single, self-contained, JSON-serializable DTO that contains all normalized data.

The original `data-normalization` project output a flat `Dictionary<string, List<Dictionary<string, object>>>` that serialized directly to JSON with named type arrays. DataNormalizer should produce a strongly-typed equivalent.

## Design

### Container DTO = Normalized Root

The generator produces a container DTO whose:
- **Root properties** are normalized (simple props kept as-is, object refs become `int` indices, collections become `int[]` indices)
- **Entity lists** are added as `{TypeName}List` array properties for every type in the normalization graph

### Example

Given:
```csharp
public class PeopleDto { public Person[] People { get; set; } }
public class Person { public string Name { get; set; } public Address Home { get; set; } }
public class Address { public string City { get; set; } public string Zip { get; set; } }
```

Generated container:
```csharp
public partial class NormalizedPeopleDto
{
    public int[] PeopleIndices { get; set; } = Array.Empty<int>();
    public NormalizedPerson[] PersonList { get; set; } = Array.Empty<NormalizedPerson>();
    public NormalizedAddress[] AddressList { get; set; } = Array.Empty<NormalizedAddress>();
}
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

Serialized JSON:
```json
{
  "PeopleIndices": [0, 0, 1, 2],
  "PersonList": [
    { "Name": "John", "HomeIndex": 0 },
    { "Name": "Jane", "HomeIndex": 0 },
    { "Name": "June", "HomeIndex": -1 }
  ],
  "AddressList": [
    { "City": "Anytown", "Zip": "12345" },
    { "City": "BigCity", "Zip": "99999" }
  ]
}
```

### Root Property Mapping Rules

| Root property type | Container property |
|---|---|
| Simple (string, int, etc.) | Same type, same name |
| Single complex object | `int {PropertyName}Index` |
| Nullable complex object | `int? {PropertyName}Index` |
| Collection of complex objects | `int[] {PropertyName}Indices` |
| Inlined object | Kept as-is |

### Entity List Naming

Always `{TypeName}List`. The `WithName("custom")` configuration overrides `TypeName`.

### Normalizer

```csharp
public static NormalizedPeopleDto Normalize(PeopleDto source)
{
    var context = new NormalizationContext(typeCount);

    // Normalize root properties
    var peopleIndices = new int[source.People.Length];
    for (var i = 0; i < source.People.Length; i++)
        peopleIndices[i] = NormalizePerson(source.People[i], context);

    // Build container from context
    var result = new NormalizedPeopleDto();
    result.PeopleIndices = peopleIndices;
    result.PersonList = context.GetCollection<NormalizedPerson>("Person").ToArray();
    result.AddressList = context.GetCollection<NormalizedAddress>("Address").ToArray();
    return result;
}
```

`NormalizationContext` is used internally during normalization then discarded. Per-type `NormalizeXxx()` helper methods are unchanged.

### Denormalizer

```csharp
public static PeopleDto Denormalize(NormalizedPeopleDto normalized)
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

    // Reconstruct root from container
    var root = new PeopleDto();
    root.People = new Person[normalized.PeopleIndices.Length];
    for (var i = 0; i < normalized.PeopleIndices.Length; i++)
        root.People[i] = persons[normalized.PeopleIndices[i]];

    return root;
}
```

Input is the container DTO. Reads from typed arrays directly.

## What Changes

### Removed
- `NormalizedResult<TRoot>` — container DTO replaces it
- `CastingReadOnlyList<T>` — container uses concrete arrays

### Kept (internal)
- `NormalizationContext` — still used during normalization, never exposed

### Unchanged
- Configuration API (attributes, builder, config base class)
- Per-type DTOs with `IEquatable<T>`
- Per-type `NormalizeXxx()` helper methods
- Circular reference handling (two-DTO pattern)

### New
- `ContainerEmitter` — generates the container DTO class
- Modified `NormalizerEmitter` — entry method returns container
- Modified `DenormalizerEmitter` — takes container as input

## Reversibility

Any consumer (frontend, API client, other language) can reconstruct the original object graph from the serialized container:

1. Parse JSON into container shape
2. Reconstruct leaf entities from their lists
3. Reconstruct composite entities by resolving index references into entity lists
4. Reconstruct root by resolving root index properties into entity lists

Shared references are preserved: multiple indices pointing to the same list entry reconstruct as the same object reference.
