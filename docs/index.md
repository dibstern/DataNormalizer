---
---

# DataNormalizer

A .NET source generator that normalizes nested object graphs into flat, deduplicated, JSON-serializable containers.

[![CI](https://github.com/dibstern/DataNormalizer/actions/workflows/ci.yml/badge.svg)](https://github.com/dibstern/DataNormalizer/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/DataNormalizer.svg)](https://www.nuget.org/packages/DataNormalizer)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://github.com/dibstern/DataNormalizer/blob/main/LICENSE)

## What It Does

**Before** — nested routes with repeated carriers and places:

```csharp
var seattle = new Place { Name = "Seattle" };
var carrier = new Carrier { Name = "FastFreight" };

var routes = new[]
{
    new Route
    {
        Hops = new[]
        {
            new Hop { Origin = seattle, Destination = new Place { Name = "Portland" }, Carrier = carrier },
            new Hop { Origin = new Place { Name = "Portland" }, Destination = new Place { Name = "Denver" }, Carrier = carrier },
        },
    },
};
```

**After** — flat, deduplicated container with integer index references:

```json
{
  "result": [0],
  "routeDtos": [
    { "hopsIndices": [0, 1] }
  ],
  "hopDtos": [
    { "originIndex": 0, "destinationIndex": 1, "carrierIndex": 0 },
    { "originIndex": 1, "destinationIndex": 2, "carrierIndex": 0 }
  ],
  "carrierDtos": [
    { "name": "FastFreight" }
  ],
  "placeDtos": [
    { "name": "Seattle" },
    { "name": "Portland" },
    { "name": "Denver" }
  ]
}
```

Shared `Carrier` and `Place` instances are stored once. References become integer indices into typed arrays.

## Get Started

```
dotnet add package DataNormalizer
```

- [Getting Started](articles/getting-started.md) — Quick tutorial
- [Configuration Guide](articles/configuration.md) — All configuration options
- [Naming & JSON Contracts](articles/naming-and-contracts.md) — Dto suffixes, camelCase JSON, and wire format customization
- [Why Gzip Isn't Enough](articles/why-gzip-isnt-enough.md) — Why structural dedup beats compression alone
- [Diagnostics Reference](articles/diagnostics.md) — Compiler diagnostics DN0001–DN1002
- [API Reference](api/index.md) — Full API documentation

## Target Frameworks

The runtime library targets `net6.0`, `net7.0`, `net8.0`, `net9.0`, and `net10.0`. The source generator targets `netstandard2.0` (a Roslyn requirement) and is bundled in the same NuGet package.

## License

MIT — see [LICENSE](https://github.com/dibstern/DataNormalizer/blob/main/LICENSE).
