---
name: dotnet-patterns
description: C# development patterns for the DataNormalizer project. Covers records with init-only properties, pattern matching (type/property/list/positional), nullable reference types, async/await with ConfigureAwait(false), LINQ method syntax, dependency injection, generic constraints with IEquatable<T>, and Result<T> error handling.
---

# C# Development Patterns

## Records and Init-Only Properties

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

**DataNormalizer note:** Generator pipeline models MUST be records. The incremental generator caches pipeline results and uses structural equality to avoid re-emitting unchanged output. Non-equatable models break caching and cause unnecessary recompilation.

## Pattern Matching

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

### Positional Patterns

```csharp
// Deconstruct in pattern
public static string Describe(IndexEntry entry) => entry switch
{
    (0, true) => "First item added",
    (_, true) => $"New item at index {entry.Index}",
    (_, false) => $"Existing item at index {entry.Index}",
};
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
cache ??= BuildCache();

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

### ConfigureAwait in Libraries (MANDATORY)

DataNormalizer is a library — **always** use `ConfigureAwait(false)` in async methods. This prevents deadlocks when consumers call from synchronization contexts (WPF, WinForms, ASP.NET classic).

```csharp
// CORRECT - library code
public async Task<NormalizedPersonResult> NormalizeAsync(Person source, CancellationToken cancellationToken = default)
{
    var data = await LoadDataAsync(cancellationToken).ConfigureAwait(false);
    return Process(data);
}

// WRONG - missing ConfigureAwait in library
public async Task<NormalizedPersonResult> NormalizeAsync(Person source)
{
    var data = await LoadDataAsync(); // potential deadlock
    return Process(data);
}
```

### CancellationToken

Always accept and forward `CancellationToken` in async methods.

```csharp
public async Task<Result<T>> ProcessAsync(T input, CancellationToken cancellationToken = default)
{
    cancellationToken.ThrowIfCancellationRequested();
    var result = await ComputeAsync(input, cancellationToken).ConfigureAwait(false);
    return Result.Ok(result);
}
```

### Parallel Async

```csharp
// Run independent operations concurrently
public async Task<(Result<A> a, Result<B> b)> ProcessBothAsync(CancellationToken ct = default)
{
    var taskA = ProcessAAsync(ct);
    var taskB = ProcessBAsync(ct);
    await Task.WhenAll(taskA, taskB).ConfigureAwait(false);
    return (await taskA, await taskB);
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
    if (cache.TryGetValue("count", out var count))
        return ValueTask.FromResult(count);

    return ComputeCountAsync();
}
```

### IAsyncEnumerable

```csharp
public async IAsyncEnumerable<TypeGraphNode> AnalyzeTypesAsync(
    IEnumerable<INamedTypeSymbol> types,
    [EnumeratorCancellation] CancellationToken cancellationToken = default)
{
    foreach (var type in types)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var node = await AnalyzeTypeAsync(type, cancellationToken).ConfigureAwait(false);
        yield return node;
    }
}

// Consuming
await foreach (var node in AnalyzeTypesAsync(types, ct).ConfigureAwait(false))
{
    ProcessNode(node);
}
```

## LINQ (Method Syntax Preferred)

Prefer method syntax over query syntax for consistency.

```csharp
// CORRECT - method syntax
var normalizedTypes = typeGraph
    .Where(static x => x.Kind == PropertyKind.Normalized)
    .Select(static x => x.TypeName)
    .Distinct()
    .OrderBy(static x => x)
    .ToList();

// AVOID - query syntax
var normalizedTypes = (from node in typeGraph
                       where node.Kind == PropertyKind.Normalized
                       select node.TypeName).Distinct().OrderBy(x => x).ToList();
```

### Common LINQ Patterns

```csharp
// Existence checks
bool hasCircular = nodes.Any(static x => x.HasCircularReference);

// First with default
var rootNode = nodes.FirstOrDefault(x => x.TypeName == rootTypeName);

// Grouping
var byKind = properties
    .GroupBy(static x => x.Kind)
    .ToDictionary(static x => x.Key, static x => x.ToList());

// OfType for filtering and casting
var namedTypes = members.OfType<INamedTypeSymbol>();

// SelectMany for flattening
var allProperties = nodes
    .SelectMany(static x => x.Properties)
    .Distinct()
    .ToList();

// Aggregate
var totalCount = groups.Aggregate(0, (sum, x) => sum + x.Count);

// Complex pipeline
var report = typeGraph
    .Where(static x => x.Properties.Any())
    .GroupBy(static x => x.Kind)
    .Select(static x => new { Kind = x.Key, Types = x.OrderBy(static t => t.TypeName).ToList() })
    .OrderBy(static x => x.Kind)
    .ToList();
```

## Dependency Injection

### Constructor Injection with Primary Constructors

```csharp
public sealed class NormalizerService(
    INormalizationEngine engine,
    IOptions<NormalizerOptions> options,
    ILogger<NormalizerService> logger)
{
    public object Normalize<T>(T source)
    {
        logger.LogDebug("Normalizing {Type}", typeof(T).Name);
        return engine.Process(source, options.Value);
    }
}
```

### Interface Segregation

```csharp
// Small, focused interfaces — each NormalizeGraph<T>() produces
// a concrete container type (e.g., NormalizedPersonResult), so
// generic interfaces use object or per-config wrappers.
public interface INormalizationEngine
{
    object Normalize<T>(T source);
}

public interface IDenormalizationEngine
{
    T Denormalize<T>(object container);
}
```

### Registration Patterns

```csharp
// Singleton - shared state, thread-safe, created once
services.AddSingleton<INormalizationEngine, NormalizationEngine>();

// Scoped - per-request/operation, new instance per scope
services.AddScoped<INormalizationContext, NormalizationContext>();

// Transient - new instance every time, stateless services
services.AddTransient<ITypeAnalyzer, TypeAnalyzer>();

// Extension method for clean registration
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDataNormalizer(
        this IServiceCollection services,
        Action<NormalizerOptions>? configure = null)
    {
        if (configure is not null)
            services.Configure(configure);

        services.AddSingleton<INormalizationEngine, NormalizationEngine>();
        return services;
    }
}
```

## Generic Patterns

### Constraints for Compile-Time Safety

```csharp
// IEquatable<TDto> constraint for dedup — central to DataNormalizer
public (int Index, bool IsNew) GetOrAddIndex<TDto>(string typeKey, TDto dto)
    where TDto : IEquatable<TDto>
{
    // Compiler enforces TDto has Equals for deduplication
}

// Class constraint for reference type collections (internal runtime API)
public IReadOnlyList<T> GetCollection<T>(string typeKey) where T : class
    => collections.TryGetValue(typeKey, out var list)
        ? list.Cast<T>().ToList()
        : Array.Empty<T>();

// Multiple constraints
public T Resolve<T>(object container) where T : class, IEquatable<T>, new()
{
    // ...
}
```

### Covariance and Contravariance

```csharp
// Covariant (out) - can return derived types
public interface IReadOnlyRepository<out T> where T : class
{
    T? GetById(Guid id);
    IReadOnlyList<T> GetAll();
}

// Contravariant (in) - can accept base types
public interface IComparer<in T>
{
    int Compare(T x, T y);
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
