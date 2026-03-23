---
name: dotnet-tdd
description: Test-Driven Development workflow for the DataNormalizer project using NUnit 4. Covers Red-Green-Refactor cycle, NUnit 4 specifics (Test/TestCase/Assert.That constraint syntax), AAA pattern, test naming, test doubles, Verify.SourceGenerators snapshot testing, and commands for running tests.
---

# Test-Driven Development with NUnit 4

## Red-Green-Refactor Cycle

```
 ┌─────────────────────────────────────────────┐
 │                                             │
 │   ┌───────┐   ┌─────────┐   ┌──────────┐   │
 │   │  RED  │──▶│  GREEN  │──▶│ REFACTOR │───┘
 │   └───────┘   └─────────┘   └──────────┘
 │
 │   Write a      Write the     Improve code
 │   failing      minimal       while keeping
 │   test         code to       tests green
 │                pass
```

### Phase 1: RED — Write a Failing Test

Rules:
- Write the test BEFORE any implementation code
- The test MUST fail (red) when first run — if it passes, something is wrong
- Test name follows `MethodName_Scenario_ExpectedResult` convention
- Use AAA pattern (Arrange-Act-Assert)
- Categorize with `[Category("Unit")]` or `[Category("Integration")]`

```csharp
// 1. RED - Write the test first (NormalizationContext doesn't exist yet)
[Test]
[Category("Unit")]
public void GetOrAddIndex_FirstObject_ReturnsZeroAndIsNew()
{
    // Arrange
    var ctx = new NormalizationContext();
    var dto = new TestDto("Alice", 30);

    // Act
    var (index, isNew) = ctx.GetOrAddIndex("person", dto);

    // Assert
    Assert.That(index, Is.EqualTo(0));
    Assert.That(isNew, Is.True);
}
```

### Phase 2: GREEN — Make It Pass

Rules:
- Write the MINIMUM code to make the test pass — no more
- "Fake it till you make it": hardcoded values are fine initially
- Don't optimize, don't refactor, don't add features
- Only fix the currently failing test

```csharp
// 2. GREEN - Implement the minimal code
public sealed class NormalizationContext
{
    private readonly Dictionary<string, object> maps = new();

    public (int Index, bool IsNew) GetOrAddIndex<TDto>(string typeKey, TDto dto)
        where TDto : IEquatable<TDto>
    {
        if (!maps.TryGetValue(typeKey, out var mapObj))
        {
            mapObj = new Dictionary<TDto, int>();
            maps[typeKey] = mapObj;
        }
        var map = (Dictionary<TDto, int>)mapObj;
        if (map.TryGetValue(dto, out var idx))
            return (idx, false);
        var newIdx = map.Count;
        map[dto] = newIdx;
        return (newIdx, true);
    }
}
```

### Phase 3: REFACTOR — Improve the Code

Rules:
- Run tests after EVERY change — they must stay green
- Refactoring checklist:
  - [ ] Extract methods for repeated logic
  - [ ] Rename for clarity
  - [ ] Remove duplication (DRY)
  - [ ] Simplify conditionals
  - [ ] Apply project coding standards (`sealed`, `record`, file-scoped namespaces)
- Common refactorings: Extract Method, Extract Class, Rename, Inline Variable, Replace Conditional with Pattern Matching

## CRITICAL: NUnit 4, NOT xUnit

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
| `[Category("Unit")]` | `[Trait("Category", "Unit")]` |
| `[Ignore("Reason")]` | `[Skip("Reason")]` |
| `Assert.That(x, Is.EqualTo(y))` | `Assert.Equal(y, x)` |
| `Assert.That(x, Is.True)` | `Assert.True(x)` |
| `Assert.That(x, Is.Not.Null)` | `Assert.NotNull(x)` |

### Test Class Structure

```csharp
[TestFixture]
public sealed class NormalizationContextTests
{
    private NormalizationContext context = null!;

    [SetUp]
    public void SetUp()
    {
        // Fresh instance for each test (replaces xUnit constructor)
        context = new NormalizationContext();
    }

    [TearDown]
    public void TearDown()
    {
        // Cleanup if needed (replaces xUnit IDisposable)
    }

    [Test]
    public void GetOrAddIndex_FirstObject_ReturnsZero()
    {
        var (index, _) = context.GetOrAddIndex("person", new TestDto("Alice", 30));
        Assert.That(index, Is.EqualTo(0));
    }

    [TestCase("Alice", 30, 0)]
    [TestCase("Bob", 25, 1)]
    public void GetOrAddIndex_MultipleObjects_ReturnsSequentialIndices(string name, int age, int expected)
    {
        context.GetOrAddIndex("person", new TestDto("Alice", 30));
        if (name != "Alice")
        {
            var (index, _) = context.GetOrAddIndex("person", new TestDto(name, age));
            Assert.That(index, Is.EqualTo(expected));
        }
    }
}
```

