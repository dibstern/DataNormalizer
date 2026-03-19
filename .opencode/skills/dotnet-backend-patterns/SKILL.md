---
name: dotnet-backend-patterns
description: .NET backend patterns adapted for the DataNormalizer project. Covers DI registration (scoped/transient/singleton, keyed services), IOptions pattern, Result<T> error handling, async best practices, and NUnit 4 test examples with Assert.That syntax.
---

# .NET Backend Patterns

## Dependency Injection Patterns

### Service Lifetimes

```csharp
// Singleton - shared state, thread-safe, created once
services.AddSingleton<INormalizationEngine, NormalizationEngine>();

// Scoped - per-request/operation, new instance per scope
services.AddScoped<INormalizationContext, NormalizationContext>();

// Transient - new instance every time, stateless services
services.AddTransient<ITypeAnalyzer, TypeAnalyzer>();
```

### When to Use Each Lifetime

| Lifetime | Use For | Example |
|----------|---------|---------|
| Singleton | Caches, configuration, thread-safe stateless services | `NormalizationEngine` |
| Scoped | Per-operation state, unit-of-work | `NormalizationContext` |
| Transient | Lightweight stateless services | `TypeAnalyzer` |

### Keyed Services (.NET 8+)

```csharp
// Registration
services.AddKeyedSingleton<ISerializer, JsonSerializer>("json");
services.AddKeyedSingleton<ISerializer, XmlSerializer>("xml");

// Injection
public sealed class ExportService([FromKeyedServices("json")] ISerializer serializer)
{
    public string Export<T>(T data) => serializer.Serialize(data);
}
```

### Constructor Injection with Primary Constructors

```csharp
public sealed class NormalizerService(
    INormalizationEngine engine,
    IOptions<NormalizerOptions> options,
    ILogger<NormalizerService> logger) : INormalizerService
{
    public NormalizedResult<T> Normalize<T>(T source)
    {
        logger.LogDebug("Normalizing type {TypeName}", typeof(T).Name);
        return engine.Process(source, options.Value);
    }
}
```

### Registration Extensions

```csharp
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

## IOptions Pattern

### Configuration Classes

```csharp
public sealed class NormalizerOptions
{
    public const string SectionName = "DataNormalizer";

    public int MaxDepth { get; set; } = 100;
    public bool EnableCircularReferenceDetection { get; set; } = true;
    public StringComparison PropertyNameComparison { get; set; } = StringComparison.Ordinal;
}
```

### Registration

```csharp
// From configuration
services.Configure<NormalizerOptions>(config.GetSection(NormalizerOptions.SectionName));

