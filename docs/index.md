---
_layout: landing
---

# DataNormalizer

A .NET source generator that normalizes nested object graphs into flat, deduplicated representations.

[![NuGet](https://img.shields.io/nuget/v/DataNormalizer.svg)](https://www.nuget.org/packages/DataNormalizer)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://github.com/dibstern/DataNormalizer/blob/main/LICENSE)

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

## Get Started

```
dotnet add package DataNormalizer
```

- [Getting Started](articles/getting-started.md) — Quick tutorial
- [Configuration Guide](articles/configuration.md) — All configuration options
- [Diagnostics Reference](articles/diagnostics.md) — Compiler diagnostics DN0001–DN0004
- [API Reference](api/index.md) — Full API documentation

## Target Frameworks

The runtime library targets `net8.0`, `net9.0`, and `net10.0`. The source generator targets `netstandard2.0` (a Roslyn requirement) and is bundled in the same NuGet package.

## License

MIT — see [LICENSE](https://github.com/dibstern/DataNormalizer/blob/main/LICENSE).
