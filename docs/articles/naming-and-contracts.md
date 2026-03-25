# Naming & JSON Contracts

DataNormalizer generates DTOs with semantic CLR names (`RouteDto`, `HopDto`) and lets you configure JSON wire-format names independently. This separation means your C# code stays readable while your serialized output matches any existing API contract.

## Naming Policy

Control DTO type names with `UseNaming()`. Available at two levels:

**Global** (applies to all graphs):

```csharp
protected override void Configure(NormalizeBuilder builder)
{
    builder.UseNaming(n =>
    {
        n.DtoSuffix = "Model";      // RouteModel instead of RouteDto
        n.DtoPrefix = "";            // default — no prefix
        n.ContainerSuffix = "Dto";   // SearchResponseResultDto
        n.EmitJsonPropertyNames = true; // emit [JsonPropertyName] attributes
    });

    builder.NormalizeGraph<SearchResponse>();
}
```

**Per-graph** (overrides global for that graph):

```csharp
builder.UseNaming(n => n.DtoSuffix = "Record"); // global default

builder.NormalizeGraph<SearchResponse>(graph =>
{
    graph.UseNaming(n => n.DtoSuffix = "View"); // this graph uses "View"
});
```

### Properties

| Property | Default | Effect |
|---|---|---|
| `DtoPrefix` | `""` | Prefix for DTO types: `{Prefix}{TypeName}{Suffix}` |
| `DtoSuffix` | `"Dto"` | Suffix for DTO types. Set `""` for bare names |
| `ContainerSuffix` | `"Dto"` | Suffix for the container: `{Root}Result{ContainerSuffix}` |
| `EmitJsonPropertyNames` | `true` | Emit `[JsonPropertyName("camelCase")]` on all generated properties |

When `DtoSuffix` is empty, collection property names use a `List` fallback (e.g., `placeList` instead of `placeDtos`). The `DtoPrefix` does not apply to the container type name — containers always follow `{TypeName}Result{ContainerSuffix}`.

## JSON Contract Customization

`UseJsonContract()` controls the JSON property names on the container DTO itself. This is per-graph only:

```csharp
builder.NormalizeGraph<SearchResponse>(graph =>
{
    graph.UseJsonContract(c =>
    {
        c.RootPropertyName = "result";
        c.Collection<Route>("routes");
        c.Collection<Hop>("hops");
        c.Collection<Carrier>("carriers");
        c.Collection<Place>("places");
    });
});
```

### RootPropertyName

Sets the JSON name for the container's `Result` property. Default is `"result"`. The `Result` property always gets a `[JsonPropertyName]` attribute regardless of the `EmitJsonPropertyNames` setting.

### Collection\<T\>(jsonName)

Overrides the JSON property name for a type's list array in the container. Without it, the default name is `{typeName}{DtoSuffix}s` in camelCase (e.g., `"routeDtos"`). With `Collection<Route>("routes")`, it serializes as `"routes"`.

### Example JSON output

```json
{
  "result": { "routesIndices": [0], "originPlaceIndex": 0, ... },
  "routes": [ { "segmentsIndices": [0], ... } ],
  "hops":   [ { "line": 0, "marketingCarrier": 1, ... } ],
  "places": [ { "shortName": "London", "kind": "city", ... } ],
  "carriers": [ { "name": "Eurostar", "code": "ES" } ]
}
```

## Reference JSON Names

When a property is normalized into an index reference, the default JSON name is `{propertyName}Index` (or `{propertyName}Indices` for collections). Use `Reference().JsonName()` or `ReferenceCollection().JsonName()` to override this:

```csharp
builder.ForType<Hop>(x =>
{
    x.Reference(p => p.Line).JsonName("line");
    x.Reference(p => p.MarketingCarrier).JsonName("marketingCarrier");
    x.Reference(p => p.Vehicle).JsonName("vehicle");
    x.ReferenceCollection(p => p.TransitImages).JsonName("transitImages");
});
```

This generates a DTO like:

```csharp
public partial class HopDto : IEquatable<HopDto>
{
    [JsonPropertyName("line")]
    public int LineIndex { get; set; }              // JSON: "line" instead of "lineIndex"

    [JsonPropertyName("marketingCarrier")]
    public int? MarketingCarrierIndex { get; set; } // JSON: "marketingCarrier"

    [JsonPropertyName("vehicle")]
    public int VehicleIndex { get; set; }           // JSON: "vehicle"

    [JsonPropertyName("transitImages")]
    public int[] TransitImagesIndices { get; set; }  // JSON: "transitImages"
}
```

