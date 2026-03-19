---
name: dotnet-tdd
description: Test-Driven Development workflow for the DataNormalizer project using NUnit 4. Covers Red-Green-Refactor cycle, NUnit 4 specifics (Test/TestCase/Assert.That), AAA pattern, test naming conventions, test doubles, and commands for running tests.
---

# Test-Driven Development with NUnit 4

## Red-Green-Refactor Cycle

1. **RED**: Write a failing test that describes the desired behavior
2. **GREEN**: Write the minimal code to make the test pass
3. **REFACTOR**: Improve the code while keeping tests green

### Example Cycle

```csharp
// 1. RED - Write the test first (NormalizationContext doesn't exist yet)
[Test]
public void GetOrAddIndex_FirstObject_ReturnsZeroAndIsNew()
{
    var ctx = new NormalizationContext();
    var dto = new TestDto("Alice", 30);

    var (index, isNew) = ctx.GetOrAddIndex("person", dto);

    Assert.That(index, Is.EqualTo(0));
    Assert.That(isNew, Is.True);
}

// 2. GREEN - Implement the minimal code
public sealed class NormalizationContext
{
    private readonly Dictionary<string, object> _maps = new();

    public (int Index, bool IsNew) GetOrAddIndex<TDto>(string typeKey, TDto dto)
        where TDto : IEquatable<TDto>
    {
        // minimal implementation
        if (!_maps.TryGetValue(typeKey, out var mapObj))
        {
            mapObj = new Dictionary<TDto, int>();
            _maps[typeKey] = mapObj;
        }
        var map = (Dictionary<TDto, int>)mapObj;
        if (map.TryGetValue(dto, out var idx))
            return (idx, false);
        var newIdx = map.Count;
        map[dto] = newIdx;
        return (newIdx, true);
    }
}

// 3. REFACTOR - Clean up while tests stay green
```

## NUnit 4 Specifics

### CRITICAL: NUnit 4, NOT xUnit

This project uses **NUnit 4**. Never use xUnit conventions.

| NUnit 4 | xUnit (DO NOT USE) |
|---------|-------------------|
| `[Test]` | `[Fact]` |
| `[TestCase(1, 2, 3)]` | `[Theory]` + `[InlineData(1, 2, 3)]` |
| `[TestFixture]` | (class-level, implicit) |
| `[SetUp]` | constructor |
| `[TearDown]` | `IDisposable` |
| `[OneTimeSetUp]` | `IClassFixture<T>` |
| `[OneTimeTearDown]` | `IClassFixture<T>.Dispose` |
| `Assert.That(x, Is.EqualTo(y))` | `Assert.Equal(y, x)` |

### Test Attributes

```csharp
[TestFixture]
public sealed class NormalizationContextTests
{
    private NormalizationContext _context = null!;

    [SetUp]
    public void SetUp()
    {
        _context = new NormalizationContext();
    }

    [Test]
    public void GetOrAddIndex_FirstObject_ReturnsZero()
    {
        var (index, _) = _context.GetOrAddIndex("person", new TestDto("Alice", 30));
        Assert.That(index, Is.EqualTo(0));
    }

    [TestCase("Alice", 30, 0)]
    [TestCase("Bob", 25, 1)]
    public void GetOrAddIndex_MultipleObjects_ReturnsSequentialIndices(string name, int age, int expected)
    {
        // Add Alice first to establish index 0
        _context.GetOrAddIndex("person", new TestDto("Alice", 30));
        if (name != "Alice")
        {
            var (index, _) = _context.GetOrAddIndex("person", new TestDto(name, age));
            Assert.That(index, Is.EqualTo(expected));
        }
    }
}
```

### Assert.That Constraint Syntax

Always use the constraint-based `Assert.That()` syntax:

```csharp
// Equality
Assert.That(result, Is.EqualTo(expected));
Assert.That(result, Is.Not.EqualTo(unexpected));

// Null checks
Assert.That(result, Is.Null);
Assert.That(result, Is.Not.Null);

// Boolean
Assert.That(isNew, Is.True);
Assert.That(exists, Is.False);

// String
Assert.That(message, Does.Contain("error"));
Assert.That(name, Does.StartWith("Normalized"));
Assert.That(name, Is.EqualTo("person").IgnoreCase);

// Collections
Assert.That(collection, Has.Count.EqualTo(3));
Assert.That(collection, Is.Empty);
Assert.That(collection, Is.Not.Empty);
Assert.That(collection, Has.Exactly(2).Items);
Assert.That(collection, Does.Contain(item));
Assert.That(collection, Is.All.Not.Null);
Assert.That(collection, Is.Ordered);

// Numeric
Assert.That(value, Is.GreaterThan(0));
Assert.That(value, Is.InRange(1, 10));

// Type
Assert.That(result, Is.InstanceOf<NormalizedResult<TestDto>>());
Assert.That(result, Is.AssignableTo<IEquatable<TestDto>>());

// Exceptions
Assert.That(() => context.AddToCollection("key", -1, obj),
    Throws.InstanceOf<ArgumentOutOfRangeException>());

Assert.That(() => new NormalizationContext().Resolve<TestDto>("missing", 0),
    Throws.InstanceOf<ArgumentOutOfRangeException>()
        .With.Message.Contains("out of range"));

// Async exceptions
Assert.That(async () => await service.ProcessAsync(null!),
    Throws.InstanceOf<ArgumentNullException>());

// Reference equality
Assert.That(actual, Is.SameAs(expected));
Assert.That(actual, Is.Not.SameAs(other));
```

## AAA Pattern (Arrange-Act-Assert)

Every test follows the Arrange-Act-Assert pattern with clear separation.

