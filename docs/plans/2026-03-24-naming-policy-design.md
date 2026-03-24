# Naming Policy System Design

**Date:** 2026-03-24

**Goal:** Add a configurable naming policy system to DataNormalizer so generated DTOs, containers, and JSON property names can match existing wire formats or custom conventions.

**Context:** The ChatGPT discussion in `docs/plans/2026-03-24-chatgpt-discussion.md` identified that DataNormalizer's current opinionated naming (`NormalizedPerson`, `PersonList`, `HomeAddressIndex`) prevents adoption by teams with existing API contracts. This design adds configurable naming while preserving semantic CLR names.

---

## Design Principles

1. **Semantic CLR names, configurable JSON names** -- CLR properties stay self-documenting (`LineIndex`, `TransitImagesIndices`), while `[JsonPropertyName]` attributes control wire format.
2. **Builder-based configuration** -- Global naming defaults via `builder.UseNaming()`, per-graph overrides via `graph.UseNaming()`, per-property overrides via `ForType<T>().Reference().JsonName()`.
3. **Domain attributes as alternative** -- `[NormalizeJsonName("line")]` on domain properties for teams that prefer collocated config.
4. **No backward compatibility concern** -- Library has no production consumers yet.

---

## New Defaults

| Aspect | Old Default | New Default |
|---|---|---|
| DTO type name | `NormalizedPerson` | `PersonDto` |
| Container type name | `NormalizedPersonResult` | `PersonResultDto` |
| Container list property | `PersonList` | `PersonDtos` |
| `[JsonPropertyName]` emission | Off (unless `UseJsonNaming`) | Always on (camelCase) |
| Reference property CLR name | `HomeAddressIndex` | `HomeAddressIndex` (unchanged) |
| Collection ref CLR name | `PhoneNumbersIndices` | `PhoneNumbersIndices` (unchanged) |

---

## API Design

### NamingBuilder

```csharp
public sealed class NamingBuilder
{
    public string DtoPrefix { get; set; } = "";
    public string DtoSuffix { get; set; } = "Dto";
    public string ContainerSuffix { get; set; } = "Dto";
    public bool EmitJsonPropertyNames { get; set; } = true;
}
```

### Usage on NormalizeBuilder (global defaults)

```csharp
builder.UseNaming(n =>
{
    n.DtoPrefix = "";
    n.DtoSuffix = "Dto";
    n.ContainerSuffix = "Dto";
    n.EmitJsonPropertyNames = true;
});
```

### Usage on GraphBuilder (per-graph override)

```csharp
builder.NormalizeGraph<SearchResponse>(graph =>
{
    graph.UseNaming(n =>
    {
        n.ContainerSuffix = "Container";
    });
});
```

### JsonContractBuilder (per-graph JSON contract)

```csharp
public sealed class JsonContractBuilder
{
    public string? RootPropertyName { get; set; }
    public void Collection<T>(string jsonName);
}
```

Usage:

```csharp
graph.UseJsonContract(c =>
{
    c.RootPropertyName = "result";
    c.Collection<SearchRoute>("routes");
    c.Collection<SearchLine>("lines");
    c.Collection<SearchPlace>("places");
});
```

### Per-Property Reference Naming (ForType)

```csharp
public sealed class ReferenceBuilder
{
    public ReferenceBuilder JsonName(string jsonName);
}
```

On TypeBuilder:

```csharp
builder.ForType<SearchHop>(x =>
{
    x.Reference(p => p.Line).JsonName("line");
    x.Reference(p => p.MarketingCarrier).JsonName("marketingCarrier");
    x.ReferenceCollection(p => p.TransitImages).JsonName("transitImages");
});
```

### Domain Attribute Alternative

```csharp
[AttributeUsage(AttributeTargets.Property)]
public sealed class NormalizeJsonNameAttribute : Attribute
{
    public string Name { get; }
    public NormalizeJsonNameAttribute(string name) => Name = name;
}
```

Usage on domain types:

```csharp
public class SearchHop
{
    [NormalizeJsonName("line")]
    public SearchLine Line { get; set; }
}
```

---

## Generated Output Examples

### With custom config (search response use case)

