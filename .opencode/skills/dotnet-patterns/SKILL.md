---
name: dotnet-patterns
description: C# development patterns for the DataNormalizer project. Covers records with init-only properties, pattern matching, nullable reference types, async/await best practices, LINQ (method syntax), dependency injection, generic constraints, and the Result<T> pattern for error handling.
---

# C# Development Patterns

## Records with Init-Only Properties

Use records for immutable data transfer objects and value objects. Prefer `init` over `set` for properties that should only be set during initialization.

```csharp
// Positional record - concise for small types
public sealed record AnalyzedProperty(string Name, string TypeName, PropertyKind Kind, bool IsNullable);

// Init-only record - for types with many properties
public sealed record TypeGraphNode
{
    public required string TypeName { get; init; }
    public required string FullyQualifiedName { get; init; }
    public required IReadOnlyList<AnalyzedProperty> Properties { get; init; }
    public bool HasCircularReference { get; init; }
    public IReadOnlySet<string> CycleEdgeProperties { get; init; } = new HashSet<string>();
}

// Record struct for high-performance value types in hot paths
public readonly record struct IndexEntry(int Index, bool IsNew);
```

## Pattern Matching

Use type patterns, property patterns, and switch expressions for cleaner control flow.

### Type Patterns

```csharp
// Type pattern with declaration
if (symbol is INamedTypeSymbol namedType)
{
    AnalyzeType(namedType);
}

// Negation
if (expression is not InvocationExpressionSyntax)
    return;
```

### Property Patterns

```csharp
// Property pattern
if (type is { TypeKind: TypeKind.Enum })
    return PropertyKind.Simple;

if (type is { IsValueType: true, OriginalDefinition.SpecialType: SpecialType.System_Nullable_T })
{
    var innerType = ((INamedTypeSymbol)type).TypeArguments[0];
    // handle Nullable<T>
}
```

### Switch Expressions

```csharp
public static PropertyKind Classify(ITypeSymbol type) => type switch
{
    { TypeKind: TypeKind.Enum } => PropertyKind.Simple,
    { SpecialType: not SpecialType.None } => PropertyKind.Simple,
    IArrayTypeSymbol array => ClassifyCollection(array.ElementType),
    INamedTypeSymbol named when IsCollectionType(named) => ClassifyCollection(GetElementType(named)),
    INamedTypeSymbol named => PropertyKind.Normalized,
    _ => PropertyKind.Simple,
};
```

### List Patterns

```csharp
// Checking argument lists
if (invocation.ArgumentList.Arguments is [var singleArg])
{
    ProcessSingleArgument(singleArg);
}
```

## Nullable Reference Types

### Method Signatures

```csharp
// Nullable parameters and return types
public INamedTypeSymbol? FindType(string name) { }

// Non-nullable with guard
public void Process(INamedTypeSymbol type)
{
    ArgumentNullException.ThrowIfNull(type);
}
```

### Null-Conditional and Null-Coalescing

```csharp
// Null-conditional
var name = type.ContainingNamespace?.ToDisplayString();

// Null-coalescing assignment
_cache ??= BuildCache();

// Null-coalescing with throw
var symbol = semanticModel.GetDeclaredSymbol(node)
    ?? throw new InvalidOperationException($"Cannot resolve symbol for {node}");
```

### Nullable Flow Analysis

```csharp
// The compiler tracks nullability through flow
public void Process(string? input)
{
    if (input is null)
        return;

    // Here 'input' is known non-null
    var length = input.Length; // No warning
}
```

## Async/Await Best Practices

### ConfigureAwait in Libraries

DataNormalizer is a library — always use `ConfigureAwait(false)` in async methods.

```csharp
// CORRECT - library code
public async Task<NormalizedResult<T>> NormalizeAsync<T>(T source, CancellationToken cancellationToken = default)
{
    var data = await LoadDataAsync(cancellationToken).ConfigureAwait(false);
    return Process(data);
}
```

### CancellationToken

Always accept and forward `CancellationToken` in async methods.

```csharp
// CORRECT
public async Task<Result<T>> ProcessAsync(T input, CancellationToken cancellationToken = default)
{
    cancellationToken.ThrowIfCancellationRequested();
    var result = await ComputeAsync(input, cancellationToken).ConfigureAwait(false);
    return Result.Ok(result);
}
```

### Never Block on Async

Never call `.Result`, `.Wait()`, or `.GetAwaiter().GetResult()` on tasks.