### One-Time Setup (Replaces IClassFixture)

```csharp
[TestFixture]
public sealed class ExpensiveResourceTests
{
    private static ExpensiveResource resource = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        // Runs once before all tests in this fixture
        resource = new ExpensiveResource();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        // Runs once after all tests in this fixture
        resource.Dispose();
    }

    [Test]
    public void Test1() { /* uses resource */ }

    [Test]
    public void Test2() { /* uses resource */ }
}
```

## Assert.That Constraint Syntax (Complete Reference)

Always use the constraint-based `Assert.That()` syntax:

```csharp
// === Equality ===
Assert.That(result, Is.EqualTo(expected));
Assert.That(result, Is.Not.EqualTo(unexpected));

// === Null ===
Assert.That(result, Is.Null);
Assert.That(result, Is.Not.Null);

// === Boolean ===
Assert.That(isNew, Is.True);
Assert.That(exists, Is.False);

// === String ===
Assert.That(message, Does.Contain("error"));
Assert.That(name, Does.StartWith("Normalized"));
Assert.That(name, Does.EndWith("Dto"));
Assert.That(name, Does.Match("^Normal.*Dto$"));
Assert.That(name, Is.EqualTo("person").IgnoreCase);
Assert.That(source, Is.Empty);
Assert.That(source, Is.Not.Empty);

// === Collections ===
Assert.That(collection, Has.Count.EqualTo(3));
Assert.That(collection, Is.Empty);
Assert.That(collection, Is.Not.Empty);
Assert.That(collection, Has.Exactly(2).Items);
Assert.That(collection, Does.Contain(item));
Assert.That(collection, Is.All.Not.Null);
Assert.That(collection, Is.Ordered);
Assert.That(collection, Is.Ordered.Descending);
Assert.That(collection, Is.Unique);
Assert.That(collection, Has.Member(item));
Assert.That(collection, Has.No.Member(item));
Assert.That(collection, Is.EquivalentTo(expected));   // same items, any order
Assert.That(collection, Is.SubsetOf(superset));

// === Numeric ===
Assert.That(value, Is.GreaterThan(0));
Assert.That(value, Is.LessThan(100));
Assert.That(value, Is.GreaterThanOrEqualTo(1));
Assert.That(value, Is.InRange(1, 10));
Assert.That(value, Is.Positive);
Assert.That(value, Is.Negative);
Assert.That(value, Is.Zero);
Assert.That(actual, Is.EqualTo(3.14).Within(0.01));   // floating point

// === Type ===
Assert.That(result, Is.InstanceOf<NormalizedTestDtoResult>());
Assert.That(result, Is.AssignableTo<IEquatable<TestDto>>());

// === Exceptions ===
Assert.That(() => context.AddToCollection("key", -1, obj),
    Throws.InstanceOf<ArgumentOutOfRangeException>());

Assert.That(() => new NormalizationContext().GetCollection<TestDto>("missing"),
    Throws.InstanceOf<KeyNotFoundException>());

Assert.That(() => DoSomething(), Throws.Nothing);  // no exception

// === Async Exceptions ===
Assert.That(async () => await service.ProcessAsync(null!),
    Throws.InstanceOf<ArgumentNullException>());

// === Reference Equality ===
Assert.That(actual, Is.SameAs(expected));
Assert.That(actual, Is.Not.SameAs(other));

// === Compound ===
Assert.That(value, Is.GreaterThan(0).And.LessThan(100));
Assert.That(name, Is.Not.Null.And.Not.Empty);
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
    Assert.That(result.AddressList, Has.Length.EqualTo(1));
    var root = result.PersonList[result.RootIndex];
    Assert.That(root.HomeAddressIndex, Is.EqualTo(root.WorkAddressIndex));
}
```

## Test Naming Convention

Pattern: `MethodName_Scenario_ExpectedResult`

