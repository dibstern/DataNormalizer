# DataNormalizer v1.0 Design

## Summary

DataNormalizer is a .NET source generator library that transforms nested object graphs into flat, deduplicated normalized representations. It publishes to NuGet as a single package (`DataNormalizer`) containing both the runtime types and the Roslyn source generator.

**Inspired by:** A proprietary dynamic data normalization library that used runtime expression trees for hash-based deduplication. This rewrite replaces the runtime approach with compile-time source generation for better performance, type safety, and developer experience.

---

## Core Concept

Given nested domain types where the same objects appear multiple times (e.g., shared addresses, repeated geographic nodes across routes), DataNormalizer:

1. Generates flat DTO classes where nested objects are replaced with integer index references
2. Deduplicates identical objects (by equality) so each unique object is stored once
3. Produces a `NormalizedResult<T>` containing the root DTO plus flat collections of all referenced types
4. Provides denormalization to reconstruct the original object graph from the normalized representation

---

## Architecture: Pure Source Generator

### Approach

Everything is generated at compile time via a Roslyn `IIncrementalGenerator`. No runtime reflection, no expression trees. The generator analyzes the type graph and emits:

- Normalized DTO classes (partial, with index properties)
- `IEquatable<T>` implementations for deduplication
- `Normalize()` and `Denormalize()` static methods on the configuration class

### Why Source Generator Over Runtime Expression Trees

- Maximum performance (no reflection at runtime)
- Full IntelliSense on generated types
- Compile-time cycle detection with diagnostics
- AOT-friendly (no dynamic code generation)
- Type safety at compile time

---

## Target Frameworks

- **Runtime library (`DataNormalizer`):** `net8.0;net9.0;net10.0`
- **Source generator (`DataNormalizer.Generators`):** `netstandard2.0` (Roslyn requirement)
- **Shipped as single NuGet package:** `DataNormalizer` bundles the generator as an analyzer

---

## Configuration: Fluent Builder API

### Auto-Discovery (Simplest Case)

```csharp
[NormalizeConfiguration]
public partial class AppNormalization : NormalizationConfig
{
    protected override void Configure(NormalizeBuilder builder)
    {
        builder.NormalizeGraph<Person>();
    }
}
```

Generator walks `Person`'s type graph and generates normalized DTOs for every complex type found.

### Auto-Discovery with Opt-Out

```csharp
[NormalizeConfiguration]
public partial class AppNormalization : NormalizationConfig
{
    protected override void Configure(NormalizeBuilder builder)
    {
        builder.NormalizeGraph<Person>(graph =>
        {
            graph.Inline<Metadata>();  // Keep inline, don't normalize

            graph.ForType<Person>(p =>
            {
                p.IgnoreProperty(x => x.InternalId);  // Exclude from equality
            });
        });
    }
}
```

### Explicit Opt-In

```csharp
[NormalizeConfiguration]
public partial class AppNormalization : NormalizationConfig
{
    protected override void Configure(NormalizeBuilder builder)
    {
        builder.ForType<Person>(p =>
        {
            p.NormalizeProperty(x => x.HomeAddress);
            p.NormalizeProperty(x => x.WorkAddress);
            p.InlineProperty(x => x.Meta);
            p.IgnoreProperty(x => x.InternalId);
        });

        builder.ForType<Address>();
    }
}
```

### Multiple Graphs

```csharp
builder.NormalizeGraph<PersonDTO>();
builder.NormalizeGraph<TripDTO>(graph =>
{
    graph.Inline<GeoPositionDTO>();
});
```

### Serialization Attribute Support

```csharp
builder.NormalizeGraph<Person>(graph =>
{
    graph.CopySourceAttributes();             // Copy [JsonPropertyName] etc. from source types
    graph.UseJsonNaming(JsonNamingPolicy.CamelCase);  // Convention for generated/unattributed properties
});
```

- Properties with explicit serialization attributes on the source type: copied as-is
- Generated index/array properties (no source equivalent): named via convention
- Source properties without attributes: named via convention

---

## Generated Code Shape

### Generated DTO Classes

For `builder.NormalizeGraph<Person>()` where Person has Address and PhoneNumber:

```csharp
// NormalizedPerson.g.cs
[GeneratedCode("DataNormalizer", "1.0.0")]
public partial class NormalizedPerson : IEquatable<NormalizedPerson>
{
    public string Name { get; set; }
    public int Age { get; set; }
    public int HomeAddressIndex { get; set; }
    public int WorkAddressIndex { get; set; }
    public int[] PhoneNumberIndices { get; set; }

    public bool Equals(NormalizedPerson? other) { /* generated */ }
    public override bool Equals(object? obj) => obj is NormalizedPerson other && Equals(other);
    public override int GetHashCode() { /* generated */ }
}
```

