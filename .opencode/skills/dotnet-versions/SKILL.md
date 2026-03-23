---
name: dotnet-versions
description: Multi-targeting .NET 6/7/8/9/10 and netstandard2.0 for the DataNormalizer project. Covers TargetFrameworks configuration, conditional compilation with preprocessor directives, TFM-specific APIs, test project targeting, and CI SDK installation.
---

# Multi-Targeting .NET Versions

## Project Target Frameworks

### Runtime Library (DataNormalizer)

```xml
<PropertyGroup>
  <TargetFrameworks>net6.0;net7.0;net8.0;net9.0;net10.0</TargetFrameworks>
</PropertyGroup>
```

The runtime library multi-targets to support consumers on any of these frameworks. Each TFM produces a separate DLL in the NuGet package under `lib/net6.0/`, `lib/net7.0/`, `lib/net8.0/`, `lib/net9.0/`, `lib/net10.0/`.

### Source Generator (DataNormalizer.Generators)

```xml
<PropertyGroup>
  <TargetFramework>netstandard2.0</TargetFramework>
</PropertyGroup>
```

**Critical:** Source generators MUST target `netstandard2.0`. This is a Roslyn requirement — the compiler loads generators into its own process, which runs on netstandard2.0. Targeting anything else will cause the generator to fail to load.

PolySharp is used to provide C# 12 features (init-only properties, records, nullable attributes) on netstandard2.0.

### Test Projects

```xml
<PropertyGroup>
  <TargetFramework>net8.0</TargetFramework>
</PropertyGroup>
```

Test projects target a single TFM for simplicity. Use `net8.0` as the baseline (LTS release). The CI pipeline installs all required SDKs so the multi-targeting runtime library builds correctly.

### Sample Project

```xml
<PropertyGroup>
  <TargetFramework>net8.0</TargetFramework>
</PropertyGroup>
```

## Conditional Compilation

### Preprocessor Directives

Use `#if` directives when behavior differs between frameworks:

```csharp
// Feature available only on .NET 9+
#if NET9_0_OR_GREATER
    public void UseNet9Feature()
    {
        // .NET 9 specific API
        var frozen = collection.ToFrozenSet();
    }
#endif

// Feature available on .NET 8+
#if NET8_0_OR_GREATER
    public void UseNet8Feature()
    {
        // .NET 8 specific API (also available in 9, 10)
        var time = TimeProvider.System.GetUtcNow();
    }
#endif
```

### Available Directives

| Directive | True When Targeting |
|-----------|-------------------|
| `NET8_0` | Exactly net8.0 |
| `NET8_0_OR_GREATER` | net8.0, net9.0, net10.0 |
| `NET9_0` | Exactly net9.0 |
| `NET9_0_OR_GREATER` | net9.0, net10.0 |
| `NET10_0` | Exactly net10.0 |
| `NET10_0_OR_GREATER` | net10.0 |
| `NETSTANDARD2_0` | netstandard2.0 |

### Polyfill Pattern

When a newer API isn't available on older frameworks:

```csharp
namespace DataNormalizer.Runtime;

public sealed class NormalizationContext
{
    private readonly HashSet<object> _visited;

    public NormalizationContext()
    {
#if NET8_0_OR_GREATER
        _visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
#else
        // Fallback for netstandard2.0 (if ever needed)
        _visited = new HashSet<object>(new ObjectReferenceComparer());
#endif
    }
}
```

### Conditional Package References

Reference packages only for specific TFMs:

```xml
<ItemGroup Condition="'$(TargetFramework)' == 'net8.0'">
  <PackageReference Include="System.Text.Json" />
</ItemGroup>
```

## TFM-Specific APIs

### .NET 8 APIs (Available to All Targets)

```csharp
// TimeProvider (abstract time for testing)
TimeProvider.System.GetUtcNow();

// FrozenDictionary / FrozenSet
var frozen = dictionary.ToFrozenDictionary();

// SearchValues for efficient character searching
var vowels = SearchValues.Create("aeiou");

// Keyed DI services
services.AddKeyedSingleton<IFoo, Foo>("key");
```

### .NET 9 APIs

```csharp
#if NET9_0_OR_GREATER
// OrderedDictionary<TKey, TValue> (new in .NET 9)
var ordered = new OrderedDictionary<string, int>();

// LINQ CountBy
var counts = items.CountBy(x => x.Category);

// LINQ AggregateBy
var sums = items.AggregateBy(x => x.Category, 0, (sum, x) => sum + x.Value);
#endif
```

## DataNormalizer-Specific Considerations

### Runtime Library API Surface

Keep the public API compatible across all targeted frameworks. Avoid exposing TFM-specific types in public APIs:

```csharp
// WRONG - FrozenDictionary is .NET 8+ only
public FrozenDictionary<string, int> GetIndices() { }

// CORRECT - use interface that exists on all targets
public IReadOnlyDictionary<string, int> GetIndices() { }

// CORRECT - use FrozenDictionary internally, expose as IReadOnlyDictionary
public IReadOnlyDictionary<string, int> GetIndices()
{
#if NET8_0_OR_GREATER
    return _indices.ToFrozenDictionary();
#else
    return _indices;
#endif
}
```

### Generator Does NOT Multi-Target

The generator runs in the compiler process and targets `netstandard2.0` only. It doesn't need `#if` directives for framework features — it generates code that targets the consumer's framework.

However, the **generated code** might need `#if` directives:

```csharp
// The generator emits this code
var source = $$"""
    #if NET8_0_OR_GREATER
    private static readonly FrozenDictionary<string, int> _lookup = ...;
    #else
    private static readonly Dictionary<string, int> _lookup = ...;
    #endif
    """;
```

## CI SDK Installation

GitHub Actions needs all targeted SDKs installed:

```yaml
- name: Setup .NET
  uses: actions/setup-dotnet@v4
  with:
    dotnet-version: |
      8.0.x
      9.0.x
      10.0.x
```

This ensures `dotnet build` can compile all TFMs in the runtime library.

## Common Pitfalls

### 1. Missing SDK in CI

If CI fails with "framework not found" errors, ensure all required SDKs are installed in the workflow.

### 2. API Not Available on Lower TFM

If you use a .NET 9 API without `#if NET9_0_OR_GREATER`, the `net8.0` build will fail. Always guard TFM-specific APIs.

### 3. Generator Targeting Wrong Framework

If the generator doesn't target `netstandard2.0`, it won't load in the compiler. The error message is often obscure (generator silently doesn't run).

### 4. Test Project Multi-Targeting

Don't multi-target test projects unless you specifically need to test framework-specific behavior. A single `net8.0` target is sufficient for most tests. Running tests on multiple TFMs adds CI time with little benefit.

### 5. TargetFramework vs TargetFrameworks

```xml
<!-- Single target - no 's' -->
<TargetFramework>netstandard2.0</TargetFramework>

<!-- Multiple targets - with 's', semicolon separated -->
<TargetFrameworks>net8.0;net9.0;net10.0</TargetFrameworks>
```

Using `<TargetFramework>` (singular) with multiple values will cause a build error. Using `<TargetFrameworks>` (plural) with a single value works but is unconventional.
