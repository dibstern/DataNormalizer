# JSON Contract Customization Design (Phase 2)

**Date:** 2026-03-25

**Goal:** Let users control the JSON wire format of normalized containers -- root property shape, collection JSON names, and per-property reference JSON names -- so generated output matches existing API contracts.

**Prerequisite:** Phase 1 (naming policy) must be complete. Phase 2 builds on `NamingModel`, naming-aware emitters, and the `ProcessAssignment` parser code path.

**Context:** The ChatGPT discussion in `docs/plans/2026-03-24-chatgpt-discussion.md` showed that a real transport search API uses a flat normalized format with domain-friendly JSON names (`"routes"`, `"lines"`, `"result"`) rather than generated ones (`"searchRouteDtos"`, `"searchLineDtos"`). This phase adds the configuration to match that format.

---

## Design Principles

1. **Root is always a named property** -- Every container has a `Result` property, never accessed by `list[0]`
2. **No redundant lists** -- If a type is only the root (not referenced by other types), it has no list array at all
3. **Semantic CLR names, configurable JSON names** -- `LineIndex` in C#, `[JsonPropertyName("line")]` on the wire
4. **Config overrides attributes** -- `ForType<T>().Reference().JsonName()` wins over `[NormalizeJsonName]`

---

## Feature 1: Root Property Redesign

### Current (after Phase 1)

```csharp
public partial class PersonResultDto
{
    [JsonPropertyName("personDtos")]
    public PersonDto[] PersonDtos { get; set; }  // root at index 0

    [JsonPropertyName("addressDtos")]
    public AddressDto[] AddressDtos { get; set; }
}
```

### After Phase 2 (default, root not referenced elsewhere)

```csharp
public partial class PersonResultDto
{
    [JsonPropertyName("result")]
    public PersonDto Result { get; set; }

    [JsonPropertyName("addressDtos")]
    public AddressDto[] AddressDtos { get; set; }
    // NO PersonDtos array
}
```

### After Phase 2 (root IS referenced by other types)

If `Place` is the root AND `Route.Origin` is a `Place` reference:

```csharp
public partial class PlaceResultDto
{
    [JsonPropertyName("result")]
    public PlaceDto Result { get; set; }

    [JsonPropertyName("placeDtos")]
    public PlaceDto[] PlaceDtos { get; set; }  // needed for index refs
}
```

### Compile-time detection

The TypeGraphAnalyzer already produces `TypeGraphNode` for every type in the graph, with `AnalyzedProperty.Kind` indicating `Normalized` or `Collection` references. To determine if the root type needs a list:

```
rootTypeNeedsList = any other node's properties reference the root type's FQN
                    with Kind == Normalized or Kind == Collection
```

This is a straightforward check on the already-computed type graph.

---

## Feature 2: UseJsonContract

### API

```csharp
public sealed class JsonContractBuilder
{
    public string? RootPropertyName { get; set; }
    public void Collection<T>(string jsonName);
}
```

### Usage

```csharp
builder.NormalizeGraph<SearchResponse>(graph =>
{
    graph.UseJsonContract(c =>
    {
        c.RootPropertyName = "searchResult";      // override "result" default
        c.Collection<SearchRoute>("routes");       // JSON name for SearchRoute list
        c.Collection<SearchLine>("lines");         // JSON name for SearchLine list
        c.Collection<SearchPlace>("places");       // JSON name for SearchPlace list
    });
});
```

### Behavior

- `RootPropertyName` overrides the JSON name of the root property (CLR name stays `Result`)
- `Collection<T>("name")` overrides the JSON name of that type's list property (CLR name stays `{Type}Dtos`)
- Types without a Collection override use default camelCase JSON naming

---

## Feature 3: Reference().JsonName()

### API

```csharp
public sealed class ReferenceBuilder
{
    public ReferenceBuilder JsonName(string jsonName);
}
```

On TypeBuilder:

```csharp
public ReferenceBuilder Reference(Expression<Func<T, object?>> selector);
public ReferenceBuilder ReferenceCollection(Expression<Func<T, object?>> selector);
```

### Usage

```csharp
builder.ForType<SearchHop>(x =>
{
    x.Reference(p => p.Line).JsonName("line");
    x.Reference(p => p.MarketingCarrier).JsonName("marketingCarrier");
    x.ReferenceCollection(p => p.TransitImages).JsonName("transitImages");
});
```

### Generated output