### Generated Config Partial

```csharp
// AppNormalization.g.cs
public partial class AppNormalization
{
    public static NormalizedResult<NormalizedPerson> Normalize(Person source)
    {
        var context = new NormalizationContext();
        // generated normalization logic: hash, dedup, index assignment
        return new NormalizedResult<NormalizedPerson>(root, context);
    }

    public static Person Denormalize(NormalizedResult<NormalizedPerson> result)
    {
        // generated denormalization: resolve indices, reconstruct graph
    }
}
```

### Consumer Usage

```csharp
var person = new Person { ... };

// Normalize
var result = AppNormalization.Normalize(person);
var root = result.Root;
var addresses = result.GetCollection<NormalizedAddress>();

// Denormalize
Person reconstructed = AppNormalization.Denormalize(result);
```

### Partial Class Extensibility

Generated DTOs are `partial`, so users can extend them:

```csharp
public partial class NormalizedPerson
{
    public bool IsAdult => Age >= 18;
    public string ToSummary() => $"{Name}, age {Age}";
}
```

---

## Property Handling Rules

| Source Property Type | Generated Property | Example |
|---------------------|-------------------|---------|
| Simple (int, string, DateTime, enum, etc.) | Inline, same type | `string Name` -> `string Name` |
| Normalizable complex type (has [Normalize] or in graph) | `int {Name}Index` | `Address Home` -> `int HomeIndex` |
| Collection of normalizable type | `int[] {Name}Indices` | `List<Phone> Phones` -> `int[] PhoneIndices` |
| Complex type NOT in graph (inlined) | Same type, kept as-is | `Metadata Meta` -> `Metadata Meta` |
| Collection of simple type | Inline, same type | `List<string> Tags` -> `string[] Tags` |

---

## Equality & Hashing Strategy

Layered approach for deduplication:

1. **IEquatable<T> delegation:** If the source type implements `IEquatable<T>`, the generated equality delegates to the source type's equality during normalization.

2. **Default (IncludeAll):** All public properties participate in hash/equality. Users can exclude with `p.IgnoreProperty(x => x.Prop)`.

3. **Opt-in (ExplicitOnly):** Only properties explicitly included via `p.IncludeProperty(x => x.Prop)` participate.

### Hash Algorithm

Same as the original library: rolling hash with prime multiplication:
```
hashCode = (hashCode * 397) ^ propertyValue.GetHashCode()
```

For index properties, the index value (int) is used. For collections, sequence hashing over all elements.

---

## Circular Reference Handling

### Detection

The generator performs depth-first traversal of the type graph. When it encounters a type already being processed, it:

1. Emits compiler warning **DN0001**: `Circular reference detected: {TypeA}.{Property} -> {TypeB} -> ... -> {TypeA}`
2. Marks the back-edge property for special handling

### Normalization with Cycles

- Generated `Normalize()` uses a visited-set (`HashSet<object>` keyed by reference) to break cycles
- When a cycle is detected at runtime, the object is looked up in the existing index table (it was already normalized earlier in the traversal)
- The index reference is stored, just like any other normalizable property

### Denormalization with Cycles

- Two-pass approach:
  1. First pass: create all objects, populate simple properties and index references
  2. Second pass: resolve all index references to actual object references
- This naturally handles cycles because all objects exist before any references are resolved

---

## Source Generator Internals

### Pipeline (IIncrementalGenerator)

```
1. Syntax Provider
   - Find classes inheriting NormalizationConfig with [NormalizeConfiguration]
   - Extract Configure method body syntax

2. Semantic Analysis
   - Resolve type arguments in builder.NormalizeGraph<T>() calls
   - Parse lambda member access expressions (x => x.Property)
   - Build configuration model: which types, which properties, inline/normalize/ignore

3. Type Graph Analysis
   - For each root type: DFS traversal of all properties
   - Classify each property: simple, normalizable, collection, inlined
   - Detect circular references, record cycle edges
   - Resolve IEquatable<T> implementations on source types

4. Diagnostics Emission
   - DN0001 (Warning): Circular reference detected
   - DN0002 (Error): Configuration class must be partial
   - DN0003 (Error): Source type has no public properties
   - DN0004 (Info): Unmapped complex type will be inlined

5. Code Emission (per type in graph)
   - DTO class with properties, IEquatable<T>, GetHashCode
   - Serialization attributes (copied or convention-based)

6. Code Emission (per configuration class)
   - Normalize() method
   - Denormalize() method
```

---

## Solution Structure

