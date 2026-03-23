---
name: dotnet-backend-patterns
description: .NET backend patterns adapted for the DataNormalizer NuGet library. Covers DI registration (scoped/transient/singleton, keyed services, factory pattern), IOptions pattern, Result<T> error handling, async best practices with ConfigureAwait(false), and NUnit 4 test examples with Assert.That constraint syntax.
---

# .NET Backend Patterns

## Project Structure (Clean Architecture)

DataNormalizer follows clean architecture principles as a NuGet library:

| Layer | Project | Purpose |
|-------|---------|---------|
| Public API | `DataNormalizer` | Attributes, configuration, runtime containers |
| Code Generation | `DataNormalizer.Generators` | Roslyn source generator (netstandard2.0) |
| Unit Tests | `DataNormalizer.Tests` | Runtime behavior tests (NUnit 4) |
| Snapshot Tests | `DataNormalizer.Generators.Tests` | Generator output verification (Verify) |
| Integration Tests | `DataNormalizer.Integration.Tests` | End-to-end with real generated code |

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

**Anti-pattern: Captive Dependencies** - Never inject a scoped service into a singleton. The scoped service becomes "captive" — held for the lifetime of the singleton.

```csharp
// WRONG - captive dependency
services.AddSingleton<MySingleton>();   // lives forever
services.AddScoped<MyScopedDep>();      // should live per-scope

public sealed class MySingleton(MyScopedDep dep) { } // dep is now captive!
```

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

### Factory Pattern

```csharp
// When you need runtime decisions for service creation
services.AddSingleton<INormalizerFactory, NormalizerFactory>();

public sealed class NormalizerFactory(IServiceProvider provider) : INormalizerFactory
{
    public INormalizer Create(NormalizerOptions options)
    {
        return new Normalizer(options, provider.GetRequiredService<ILogger<Normalizer>>());
    }
}
```

### Constructor Injection with Primary Constructors

```csharp
public sealed class NormalizerService(
    INormalizationEngine engine,
    IOptions<NormalizerOptions> options,
    ILogger<NormalizerService> logger) : INormalizerService
{
    public object Normalize<T>(T source)
    {
        logger.LogDebug("Normalizing type {TypeName}.", typeof(T).Name);
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
// From configuration section
services.Configure<NormalizerOptions>(config.GetSection(NormalizerOptions.SectionName));

// With data annotation validation
services.AddOptions<NormalizerOptions>()
    .BindConfiguration(NormalizerOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();
```

### Injection Variants

| Interface | Lifetime | Reloads? | Use When |
|-----------|----------|----------|----------|
| `IOptions<T>` | Singleton | No | Config read once at startup |
| `IOptionsSnapshot<T>` | Scoped | Per-scope | Config may change between requests |
| `IOptionsMonitor<T>` | Singleton | Yes (notify) | React to config changes in real-time |

```csharp
// IOptions<T> - singleton, read once at startup
public sealed class Service1(IOptions<NormalizerOptions> options)
{
    private readonly NormalizerOptions opts = options.Value;
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
    onSuccess: x => ProcessModel(x),
    onError: x => logger.LogWarning("Parse failed: {Error}.", x));
```

### When to Use Exceptions vs Result

| Scenario | Use |
|----------|-----|
| Programmer error (null arg, invalid state) | `ArgumentException`, `InvalidOperationException` |
| Expected business failure (validation, not found) | `Result<T>` |
| Infrastructure failure (DB down, network) | Exceptions (let them propagate) |
| Configuration error (missing settings) | Exceptions at startup |

## Async/Await Patterns

### Library Code (ConfigureAwait MANDATORY)

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

### Async Anti-Patterns

```csharp
// WRONG - async void (fire-and-forget, exceptions are unobserved)
public async void ProcessInBackground() { }

// WRONG - blocking on async code
var result = ProcessAsync(data).Result;         // deadlock risk
ProcessAsync(data).Wait();                       // deadlock risk
var result = ProcessAsync(data).GetAwaiter().GetResult(); // deadlock risk

// WRONG - unnecessary async/await (just return the task)
public async Task<int> GetCountAsync()
{
    return await repository.CountAsync(); // unnecessary state machine
}

// CORRECT - elide async/await when just forwarding
public Task<int> GetCountAsync()
{
    return repository.CountAsync();
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
        Assert.That(result, Is.Not.Null); // Container DTO returned by generated Normalize()
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

### Parameterized Tests

```csharp
[TestFixture]
public sealed class LifetimeTests
{
    [TestCase(ServiceLifetime.Singleton)]
    [TestCase(ServiceLifetime.Scoped)]
    [TestCase(ServiceLifetime.Transient)]
    public void AddDataNormalizer_RegistersWithCorrectLifetime(ServiceLifetime expected)
    {
        var services = new ServiceCollection();
        services.AddDataNormalizer(lifetime: expected);

        var descriptor = services.First(x => x.ServiceType == typeof(INormalizationEngine));
        Assert.That(descriptor.Lifetime, Is.EqualTo(expected));
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

        Assert.That(
            () => service.NormalizeDeeply(deepObject),
            Throws.InstanceOf<InvalidOperationException>()
                .With.Message.Contains("max depth"));
    }
}
```

## Best Practices Summary

| DO | DON'T |
|----|-------|
| Use `sealed` on all classes not designed for inheritance | Leave classes unsealed by default |
| Use primary constructors for DI | Use service locator pattern |
| Use `ConfigureAwait(false)` in library code | Forget ConfigureAwait in libraries |
| Use `CancellationToken` in all async methods | Use `async void` |
| Use `Result<T>` for expected failures | Throw exceptions for control flow |
| Use `IOptions<T>` for configuration | Hardcode settings |
| Use `record` for immutable data | Use mutable DTOs |
| Register with correct lifetimes | Inject scoped into singleton |
| Use structured logging with `ILogger` | Use string interpolation in logs |
| Forward `CancellationToken` to all calls | Swallow cancellation |

## Common Pitfalls

1. **Captive dependencies**: Never inject scoped service into singleton
2. **Service locator anti-pattern**: Don't resolve services from `IServiceProvider` in constructors
3. **Async void**: Never use `async void` except for event handlers
4. **Missing ConfigureAwait**: Always use `ConfigureAwait(false)` in library code
5. **Throwing in constructors**: Keep constructors simple; use factory methods for complex initialization