```csharp
public partial class SearchHopDto
{
    [JsonPropertyName("line")]           // overridden
    public int LineIndex { get; set; }

    [JsonPropertyName("marketingCarrier")]  // overridden
    public int? MarketingCarrierIndex { get; set; }

    [JsonPropertyName("transitImages")]     // overridden
    public int[] TransitImagesIndices { get; set; }
}
```

Without overrides, these would be `"lineIndex"`, `"marketingCarrierIndex"`, `"transitImagesIndices"`.

---

## Feature 4: [NormalizeJsonName] Attribute

### API

```csharp
[AttributeUsage(AttributeTargets.Property, Inherited = false)]
public sealed class NormalizeJsonNameAttribute : Attribute
{
    public string Name { get; }
    public NormalizeJsonNameAttribute(string name) => Name = name;
}
```

### Usage

```csharp
public class SearchHop
{
    [NormalizeJsonName("line")]
    public SearchLine Line { get; set; }
}
```

### Priority order

When both config and attribute exist on the same property, config wins:

1. `ForType<T>().Reference().JsonName()` (highest)
2. `[NormalizeJsonName]` attribute
3. Default camelCase

---

## Container shape: full search-response example

```csharp
// Config:
graph.UseJsonContract(c =>
{
    c.RootPropertyName = "result";
    c.Collection<SearchRoute>("routes");
    c.Collection<SearchLine>("lines");
    c.Collection<SearchPlace>("places");
    c.Collection<SearchCarrier>("carriers");
});

// Generated container:
public partial class SearchResponseResultDto
{
    [JsonPropertyName("result")]
    public SearchResponseDto Result { get; set; }

    [JsonPropertyName("routes")]
    public SearchRouteDto[] SearchRouteDtos { get; set; }

    [JsonPropertyName("lines")]
    public SearchLineDto[] SearchLineDtos { get; set; }

    [JsonPropertyName("places")]
    public SearchPlaceDto[] SearchPlaceDtos { get; set; }

    [JsonPropertyName("carriers")]
    public SearchCarrierDto[] SearchCarrierDtos { get; set; }

    // Types without Collection override use default naming:
    [JsonPropertyName("searchHopDtos")]
    public SearchHopDto[] SearchHopDtos { get; set; }
}
```

---

## Impact on Normalizer and Denormalizer

### Normalizer

- Still uses internal context collections for dedup/tracking (no change)
- When building the result container:
  - Sets `result.Result = rootDtoArray[0]`
  - Copies all other type collections to container lists
  - If root type IS referenced: also copies root collection to its list property
  - If root type is NOT referenced: skips the root's list (it doesn't exist on the container)

### Denormalizer

- Reads root from `normalized.Result` instead of `list[0]`
- Creates local resolution array: `var roots = new[] { normalized.Result }` for reference resolution (if root is referenced by other types but has no list)
- Or reads from `normalized.{RootType}Dtos` if the list exists
- All other types: unchanged (read from list properties)

---

## Implementation Changes

### Runtime library

| Change | File |
|---|---|
| New | `Configuration/JsonContractBuilder.cs` |
| New | `Configuration/ReferenceBuilder.cs` |
| New | `Attributes/NormalizeJsonNameAttribute.cs` |
| Modify | `Configuration/GraphBuilder.cs` -- add `UseJsonContract()` |
| Modify | `Configuration/TypeBuilder.cs` -- add `Reference()`, `ReferenceCollection()` |

### Source generator

| Change | File |
|---|---|
| New | `Models/JsonContractModel.cs` |
| Modify | `Models/NormalizationModel.cs` -- add JsonContractModel, PropertyJsonNameOverrides |
| Modify | `Models/AnalyzedProperty.cs` -- add JsonNameOverride |
| Modify | `Analysis/ConfigurationParser.cs` -- parse UseJsonContract, Reference().JsonName() |
| Modify | `Analysis/TypeGraphAnalyzer.cs` -- read [NormalizeJsonName], compute rootTypeNeedsList |
| Modify | `Emitters/ContainerEmitter.cs` -- root property, collection JSON names |
| Modify | `Emitters/NormalizerEmitter.cs` -- set Result, conditional root list |
| Modify | `Emitters/DenormalizerEmitter.cs` -- read from Result |
| Modify | `Emitters/DtoEmitter.cs` -- JsonNameOverride on properties |
| New | `DiagnosticDescriptors.cs` -- DN1001 for unparsed config statements |