```csharp
// WRONG
var result = ProcessAsync(data).Result;
ProcessAsync(data).Wait();
var result = ProcessAsync(data).GetAwaiter().GetResult();

// CORRECT
var result = await ProcessAsync(data, cancellationToken).ConfigureAwait(false);
```

### ValueTask

Use `ValueTask<T>` when the result is frequently available synchronously.

```csharp
public ValueTask<int> GetCachedCountAsync()
{
    if (_cache.TryGetValue("count", out var count))
        return ValueTask.FromResult(count);

    return ComputeCountAsync();
}
```

## LINQ (Method Syntax Preferred)

Prefer method syntax over query syntax for consistency.

```csharp
// CORRECT - method syntax
var normalizedTypes = typeGraph
    .Where(static node => node.Kind == PropertyKind.Normalized)
    .Select(static node => node.TypeName)
    .Distinct()
    .OrderBy(static name => name)
    .ToList();

// AVOID - query syntax
var normalizedTypes = (from node in typeGraph
                       where node.Kind == PropertyKind.Normalized
                       select node.TypeName).Distinct().OrderBy(n => n).ToList();
```

### Common LINQ Patterns

```csharp
// Existence checks
bool hasCircular = nodes.Any(static n => n.HasCircularReference);

// First with default
var rootNode = nodes.FirstOrDefault(n => n.TypeName == rootTypeName);

// Grouping
var byKind = properties.GroupBy(static p => p.Kind)
    .ToDictionary(static g => g.Key, static g => g.ToList());

// OfType for filtering and casting
var namedTypes = members.OfType<INamedTypeSymbol>();
```

## Dependency Injection

### Constructor Injection with Primary Constructors

```csharp
public sealed class NormalizerService(
    INormalizationEngine engine,
    IOptions<NormalizerOptions> options,
    ILogger<NormalizerService> logger)
{
    public NormalizedResult<T> Normalize<T>(T source)
    {
        logger.LogDebug("Normalizing {Type}", typeof(T).Name);
        return engine.Process(source, options.Value);
    }
}
```

### Interface Segregation

```csharp
// Small, focused interfaces
public interface INormalizationEngine
{
    NormalizedResult<T> Normalize<T>(T source);
}

public interface IDenormalizationEngine
{
    T Denormalize<T>(NormalizedResult<T> result);
}
```

## Generic Constraints

Use constraints to express intent and get compile-time safety.

```csharp
// Equatable constraint for dedup
public (int Index, bool IsNew) GetOrAddIndex<TDto>(string typeKey, TDto dto)
    where TDto : IEquatable<TDto>
{
    // Compiler enforces TDto has Equals
}

// Class constraint for reference type collections
public IReadOnlyList<T> GetCollection<T>(string typeKey) where T : class
    => _collections.TryGetValue(typeKey, out var list)
        ? list.Cast<T>().ToList()
        : Array.Empty<T>();

// Multiple constraints
public T Resolve<T>(NormalizedResult<T> result) where T : class, IEquatable<T>, new()
{
    // ...
}
```

## Result Pattern for Error Handling

Use `Result<T>` instead of exceptions for expected failure paths. Exceptions are for exceptional, unexpected situations only.

```csharp
// Result type
public readonly record struct Result<T>
{
    public T? Value { get; }
    public string? Error { get; }
    public bool IsSuccess { get; }

    private Result(T value) { Value = value; IsSuccess = true; }
    private Result(string error) { Error = error; IsSuccess = false; }

    public static Result<T> Ok(T value) => new(value);
    public static Result<T> Fail(string error) => new(error);

    public TOut Match<TOut>(Func<T, TOut> onSuccess, Func<string, TOut> onError)
        => IsSuccess ? onSuccess(Value!) : onError(Error!);
}

// Usage
public Result<NormalizationModel> Parse(ClassDeclarationSyntax configClass, SemanticModel model)
{
    if (!configClass.Modifiers.Any(SyntaxKind.PartialKeyword))
        return Result<NormalizationModel>.Fail("Configuration class must be partial");

    var result = AnalyzeConfiguration(configClass, model);
    return Result<NormalizationModel>.Ok(result);
}
```

## Disposable Pattern

```csharp
// Prefer using declarations (no braces)
using var stream = File.OpenRead(path);
var data = await JsonSerializer.DeserializeAsync<T>(stream, cancellationToken: ct)
    .ConfigureAwait(false);

// IAsyncDisposable
await using var connection = await factory.CreateConnectionAsync(ct).ConfigureAwait(false);
```