```csharp
[Test]
public void Normalize_SharedAddress_DeduplicatesToOneEntry()
{
    // Arrange
    var sharedAddress = new Address { Street = "123 Main", City = "Springfield" };
    var person = new Person
    {
        Name = "Alice",
        HomeAddress = sharedAddress,
        WorkAddress = sharedAddress,
    };

    // Act
    var result = TestNormalization.Normalize(person);

    // Assert
    Assert.That(result.GetCollection<NormalizedAddress>(), Has.Count.EqualTo(1));
    Assert.That(result.Root.HomeAddressIndex, Is.EqualTo(result.Root.WorkAddressIndex));
}
```

## Test Naming Convention

Pattern: `MethodName_Scenario_ExpectedResult`

```csharp
[Test]
public void GetOrAddIndex_FirstObject_ReturnsZeroAndIsNew() { }

[Test]
public void GetOrAddIndex_EqualObject_ReturnsSameIndex() { }

[Test]
public void GetOrAddIndex_DifferentTypeKeys_TracksSeparately() { }

[Test]
public void AddToCollection_NegativeIndex_ThrowsArgumentOutOfRange() { }

[Test]
public void Normalize_WithCircularReference_DoesNotInfiniteLoop() { }

[Test]
public void Roundtrip_NormalizeThenDenormalize_ProducesEquivalentObject() { }
```

## Test Doubles

### Types of Test Doubles

| Double | Purpose | Example |
|--------|---------|---------|
| **Dummy** | Fills a parameter, never used | `NullLogger<T>.Instance` |
| **Stub** | Returns predetermined values | `FakeClock(fixedTime)` |
| **Spy** | Records calls for later verification | `SpyLogger` with `Entries` list |
| **Mock** | Verifies interactions | Moq or NSubstitute mock |
| **Fake** | Working implementation (simplified) | In-memory repository |

### Prefer Fakes Over Mocks

```csharp
// PREFERRED - Fake implementation
public sealed class FakeNormalizationEngine : INormalizationEngine
{
    public List<object> NormalizedObjects { get; } = [];

    public NormalizedResult<T> Normalize<T>(T source)
    {
        NormalizedObjects.Add(source!);
        return new NormalizedResult<T>(/* ... */);
    }
}

// USE WHEN NEEDED - Mock (for interaction verification)
[Test]
public void Service_LogsOnNormalization()
{
    var logger = new SpyLogger<NormalizerService>();
    var service = new NormalizerService(new FakeEngine(), Options.Create(new()), logger);

    service.Normalize(new TestPerson());

    Assert.That(logger.Entries, Has.Count.EqualTo(1));
    Assert.That(logger.Entries[0].Message, Does.Contain("Normalizing"));
}
```

### Test Record for Dedup Testing

```csharp
// Shared test helper
private sealed record TestDto(string Name, int Age) : IEquatable<TestDto>;
```

## One Logical Assertion Per Test

Each test should verify one logical concept. Multiple `Assert.That` calls are fine when they verify the same logical assertion.

```csharp
// CORRECT - one logical assertion (the index result)
[Test]
public void GetOrAddIndex_FirstObject_ReturnsZeroAndIsNew()
{
    var ctx = new NormalizationContext();
    var (index, isNew) = ctx.GetOrAddIndex("person", new TestDto("Alice", 30));

    Assert.That(index, Is.EqualTo(0));
    Assert.That(isNew, Is.True);
}

// WRONG - testing two unrelated behaviors
[Test]
public void Context_WorksCorrectly()
{
    var ctx = new NormalizationContext();
    ctx.GetOrAddIndex("person", new TestDto("Alice", 30));
    Assert.That(ctx.GetCollection<TestDto>("person"), Is.Empty); // separate concern
    ctx.AddToCollection("person", 0, new TestDto("Alice", 30));
    Assert.That(ctx.GetCollection<TestDto>("person"), Has.Count.EqualTo(1)); // separate concern
}
```

## Independent Tests (No Order Dependency)

Tests must not depend on execution order. Each test sets up its own state.

```csharp
// CORRECT - independent
[TestFixture]
public sealed class ContextTests
{
    [Test]
    public void Test1()
    {
        var ctx = new NormalizationContext(); // fresh instance
        // ...
    }

    [Test]
    public void Test2()
    {
        var ctx = new NormalizationContext(); // fresh instance
        // ...
    }
}

// ALSO CORRECT - shared setup via [SetUp]
[TestFixture]
public sealed class ContextTests
{
    private NormalizationContext _ctx = null!;

    [SetUp]
    public void SetUp()
    {
        _ctx = new NormalizationContext(); // fresh for each test
    }
}
```

## Verify.SourceGenerators for Snapshot Testing

For generator tests, use Verify to snapshot the generated output:

```csharp
[TestFixture]
public sealed class DtoEmitterTests
{
    [Test]
    public Task SimpleType_GeneratesCorrectDto()
    {
        var source = """
            using DataNormalizer;

            public class Person { public string Name { get; set; } }

            [NormalizeConfiguration]
            public partial class Config : NormalizationConfig
            {
                protected override void Configure(NormalizeBuilder builder)
                {
                    builder.NormalizeGraph<Person>();
                }
            }
            """;

        return Verify.VerifyGenerator(source);
    }
}
```

Snapshot files go in a `Snapshots/` directory adjacent to the test. The `.verified.cs` files are committed to source control.

## Commands

```bash
# Run all tests
dotnet test

# Run specific test project
dotnet test tests/DataNormalizer.Tests

# Filter by test class name
dotnet test --filter "FullyQualifiedName~NormalizationContextTests"

# Filter by test method name
dotnet test --filter "FullyQualifiedName~GetOrAddIndex_FirstObject"

# Verbose output
dotnet test --verbosity normal

# With logger
dotnet test --logger "trx;LogFileName=results.trx"
```