```csharp
[Test] public void GetOrAddIndex_FirstObject_ReturnsZeroAndIsNew() { }
[Test] public void GetOrAddIndex_EqualObject_ReturnsSameIndex() { }
[Test] public void GetOrAddIndex_DifferentTypeKeys_TracksSeparately() { }
[Test] public void AddToCollection_NegativeIndex_ThrowsArgumentOutOfRange() { }
[Test] public void Normalize_WithCircularReference_DoesNotInfiniteLoop() { }
[Test] public void Roundtrip_NormalizeThenDenormalize_ProducesEquivalentObject() { }
```

## Test Doubles

### Types of Test Doubles

| Double | Purpose | When to Use |
|--------|---------|-------------|
| **Dummy** | Fills a parameter, never used | `NullLogger<T>.Instance` |
| **Stub** | Returns predetermined values | `FakeClock(fixedTime)` |
| **Spy** | Records calls for verification | `SpyLogger` with `Entries` list |
| **Mock** | Verifies interactions | Moq or NSubstitute mock |
| **Fake** | Working simplified implementation | In-memory repository |

### Prefer Fakes Over Mocks

```csharp
// PREFERRED - Fake implementation
public sealed class FakeNormalizationEngine : INormalizationEngine
{
    public List<object> NormalizedObjects { get; } = [];

    public object Normalize<T>(T source)
    {
        NormalizedObjects.Add(source!);
        return new { }; // Return a fake container DTO
    }
}

// USE WHEN NEEDED - Spy (for interaction verification)
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
    Assert.That(ctx.GetCollection<TestDto>("person"), Is.Empty); // separate concern — test separately
    ctx.AddToCollection("person", 0, new TestDto("Alice", 30));
    Assert.That(ctx.GetCollection<TestDto>("person"), Has.Count.EqualTo(1)); // separate concern — test separately
}
```

## Independent Tests (No Order Dependency)

Tests must not depend on execution order. Each test sets up its own state.

```csharp
// CORRECT - shared setup via [SetUp]
[TestFixture]
public sealed class ContextTests
{
    private NormalizationContext ctx = null!;

    [SetUp]
    public void SetUp()
    {
        ctx = new NormalizationContext(); // fresh for each test
    }

    [Test]
    public void Test1() { /* uses ctx */ }

    [Test]
    public void Test2() { /* uses ctx */ }
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

Snapshot files go in a `Snapshots/` directory adjacent to the test. The `.verified.cs` files are committed to source control. The `.received.cs` files are generated on test failure for diffing and should be in `.gitignore`.

### Updating Snapshots

When generator output intentionally changes:
1. Run the tests (they will fail)
2. Review the `.received.cs` diff
3. Accept the new snapshot: copy `.received.cs` → `.verified.cs` (or use Verify's auto-accept)
4. Re-run tests to confirm they pass

## Anti-Patterns

### Testing Implementation Details

```csharp
// WRONG - testing private internals
[Test]
public void GetOrAddIndex_CreatesDictionaryInternally()
{
    var ctx = new NormalizationContext();
    // Reflection to check internal dictionary — brittle!
    var field = typeof(NormalizationContext).GetField("maps", BindingFlags.NonPublic | BindingFlags.Instance);
    Assert.That(field!.GetValue(ctx), Is.Not.Null);
}

// CORRECT - test behavior through public API
[Test]
public void GetOrAddIndex_SameObjectTwice_ReturnsSameIndex()
{
    var ctx = new NormalizationContext();
    var dto = new TestDto("Alice", 30);

    var first = ctx.GetOrAddIndex("person", dto);
    var second = ctx.GetOrAddIndex("person", dto);

    Assert.That(first.Index, Is.EqualTo(second.Index));
    Assert.That(second.IsNew, Is.False);
}
```

### Order-Dependent Tests

```csharp
// WRONG - Test2 depends on Test1 running first
private static int sharedCounter;

[Test] public void Test1() { sharedCounter = 1; }
[Test] public void Test2() { Assert.That(sharedCounter, Is.EqualTo(1)); } // fragile!
```

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

# Filter by category
dotnet test --filter "Category=Unit"

# Verbose output
dotnet test --verbosity normal

# With TRX logger
dotnet test --logger "trx;LogFileName=results.trx"

# Run and update Verify snapshots
dotnet test -- Verify.AutoVerify=true
```
