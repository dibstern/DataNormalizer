---
name: dotnet-guidelines
description: Modern C# 12 coding standards for the DataNormalizer project. Covers file-scoped namespaces, primary constructors, records, sealed classes, var usage, collection initializers, static lambdas, nullable reference types, file organization, and code formatting with CSharpier.
---

# Modern C# Coding Standards

These rules apply to all C# code in the DataNormalizer project. The source generator project (`DataNormalizer.Generators`) targets netstandard2.0 and uses PolySharp for C# 12 polyfills.

## Critical Rules

- ALWAYS use primary constructors for classes (see exception below for netstandard2.0).
- ALWAYS prefer `record` types for immutable data structures.
- NEVER mix multiple classes or DTOs in a single file, even if they are small. Filename ALWAYS matches the class or DTO name.
- Private fields should NOT be prefixed with `_` (e.g., `repository` not `_repository`).
  - **Exception:** In the generator project (netstandard2.0), `_` prefix IS used for backing fields where primary constructors cannot be used due to target framework constraints.
- Use `string?` over `string` for nullable strings.
- Use `var` for local variables when type is obvious.
- Don't use Regions in code files.
- Use collection initializers: instead of `new List<string>()`, use `new() { "item1", "item2" }`, and instead of `list.ToArray()` use `[.. list]`.
- Use anonymous function static: instead of `list.Select(x => new Dto(x.SameProperty))`, use `list.Select(static x => new Dto(x.SameProperty))`.
- ALWAYS use `sealed` accessor for classes or records if not intended for inheritance.
  - **Exception:** `NormalizationConfig` is the abstract base class users inherit from.
- ALWAYS use `internal` accessor for internal classes or records.
- ALWAYS use `ILogger` with structured logging to log, no string interpolation and no other logging methods.
- ALWAYS use cancellation tokens for asynchronous methods.
- ALWAYS use `x` as a parameter name in lambdas and anonymous functions.
- NEVER use try-catch blocks solely to log and rethrow exceptions.
- ALWAYS use file-scoped namespaces. Never use block-scoped namespaces.
- ALWAYS use raw string literals (`"""`) for multi-line code emission in the generator.
- Run `dotnet build` after any backend changes and ensure no build errors.
- Run `dotnet csharpier format .` before committing.

## File-Scoped Namespaces (Required)

```csharp
// CORRECT
namespace DataNormalizer.Runtime;

public sealed class NormalizationContext { }

// WRONG - never do this
namespace DataNormalizer.Runtime
{
    public sealed class NormalizationContext { }
}
```

## Primary Constructors

Use primary constructors where appropriate, especially for DI and simple parameter capture.

```csharp
// CORRECT - simple DI
public sealed class NormalizerService(INormalizationEngine engine, ILogger<NormalizerService> logger)
{
    public void Process() => engine.Run();
}

// CORRECT - netstandard2.0 generator code where primary constructors aren't always viable
public sealed class TypeGraphAnalyzer
{
    private readonly Dictionary<string, TypeGraphNode> _visited;

    public TypeGraphAnalyzer()
    {
        _visited = new Dictionary<string, TypeGraphNode>(StringComparer.Ordinal);
    }
}
```

## Record Types for Immutable Data

Use `record` (or `record struct`) for immutable data carriers. Prefer positional syntax for small records, init-only properties for larger ones.

```csharp
// Small - positional
public sealed record PropertyInfo(string Name, string TypeName, PropertyKind Kind);

// Larger - init-only properties
public sealed record TypeGraphNode
{
    public required string TypeName { get; init; }
    public required string FullyQualifiedName { get; init; }
    public required IReadOnlyList<AnalyzedProperty> Properties { get; init; }
    public bool HasCircularReference { get; init; }
}

// Generator pipeline models MUST be records (equatable for incremental caching)
internal readonly record struct ConfigInfo(
    string ClassName,
    string Namespace,
    bool IsPartial);
```

## Sealed Classes

Apply `sealed` to all classes not intended for inheritance. In this project, nearly every class should be sealed. The only exception is `NormalizationConfig` (the abstract base class users inherit).

```csharp
// CORRECT
public sealed class NormalizationContext { }
// Generated container DTOs are sealed (e.g., NormalizedPersonResult)

// CORRECT - intended for inheritance
public abstract class NormalizationConfig
{
    protected abstract void Configure(NormalizeBuilder builder);
}
```

## Var Usage

Use `var` when the type is obvious from the right-hand side. Use explicit types when the type is not clear.