// With validation
services.AddOptions<NormalizerOptions>()
    .BindConfiguration(NormalizerOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();
```

### Injection Variants

```csharp
// IOptions<T> - singleton, read once at startup
public sealed class Service1(IOptions<NormalizerOptions> options)
{
    private readonly NormalizerOptions _options = options.Value;
}

// IOptionsSnapshot<T> - scoped, re-reads per request
public sealed class Service2(IOptionsSnapshot<NormalizerOptions> options)
{
    public void Process() => Console.WriteLine(options.Value.MaxDepth);
}

// IOptionsMonitor<T> - singleton, notifies on change
public sealed class Service3(IOptionsMonitor<NormalizerOptions> options)
{
    public void Process() => Console.WriteLine(options.CurrentValue.MaxDepth);
}
```

## Result Pattern for Error Handling

### Result Type

```csharp
public readonly record struct Result<T>
{
    public T? Value { get; }
    public string? Error { get; }
    public bool IsSuccess { get; }

    private Result(T value) => (Value, IsSuccess) = (value, true);
    private Result(string error) => (Error, IsSuccess) = (error, false);

    public static Result<T> Ok(T value) => new(value);
    public static Result<T> Fail(string error) => new(error);

    public TOut Match<TOut>(Func<T, TOut> onSuccess, Func<string, TOut> onError)
        => IsSuccess ? onSuccess(Value!) : onError(Error!);
}
```

### Usage

```csharp
public Result<NormalizationModel> ParseConfiguration(string source)
{
    if (string.IsNullOrWhiteSpace(source))
        return Result<NormalizationModel>.Fail("Source cannot be empty");

    try
    {
        var model = DoParse(source);
        return Result<NormalizationModel>.Ok(model);
    }
    catch (FormatException ex)
    {
        return Result<NormalizationModel>.Fail($"Parse error: {ex.Message}");
    }
}

// Consumer
var result = parser.ParseConfiguration(source);
result.Match(
    onSuccess: model => ProcessModel(model),
    onError: error => logger.LogWarning("Parse failed: {Error}", error));
```

### When to Use Exceptions vs Result

| Scenario | Use |
|----------|-----|
| Programmer error (null arg, invalid state) | `ArgumentException`, `InvalidOperationException` |
| Expected business failure (validation, not found) | `Result<T>` |
| Infrastructure failure (DB down, network) | Exceptions (let them propagate) |
| Configuration error (missing settings) | Exceptions at startup |

## Async Best Practices

### Library Code

```csharp
// Always use ConfigureAwait(false) in library code
public async Task<Result<T>> ProcessAsync<T>(T input, CancellationToken cancellationToken = default)
{
    var data = await FetchDataAsync(cancellationToken).ConfigureAwait(false);
    var result = Transform(data);
    await SaveAsync(result, cancellationToken).ConfigureAwait(false);
    return Result<T>.Ok(result);
}
```

### Cancellation

```csharp
// Always accept and forward CancellationToken
public async Task<IReadOnlyList<T>> GetAllAsync<T>(CancellationToken cancellationToken = default)
{
    cancellationToken.ThrowIfCancellationRequested();

    var items = new List<T>();
    await foreach (var item in source.WithCancellation(cancellationToken).ConfigureAwait(false))
    {
        items.Add(item);
    }
    return items;
}
```

## Testing These Patterns with NUnit 4

### DI Testing

```csharp
[TestFixture]
public sealed class NormalizerServiceTests
{
    [Test]
    public void Normalize_ReturnsExpectedResult()
    {
        // Arrange
        var engine = new FakeNormalizationEngine();
        var options = Options.Create(new NormalizerOptions { MaxDepth = 10 });
        var logger = NullLogger<NormalizerService>.Instance;
        var service = new NormalizerService(engine, options, logger);
        var input = new TestPerson { Name = "Alice" };

        // Act
        var result = service.Normalize(input);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Root.Name, Is.EqualTo("Alice"));
    }
}
```

### Result Pattern Testing

```csharp
[TestFixture]
public sealed class ConfigurationParserTests
{
    [Test]
    public void Parse_EmptySource_ReturnsFailure()
    {
        var parser = new ConfigurationParser();
        var result = parser.ParseConfiguration("");

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Error, Does.Contain("empty"));
    }

    [Test]
    public void Parse_ValidSource_ReturnsSuccess()
    {
        var parser = new ConfigurationParser();
        var result = parser.ParseConfiguration(ValidSource);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value, Is.Not.Null);
    }
}
```

### Async Testing

```csharp
[TestFixture]
public sealed class AsyncServiceTests
{
    [Test]
    public async Task ProcessAsync_WithCancellation_ThrowsOperationCanceled()
    {
        var service = CreateService();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.That(
            async () => await service.ProcessAsync("data", cts.Token),
            Throws.InstanceOf<OperationCanceledException>());
    }

    [Test]
    public async Task ProcessAsync_ValidInput_ReturnsResult()
    {
        var service = CreateService();

        var result = await service.ProcessAsync("valid-input");

        Assert.That(result.IsSuccess, Is.True);
    }
}
```

### IOptions Testing

```csharp
[TestFixture]
public sealed class OptionsTests
{
    [Test]
    public void Service_UsesConfiguredOptions()
    {
        var options = Options.Create(new NormalizerOptions { MaxDepth = 5 });
        var service = new NormalizerService(new FakeEngine(), options, NullLogger<NormalizerService>.Instance);

        // Test that the service respects the configured max depth
        Assert.That(() => service.NormalizeDeeply(deepObject),
            Throws.InstanceOf<InvalidOperationException>()
                .With.Message.Contains("max depth"));
    }
}
```

## Common Pitfalls

1. **Captive dependencies**: Never inject scoped service into singleton
2. **Service locator anti-pattern**: Don't resolve services from `IServiceProvider` in constructors
3. **Async void**: Never use `async void` except for event handlers
4. **Missing ConfigureAwait**: Always use `ConfigureAwait(false)` in library code
5. **Throwing in constructors**: Keep constructors simple; use factory methods for complex initialization