```
DataNormalizer/
├── .opencode/
│   ├── skills/
│   │   ├── dotnet-guidelines/SKILL.md
│   │   ├── dotnet-patterns/SKILL.md
│   │   ├── dotnet-architecture/SKILL.md
│   │   ├── dotnet-backend-patterns/SKILL.md
│   │   ├── ddd-patterns/SKILL.md
│   │   ├── dotnet-tdd/SKILL.md
│   │   ├── centralized-packages/SKILL.md
│   │   ├── nuget-packaging/SKILL.md
│   │   ├── csharpier/SKILL.md
│   │   ├── source-generator-dev/SKILL.md
│   │   ├── legacy-normalizer/SKILL.md
│   │   ├── testcontainers/SKILL.md
│   │   └── dotnet-versions/SKILL.md
│   └── agents/
│       └── AGENTS.md
├── docs/
│   └── plans/
├── src/
│   ├── DataNormalizer/                    # Runtime library (net8.0;net9.0;net10.0)
│   │   ├── Attributes/
│   │   │   ├── NormalizeConfigurationAttribute.cs
│   │   │   ├── NormalizeIgnoreAttribute.cs
│   │   │   └── NormalizeIncludeAttribute.cs
│   │   ├── Configuration/
│   │   │   ├── NormalizationConfig.cs     # Base class users inherit
│   │   │   ├── NormalizeBuilder.cs        # Fluent builder
│   │   │   ├── GraphBuilder.cs            # Per-graph configuration
│   │   │   ├── TypeBuilder.cs             # Per-type configuration
│   │   │   └── PropertyMode.cs            # Enum: IncludeAll, ExplicitOnly
│   │   ├── Runtime/
│   │   │   ├── NormalizationContext.cs    # Tracks indices, dedup state
│   │   │   └── NormalizedResult.cs        # Output container
│   │   └── DataNormalizer.csproj
│   └── DataNormalizer.Generators/         # Source generator (netstandard2.0)
│       ├── NormalizeGenerator.cs          # IIncrementalGenerator entry
│       ├── Analysis/
│       │   ├── ConfigurationParser.cs     # Parse fluent config method body
│       │   ├── TypeGraphAnalyzer.cs       # DFS type graph, cycle detection
│       │   └── PropertyClassifier.cs      # Simple/normalizable/collection
│       ├── Emitters/
│       │   ├── DtoEmitter.cs              # DTO class generation
│       │   ├── EqualityEmitter.cs         # IEquatable + GetHashCode
│       │   ├── NormalizerEmitter.cs       # Normalize() method
│       │   └── DenormalizerEmitter.cs     # Denormalize() method
│       ├── Models/
│       │   ├── NormalizationModel.cs      # Parsed config representation
│       │   ├── TypeInfo.cs                # Analyzed type information
│       │   └── PropertyInfo.cs            # Analyzed property information
│       ├── Diagnostics/
│       │   └── DiagnosticDescriptors.cs   # DN0001-DN0004
│       └── DataNormalizer.Generators.csproj
├── tests/
│   ├── DataNormalizer.Tests/              # Runtime unit tests (NUnit 4)
│   ├── DataNormalizer.Generators.Tests/   # Generator snapshot tests (Verify)
│   └── DataNormalizer.Integration.Tests/  # End-to-end roundtrip tests
├── samples/
│   └── DataNormalizer.Samples/
├── DataNormalizer.sln
├── Directory.Build.props
├── Directory.Packages.props
├── .github/
│   └── workflows/
│       ├── ci.yml                         # Build + test + format check on PR
│       └── release.yml                    # NuGet publish on v* tag + major version sync
├── .editorconfig
├── .csharpierrc.yaml
├── LICENSE                                # MIT
└── README.md
```

---

## NuGet Package Structure

Single package: `DataNormalizer`

The `.csproj` for the runtime library references the generator project and bundles it:

```xml
<ItemGroup>
  <ProjectReference Include="..\DataNormalizer.Generators\DataNormalizer.Generators.csproj"
                    OutputItemType="Analyzer"
                    ReferenceOutputAssembly="false" />
</ItemGroup>
```

Package metadata:
- **Package ID:** DataNormalizer
- **License:** MIT
- **Tags:** normalization, source-generator, dto, deduplication, serialization
- **README:** Embedded in package via `<PackageReadmeFile>`
- **SourceLink:** Enabled for debugging
- **Deterministic builds:** Enabled

---

## Testing Strategy

### DataNormalizer.Generators.Tests (Snapshot Tests)

Uses Verify.SourceGenerators to verify generated output:

```csharp
[Test]
public Task SimpleType_GeneratesCorrectDto()
{
    var source = @"
        using DataNormalizer;

        public class Person { public string Name { get; set; } }

        [NormalizeConfiguration]
        public partial class Config : NormalizationConfig
        {
            protected override void Configure(NormalizeBuilder builder)
            {
                builder.NormalizeGraph<Person>();
            }
        }";

    return Verify.VerifyGenerator(source);
}
```