The CLR property names stay semantic (`LineIndex`, `MarketingCarrierIndex`), but the wire format uses the compact names expected by the API consumer.

## \[NormalizeJsonName\] Attribute

An attribute-based alternative to the fluent `Reference().JsonName()` API:

```csharp
public class Hop
{
    [NormalizeJsonName("line")]
    public Line Line { get; set; }

    [NormalizeJsonName("marketingCarrier")]
    public Carrier? MarketingCarrier { get; set; }
}
```

### Priority

When both are present, the fluent config wins:

1. `Reference().JsonName("name")` or `ReferenceCollection().JsonName("name")` — highest priority
2. `[NormalizeJsonName("name")]` — used if no fluent config exists
3. Default camelCase (`lineIndex`, `transitImagesIndices`) — used if neither is set

Explicit JSON name overrides are always emitted, even when `EmitJsonPropertyNames` is `false`. This lets you turn off automatic camelCase attributes globally while still controlling specific properties.

## Matching an Existing Wire Format

Combining all three features to match a transport search API:

```csharp
[NormalizeConfiguration]
public partial class SearchConfig : NormalizationConfig
{
    protected override void Configure(NormalizeBuilder builder)
    {
        // 1. Naming: keep default "Dto" suffix for CLR types
        builder.UseNaming(n =>
        {
            n.EmitJsonPropertyNames = true; // camelCase JSON (default)
        });

        // 2. JSON contract: custom collection names on the container
        builder.NormalizeGraph<SearchResponse>(graph =>
        {
            graph.UseJsonContract(c =>
            {
                c.RootPropertyName = "result";
                c.Collection<Route>("routes");
                c.Collection<Hop>("hops");
                c.Collection<Line>("lines");
                c.Collection<Place>("places");
                c.Collection<Carrier>("carriers");
            });
        });

        // 3. Per-property reference names on Hop
        builder.ForType<Hop>(x =>
        {
            x.Reference(p => p.Line).JsonName("line");
            x.Reference(p => p.MarketingCarrier).JsonName("marketingCarrier");
            x.ReferenceCollection(p => p.TransitImages).JsonName("transitImages");
        });
    }
}
```

The result serializes as:

```json
{
  "result": { "routesIndices": [0], "originPlaceIndex": 0, "destinationPlaceIndex": 1 },
  "routes": [ { "segmentsIndices": [0], "placesIndices": [0, 2] } ],
  "hops":   [ { "line": 0, "marketingCarrier": 0, "transitImages": [0] } ],
  "lines":  [ { "path": "M51.5,-0.1 L48.8,2.3", "placesIndices": [0, 2, 1] } ],
  "places": [ { "shortName": "London" }, { "shortName": "Paris" }, { "shortName": "Brussels" } ],
  "carriers": [ { "name": "Eurostar", "code": "ES" } ]
}
```

The same container round-trips through `JsonSerializer.Serialize` and `Deserialize` with no configuration needed on the System.Text.Json side.

## Root Property Behavior

Every container has a `Result` property (C# name). Its JSON name defaults to `"result"` and can be changed via `UseJsonContract(c => c.RootPropertyName = "data")`.

Whether the root type also gets a list array depends on usage:

| Scenario | Result property | Root list array |
|---|---|---|
| Root is **not** referenced by other types | Yes | No |
| Root **is** referenced by other types | Yes | Yes |

**No list** — `SearchResponse` is the root and no other type references it. The container has `Result` but no `SearchResponseDtos` array. The root DTO is only accessible through `Result`.

**Has list** — `TransportNetwork` is the root, but `TransportLine` has a `Network` property that references it back. Because the root type appears in another type's graph, it needs a collection for index lookups. The container has both `Result` and `TransportNetworkDtos`.

```csharp
// No back-reference: Result only
var search = SearchConfig.Normalize(response);
search.Result           // the root DTO
// search has no SearchResponseDtos property

// Back-reference: Result + list
var transport = TransportConfig.Normalize(network);
transport.Result                 // the root DTO (always index 0)
transport.TransportNetworkDtos   // includes the root for index resolution
```

This optimization avoids a redundant array when the root is never referenced by index from another type.
