using DataNormalizer.Runtime;
using NUnit.Framework;

namespace DataNormalizer.Tests.Runtime;

[TestFixture]
public sealed class NormalizedResultTests
{
    [Test]
    public void Root_ReturnsProvidedRoot()
    {
        var root = new TestRootDto(42);
        var result = CreateResult(root);
        Assert.That(result.Root, Is.SameAs(root));
    }

    [Test]
    public void RootIndex_ReturnsProvidedIndex()
    {
        var root = new TestRootDto(42);
        var result = new NormalizedResult<TestRootDto>(root, 5, new NormalizationContext());
        Assert.That(result.RootIndex, Is.EqualTo(5));
    }

    [Test]
    public void GetCollection_ReturnsStoredObjects()
    {
        var ctx = new NormalizationContext();
        var dto = new TestItemDto("Alice");
        ctx.AddToCollection("item", 0, dto);
        var result = new NormalizedResult<TestRootDto>(new TestRootDto(0), 0, ctx);
        var items = result.GetCollection<TestItemDto>("item");
        Assert.That(items, Has.Count.EqualTo(1));
        Assert.That(items[0].Name, Is.EqualTo("Alice"));
    }

    [Test]
    public void GetCollection_TypeOverload_DerivesKeyFromTypeName()
    {
        var ctx = new NormalizationContext();
        ctx.AddToCollection("TestItemDto", 0, new TestItemDto("Bob"));
        var result = new NormalizedResult<TestRootDto>(new TestRootDto(0), 0, ctx);
        var items = result.GetCollection<TestItemDto>();
        Assert.That(items, Has.Count.EqualTo(1));
    }

    [Test]
    public void GetCollection_EmptyType_ReturnsEmpty()
    {
        var result = CreateResult(new TestRootDto(0));
        Assert.That(result.GetCollection<TestItemDto>("nonexistent"), Is.Empty);
    }

    [Test]
    public void Resolve_ReturnsCorrectObject()
    {
        var ctx = new NormalizationContext();
        var dto1 = new TestItemDto("Alice");
        var dto2 = new TestItemDto("Bob");
        ctx.AddToCollection("item", 0, dto1);
        ctx.AddToCollection("item", 1, dto2);
        var result = new NormalizedResult<TestRootDto>(new TestRootDto(0), 0, ctx);
        Assert.That(result.Resolve<TestItemDto>("item", 0).Name, Is.EqualTo("Alice"));
        Assert.That(result.Resolve<TestItemDto>("item", 1).Name, Is.EqualTo("Bob"));
    }

    [Test]
    public void Resolve_NegativeIndex_Throws()
    {
        var ctx = new NormalizationContext();
        ctx.AddToCollection("item", 0, new TestItemDto("Alice"));
        var result = new NormalizedResult<TestRootDto>(new TestRootDto(0), 0, ctx);
        Assert.That(() => result.Resolve<TestItemDto>("item", -1), Throws.InstanceOf<ArgumentOutOfRangeException>());
    }

    [Test]
    public void Resolve_IndexOutOfRange_Throws()
    {
        var ctx = new NormalizationContext();
        ctx.AddToCollection("item", 0, new TestItemDto("Alice"));
        var result = new NormalizedResult<TestRootDto>(new TestRootDto(0), 0, ctx);
        Assert.That(() => result.Resolve<TestItemDto>("item", 5), Throws.InstanceOf<ArgumentOutOfRangeException>());
    }

    [Test]
    public void CollectionNames_ReturnsAllKeys()
    {
        var ctx = new NormalizationContext();
        ctx.AddToCollection("person", 0, new TestItemDto("Alice"));
        ctx.AddToCollection("address", 0, new TestItemDto("Home"));
        var result = new NormalizedResult<TestRootDto>(new TestRootDto(0), 0, ctx);
        Assert.That(result.CollectionNames, Is.EquivalentTo(new[] { "person", "address" }));
    }

    private static NormalizedResult<TestRootDto> CreateResult(TestRootDto root)
    {
        return new NormalizedResult<TestRootDto>(root, 0, new NormalizationContext());
    }

    [Test]
    public void Constructor_NullRoot_Throws()
    {
        Assert.That(
            () => new NormalizedResult<TestRootDto>(null!, 0, new NormalizationContext()),
            Throws.ArgumentNullException.With.Property("ParamName").EqualTo("root")
        );
    }

    [Test]
    public void Constructor_NullContext_Throws()
    {
        Assert.That(
            () => new NormalizedResult<TestRootDto>(new TestRootDto(0), 0, null!),
            Throws.ArgumentNullException.With.Property("ParamName").EqualTo("context")
        );
    }

    [Test]
    public void Resolve_NonexistentTypeKey_Throws()
    {
        var result = CreateResult(new TestRootDto(0));
        Assert.That(
            () => result.Resolve<TestItemDto>("nonexistent", 0),
            Throws.InstanceOf<ArgumentOutOfRangeException>()
        );
    }

    private sealed record TestRootDto(int Id);

    private sealed class TestItemDto(string name)
    {
        public string Name { get; } = name;
    }
}