```csharp
// DTO
public partial class SearchHopDto : IEquatable<SearchHopDto>
{
    [JsonPropertyName("line")]
    public int LineIndex { get; set; }

    [JsonPropertyName("marketingCarrier")]
    public int? MarketingCarrierIndex { get; set; }

    [JsonPropertyName("vehicle")]
    public int VehicleIndex { get; set; }

    [JsonPropertyName("transitImages")]
    public int[] TransitImagesIndices { get; set; } = Array.Empty<int>();
}

// Container
public partial class SearchResponseResultDto
{
    [JsonPropertyName("result")]
    public SearchResponseDto Root { get; set; } = default!;

    [JsonPropertyName("routes")]
    public SearchRouteDto[] SearchRouteDtos { get; set; } = Array.Empty<SearchRouteDto>();

    [JsonPropertyName("lines")]
    public SearchLineDto[] SearchLineDtos { get; set; } = Array.Empty<SearchLineDto>();
}
```

### With defaults (no config)

```csharp
// DTO
public partial class PersonDto : IEquatable<PersonDto>
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = default!;

    [JsonPropertyName("homeAddressIndex")]
    public int HomeAddressIndex { get; set; }
}

// Container
public partial class PersonResultDto
{
    [JsonPropertyName("personDtos")]
    public PersonDto[] PersonDtos { get; set; } = Array.Empty<PersonDto>();

    [JsonPropertyName("addressDtos")]
    public AddressDto[] AddressDtos { get; set; } = Array.Empty<AddressDto>();
}
```

---

## Naming Resolution Order

For each naming decision, the generator checks in this order (first match wins):

1. **Per-property override** -- `ForType<T>().Reference().JsonName()` or `[NormalizeJsonName]` attribute
2. **Per-graph JSON contract** -- `UseJsonContract(c => c.Collection<T>("name"))`
3. **Per-graph naming** -- `graph.UseNaming()`
4. **Global naming** -- `builder.UseNaming()`
5. **Built-in defaults** -- `DtoPrefix=""`, `DtoSuffix="Dto"`, etc.

---

## Implementation Changes

### Runtime library (src/DataNormalizer/)

| Change | File |
|---|---|
| New | `Configuration/NamingBuilder.cs` |
| New | `Configuration/JsonContractBuilder.cs` |
| New | `Configuration/ReferenceBuilder.cs` |
| New | `Attributes/NormalizeJsonNameAttribute.cs` |
| Modify | `Configuration/NormalizeBuilder.cs` -- add `UseNaming()` |
| Modify | `Configuration/GraphBuilder.cs` -- add `UseNaming()`, `UseJsonContract()` |
| Modify | `Configuration/TypeBuilder.cs` -- add `Reference()`, `ReferenceCollection()` |

### Source generator (src/DataNormalizer.Generators/)

| Change | File |
|---|---|
| New | `Models/NamingModel.cs` -- equatable record |
| New | `Models/JsonContractModel.cs` -- equatable record |
| Modify | `Models/NormalizationModel.cs` -- add NamingModel, JsonContractModel |
| Modify | `Models/AnalyzedProperty.cs` -- add JsonNameOverride |
| Modify | `Analysis/ConfigurationParser.cs` -- parse new config syntax |
| Modify | `Analysis/TypeGraphAnalyzer.cs` -- read [NormalizeJsonName] attributes |
| Modify | `Emitters/EmitterHelpers.cs` -- new naming methods |
| Modify | `Emitters/DtoEmitter.cs` -- use NamingModel |
| Modify | `Emitters/ContainerEmitter.cs` -- use NamingModel + JsonContractModel |
| Modify | `Emitters/NormalizerEmitter.cs` -- use new type names |
| Modify | `Emitters/DenormalizerEmitter.cs` -- use new type names + list names |

### Tests

- Update all existing tests for new defaults
- New tests for UseNaming() configuration
- New tests for UseJsonContract() configuration
- New tests for Reference().JsonName() / ReferenceCollection().JsonName()
- New tests for [NormalizeJsonName] attribute
- Integration roundtrip tests with custom naming

---

## Future Work (not in this iteration)

- **README improvements** -- "Why gzip isn't enough" section, better problem statement, real-world example
- **JSON contract configuration** -- more advanced root envelope shaping
- **Custom dedupe comparers** -- value equality vs reference identity vs custom key selectors