```csharp
// CORRECT - type obvious
var context = new NormalizationContext();
var properties = type.GetMembers().OfType<IPropertySymbol>();
var map = new Dictionary<string, int>();

// CORRECT - type not obvious, be explicit
INamedTypeSymbol baseType = type.BaseType;
int index = context.GetOrAddIndex(key, dto).Index;
```

## Collection Initializers

Use target-typed `new()` and collection expressions where supported.

```csharp
// CORRECT
Dictionary<string, int> map = new();
List<TypeGraphNode> nodes = [];
int[] indices = [1, 2, 3];

// Spread operator
int[] combined = [.. firstArray, .. secondArray];

// CORRECT in assignments
private readonly Dictionary<string, object> indexMaps = new();
private readonly HashSet<object> visited = new(ReferenceEqualityComparer.Instance);
```

## Static Lambdas

Use `static` lambdas when the lambda does not capture any variables from the enclosing scope.

```csharp
// CORRECT - no captures needed
var simpleProps = properties.Where(static x => x.Kind == PropertyKind.Simple);
var names = types.Select(static x => x.Name);

// CORRECT - captures needed, no static
var targetName = "Person";
var match = types.FirstOrDefault(x => x.Name == targetName);
```

## Nullable Reference Types

Nullable reference types are enabled project-wide via `Directory.Build.props`. Always handle nullability explicitly.

```csharp
// CORRECT - explicit nullable
public string? MiddleName { get; set; }
public Address? WorkAddress { get; set; }

// CORRECT - null checks
public string GetDisplayName(string? middleName)
{
    return middleName is not null
        ? $"{FirstName} {middleName} {LastName}"
        : $"{FirstName} {LastName}";
}

// CORRECT - null-forgiving only when you've verified
var symbol = semanticModel.GetDeclaredSymbol(node)!; // after null check above

// WRONG - suppressing without justification
var x = GetValue()!; // Why is this safe?
```

## One Type Per File

Each file contains exactly one type. The filename must match the type name.

```
NormalizationContext.cs     -> class NormalizationContext
TypeGraphNode.cs            -> record TypeGraphNode
PropertyKind.cs             -> enum PropertyKind
```

Exceptions:
- A file may contain a small companion type tightly coupled to the main type (e.g., a private nested class).
- Extension method classes for the same type can share a file with the type.

## Access Modifiers

Always specify access modifiers explicitly. Never rely on defaults.

```csharp
// CORRECT
public sealed class Foo { }
internal sealed class Bar { }
private readonly int count;

// WRONG - implicit internal
class Foo { }
```

## Expression-Bodied Members

Use expression-bodied members for simple, single-expression methods and properties.

```csharp
// CORRECT
public string FullName => $"{FirstName} {LastName}";
public override string ToString() => Name;
public bool IsEmpty => items.Count == 0;

// WRONG - too complex for expression body
public string FormatDetails() =>
    string.Join(", ", Properties
        .Where(static x => x.Kind != PropertyKind.Ignored)
        .Select(static x => $"{x.Name}: {x.TypeName}")
        .OrderBy(static x => x));
// Use block body instead for multi-line logic
```

## String Handling

- Use string interpolation (`$"..."`) over `string.Format` or concatenation
- Use `StringComparison.Ordinal` / `StringComparison.OrdinalIgnoreCase` for non-user-facing comparisons
- Use raw string literals for multi-line code generation in the generator

```csharp
// CORRECT
var message = $"Type '{typeName}' has {count} properties";
var match = name.Equals("Person", StringComparison.Ordinal);

// Raw string for generated code
var source = $$"""
    namespace {{ns}};

    public partial class Normalized{{typeName}}
    {
        {{properties}}
    }
    """;
```

## Code Organization Principles

### Avoid Unnecessary Wrapper Methods

**Rule**: Do NOT create wrapper methods for specific cases that simply call another method with fixed parameters. Call the base method directly instead.

```csharp
// DON'T - unnecessary wrapper
public sealed class TypeAnalyzer
{
    public bool IsSimpleType(ITypeSymbol type)
        => ClassifyType(type) == PropertyKind.Simple;

    public bool IsCollectionType(ITypeSymbol type)
        => ClassifyType(type) == PropertyKind.Collection;

    public PropertyKind ClassifyType(ITypeSymbol type) { /* ... */ }
}

// DO - call ClassifyType directly at call sites
var kind = analyzer.ClassifyType(type);
if (kind == PropertyKind.Simple) { /* ... */ }
```

**When to create a helper method:**
- The method adds meaningful domain logic (not just passing fixed arguments)
- The method is called from many places and the fixed arguments represent a stable concept
- The method name communicates intent that would be lost at the call site
