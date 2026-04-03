# DataNormalizer

Compile-time graph normalization for .NET. Generate flat, deduplicated API contracts from nested object graphs.

[![CI](https://github.com/dibstern/DataNormalizer/actions/workflows/ci.yml/badge.svg)](https://github.com/dibstern/DataNormalizer/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/DataNormalizer.svg)](https://www.nuget.org/packages/DataNormalizer)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

[Documentation](https://dibstern.github.io/DataNormalizer/) | [API Reference](https://dibstern.github.io/DataNormalizer/api/)

## The Problem

APIs that return nested object trees repeat the same entities over and over. A search response with 20 routes through 5 airports serializes each airport up to 40 times. That costs bytes on the wire, redundant parsing work on every client, and hand-written flatten/rehydrate code to clean it up.

## What DataNormalizer Does

DataNormalizer is a source generator. You point it at your root type and it produces flat, deduplicated DTOs, a typed container, and `Normalize`/`Denormalize` methods — all at compile time, zero reflection.

Given a transport search response with routes, hops, carriers, and places, the normalized JSON looks like this:

```json
{
  "result": {
    "routesIndices": [0, 1],
    "originIndex": 0,
    "destinationIndex": 1
  },
  "routeDtos": [
    { "name": "Fly", "hopsIndices": [0, 1] },
    { "name": "Train", "hopsIndices": [2] }
  ],
  "hopDtos": [
    { "carrierIndex": 0, "departureIndex": 0, "arrivalIndex": 2, "durationMinutes": 30 },
    { "carrierIndex": 1, "departureIndex": 2, "arrivalIndex": 1, "durationMinutes": 85 },
    { "carrierIndex": 2, "departureIndex": 0, "arrivalIndex": 1, "durationMinutes": 660 }
  ],
  "carrierDtos": [
    { "name": "SkyBus", "code": "SKYBUS" },
    { "name": "Qantas", "code": "QF" },
    { "name": "NSW TrainLink", "code": "XPT" }
  ],
  "placeDtos": [
    { "name": "Melbourne", "lat": -37.814, "lng": 144.963 },
    { "name": "Sydney", "lat": -33.865, "lng": 151.207 },
    { "name": "Melbourne Airport", "lat": -37.670, "lng": 144.849 }
  ]
}
```

Melbourne appears once. Carriers are stored once. Each hop references them by index.

## Normalization + Gzip

"Just gzip it" removes syntactic redundancy, but normalization removes *semantic* redundancy first — then gzip compresses what's left even further.

Measured on a real transport search API response:

| Format | Raw | Gzipped |
|--------|-----|---------|
| Normalized | 345 KB | 58 KB |
| Unnormalized | 713 KB | 121 KB |

**Raw savings: 368 KB (2.1x smaller). Gzipped savings: 63 KB (2.1x smaller).**

Normalize first, gzip second. See [Why Gzip Isn't Enough](https://dibstern.github.io/DataNormalizer/articles/why-gzip-isnt-enough.html) for the full analysis.

## Quick Start

```
dotnet add package DataNormalizer
```

### 1. Define your types

```csharp
public class SearchResponse
{
    public Route[] Routes { get; set; }
    public Place Origin { get; set; }
    public Place Destination { get; set; }
}

public class Route
{
    public string Name { get; set; }
    public Hop[] Hops { get; set; }
}

public class Hop
{
    public Carrier Carrier { get; set; }
    public Place Departure { get; set; }
    public Place Arrival { get; set; }
    public int DurationMinutes { get; set; }
}

public class Carrier { public string Name { get; set; } public string Code { get; set; } }
public class Place { public string Name { get; set; } public double Lat { get; set; } public double Lng { get; set; } }
```

### 2. Create a configuration class

```csharp
using DataNormalizer.Attributes;
using DataNormalizer.Configuration;

[NormalizeConfiguration]
public partial class SearchNormalizer : NormalizationConfig
{
    protected override void Configure(NormalizeBuilder builder)
    {
        builder.NormalizeGraph<SearchResponse>(); // discovers Route, Hop, Carrier, Place
    }
}
```

### 3. Normalize and denormalize

```csharp
var result = SearchNormalizer.Normalize(searchResponse);

// Access the root directly
Console.WriteLine(result.Result.RoutesIndices.Length); // 2

// Access typed collections
Console.WriteLine(result.RouteDtos.Length);    // 2
Console.WriteLine(result.CarrierDtos.Length);  // 3 (deduplicated)
Console.WriteLine(result.PlaceDtos.Length);    // 3 (deduplicated)

// Serialize to JSON
var json = JsonSerializer.Serialize(result);

// Denormalize back to the original object graph
var restored = SearchNormalizer.Denormalize(result);
```

## When To Use It

**Good for:**
- Graph-like data where entities repeat across branches (routes, hops, places)
- High-volume APIs where payload size and parse time matter
- Replacing hand-written normalization or ad-hoc deduplication logic

**Not ideal for:**
- Tiny payloads where overhead isn't meaningful
- Simple flat lists with no shared references
- Very specific legacy wire formats you can't change

## Documentation

- [Getting Started](https://dibstern.github.io/DataNormalizer/docs/getting-started.html)
- [Configuration Guide](https://dibstern.github.io/DataNormalizer/docs/configuration-guide.html)
- [Naming & JSON Contracts](https://dibstern.github.io/DataNormalizer/docs/naming-and-json-contracts.html)
- [Why Gzip Isn't Enough](https://dibstern.github.io/DataNormalizer/docs/why-gzip-isnt-enough.html)
- [Diagnostics Reference](https://dibstern.github.io/DataNormalizer/docs/diagnostics-reference.html)
- [API Reference](https://dibstern.github.io/DataNormalizer/api/)

## Target Frameworks

| Component | Targets |
|---|---|
| Runtime library | `net6.0`, `net7.0`, `net8.0`, `net9.0`, `net10.0` |
| Source generator | `netstandard2.0` (Roslyn requirement, bundled in NuGet package) |

## License

MIT — see [LICENSE](LICENSE).