Coverage: simple types, nested objects, collections, circular refs, auto-discovery, opt-out, opt-in, error diagnostics, serialization attributes.

### DataNormalizer.Tests (Unit Tests, NUnit 4)

```csharp
[Test]
public void NormalizationContext_TracksIndicesCorrectly()
{
    var context = new NormalizationContext();
    // ...
    Assert.That(context.GetIndex<NormalizedAddress>(hash), Is.EqualTo(0));
}
```

### DataNormalizer.Integration.Tests (End-to-End, NUnit 4)

```csharp
[Test]
public void Normalize_WithDuplicateAddresses_DeduplicatesCorrectly()
{
    var shared = new Address { Street = "123 Main", City = "Springfield" };
    var person = new Person { HomeAddress = shared, WorkAddress = shared };

    var result = TestNormalization.Normalize(person);

    Assert.That(result.GetCollection<NormalizedAddress>(), Has.Count.EqualTo(1));
    Assert.That(result.Root.HomeAddressIndex, Is.EqualTo(result.Root.WorkAddressIndex));
}

[Test]
public void Roundtrip_NormalizeThenDenormalize_ProducesEquivalentObject()
{
    var original = CreateComplexPerson();
    var normalized = TestNormalization.Normalize(original);
    var restored = TestNormalization.Denormalize(normalized);

    Assert.That(restored, Is.EqualTo(original).Using(new DeepEqualityComparer<Person>()));
}
```

---

## CI/CD

### ci.yml (on PR)

```yaml
- dotnet restore
- dotnet build --no-restore
- dotnet csharpier --check
- dotnet test --no-build --logger "trx"
- Upload test results
```

### release.yml (on v* tag)

```yaml
- Build + test (same as CI)
- dotnet pack -c Release /p:Version=${TAG_VERSION}
- dotnet nuget push to nuget.org
- Create GitHub Release with auto-generated changelog
- nowactions/update-majorver@v1 to sync major version tag
```

---

## Skills Suite (.opencode/skills/)

| Skill | Purpose |
|-------|---------|
| `dotnet-guidelines` | Modern C# standards: primary constructors, records, sealed, var, collection initializers, static lambdas, nullable |
| `dotnet-patterns` | C# patterns: async/await, LINQ, DI, generics, error handling, Result pattern |
| `dotnet-architecture` | Clean Architecture, MSBuild standards, CPM rules, project structure |
| `dotnet-backend-patterns` | DI patterns, IOptions, caching, data access (NUnit 4 adapted) |
| `ddd-patterns` | Aggregate roots, entities, repositories, CQRS, domain exceptions, IClock |
| `dotnet-tdd` | Red-Green-Refactor, AAA pattern, test doubles, NUnit 4 specifics |
| `centralized-packages` | Directory.Packages.props management |
| `nuget-packaging` | NuGet library best practices: source generators, versioning, SourceLink |
| `csharpier` | CSharpier formatting rules and CI integration |
| `source-generator-dev` | Writing Roslyn incremental generators, debugging, Verify testing |
| `legacy-normalizer` | Original expression tree implementation: type graph, hash dedup, deferred serialization |
| `testcontainers` | .NET Testcontainers for integration tests |
| `dotnet-versions` | Multi-targeting .NET 8/9/10, netstandard2.0, conditional compilation |

---

## Dependencies

### Runtime (DataNormalizer)
- **None.** Zero external dependencies.

### Generator (DataNormalizer.Generators)
- `Microsoft.CodeAnalysis.CSharp` (compile-time only, not shipped to consumers)

### Test Projects
- NUnit 4
- NUnit3TestAdapter
- Verify.SourceGenerators (generator tests)
- BenchmarkDotNet (optional, performance tests)

### Tooling
- CSharpier
- GitHub Actions

---

## Open Design Questions (Resolved)

| Question | Resolution |
|----------|-----------|
| Runtime vs source generator | Source generator (compile-time) |
| Target frameworks | net8.0, net9.0, net10.0 (generator: netstandard2.0) |
| Configuration style | Fluent builder (NormalizationConfig) |
| Serialization dependencies | Zero. Users serialize NormalizedResult however they want. |
| Serialization attributes | Copy from source + convention-based for generated properties |
| Equality strategy | IEquatable<T> delegation > all properties > ignore/include |
| Circular references | Supported with cycle detection, compiler warning, visited-set |
| Collections | int[] indices for normalizable types, inline for simple types |
| Test framework | NUnit 4 |
| CI/CD | GitHub Actions, tag-based NuGet publish, nowactions/update-majorver |
| License | MIT |
