using DataNormalizer.Runtime;
using NUnit.Framework;

namespace DataNormalizer.Tests.Runtime;

[TestFixture]
public sealed class NormalizationContextTests
{
    [Test]
    public void GetOrAddIndex_FirstObject_ReturnsZeroAndIsNew()
    {
        var ctx = new NormalizationContext();
        var dto = new TestDto("Alice", 30);
        var (index, isNew) = ctx.GetOrAddIndex("person", dto);
        Assert.That(index, Is.EqualTo(0));
        Assert.That(isNew, Is.True);
    }

    [Test]
    public void GetOrAddIndex_EqualObject_ReturnsSameIndexAndNotNew()
    {
        var ctx = new NormalizationContext();
        var dto1 = new TestDto("Alice", 30);
        var dto2 = new TestDto("Alice", 30); // different reference, same value
        ctx.GetOrAddIndex("person", dto1);
        var (index, isNew) = ctx.GetOrAddIndex("person", dto2);
        Assert.That(index, Is.EqualTo(0));
        Assert.That(isNew, Is.False);
    }

    [Test]
    public void GetOrAddIndex_DifferentObject_ReturnsDifferentIndex()
    {
        var ctx = new NormalizationContext();
        ctx.GetOrAddIndex("person", new TestDto("Alice", 30));
        var (index, _) = ctx.GetOrAddIndex("person", new TestDto("Bob", 25));
        Assert.That(index, Is.EqualTo(1));
    }

    [Test]
    public void GetOrAddIndex_DifferentTypeKeys_TrackSeparately()
    {
        var ctx = new NormalizationContext();
        var (i1, _) = ctx.GetOrAddIndex("person", new TestDto("Alice", 30));
        var (i2, _) = ctx.GetOrAddIndex("address", new TestDto("Alice", 30));
        Assert.That(i1, Is.EqualTo(0));
        Assert.That(i2, Is.EqualTo(0)); // separate namespace
    }

    [Test]
    public void AddToCollection_StoresAndRetrieves()
    {
        var ctx = new NormalizationContext();
        var obj = new TestDto("Alice", 30);
        ctx.AddToCollection("person", 0, obj);
        var collection = ctx.GetCollection<TestDto>("person");
        Assert.That(collection, Has.Count.EqualTo(1));
        Assert.That(collection[0], Is.EqualTo(obj));
    }

    [Test]
    public void GetCollection_TypeOverload_DerivesKeyFromTypeName()
    {
        var ctx = new NormalizationContext();
        ctx.AddToCollection("TestDto", 0, new TestDto("Alice", 30));
        var collection = ctx.GetCollection<TestDto>();
        Assert.That(collection, Has.Count.EqualTo(1));
    }

    [Test]
    public void GetCollection_EmptyType_ReturnsEmpty()
    {
        var ctx = new NormalizationContext();
        Assert.That(ctx.GetCollection<TestDto>("person"), Is.Empty);
    }

    [Test]
    public void AddToCollection_NegativeIndex_Throws()
    {
        var ctx = new NormalizationContext();
        Assert.That(
            () => ctx.AddToCollection("person", -1, new TestDto("A", 1)),
            Throws.InstanceOf<ArgumentOutOfRangeException>()
        );
    }

    [Test]
    public void AddToCollection_GapInIndices_FillsWithDefault()
    {
        var ctx = new NormalizationContext();
        var dto = new TestDto("Alice", 30);
        ctx.AddToCollection("person", 2, dto);
        var collection = ctx.GetCollection<TestDto>("person");
        Assert.That(collection, Has.Count.EqualTo(3));
        Assert.That(collection[2], Is.EqualTo(dto));
    }

    [Test]
    public void CollectionNames_ReturnsAllKeys()
    {
        var ctx = new NormalizationContext();
        ctx.AddToCollection("person", 0, new TestDto("Alice", 30));
        ctx.AddToCollection("address", 0, new TestDto("Home", 1));
        Assert.That(ctx.CollectionNames, Is.EquivalentTo(new[] { "person", "address" }));
    }

    [Test]
    public void AddToCollection_OverwriteAtExistingIndex_ReplacesValue()
    {
        var ctx = new NormalizationContext();
        var dto1 = new TestDto("Alice", 30);
        var dto2 = new TestDto("Bob", 25);
        ctx.AddToCollection("person", 0, dto1);
        ctx.AddToCollection("person", 0, dto2);
        var collection = ctx.GetCollection<TestDto>("person");
        Assert.That(collection, Has.Count.EqualTo(1));
        Assert.That(collection[0], Is.EqualTo(dto2));
    }

    [Test]
    public void GetOrAddIndex_DifferentDtoTypeUnderSameKey_ThrowsInvalidCast()
    {
        var ctx = new NormalizationContext();
        ctx.GetOrAddIndex("shared", new TestDto("Alice", 30));

        Assert.That(
            () => ctx.GetOrAddIndex("shared", new OtherDto("Value")),
            Throws.InstanceOf<InvalidCastException>()
        );
    }

    [Test]
    public void TryGetTrackedIndex_NotTracked_ReturnsFalse()
    {
        var ctx = new NormalizationContext();
        Assert.That(ctx.TryGetTrackedIndex(new object(), out _), Is.False);
    }

    [Test]
    public void TrackSource_ThenTryGet_ReturnsIndex()
    {
        var ctx = new NormalizationContext();
        var source = new object();
        ctx.TrackSource(source, 42);
        Assert.That(ctx.TryGetTrackedIndex(source, out var index), Is.True);
        Assert.That(index, Is.EqualTo(42));
    }

    [Test]
    public void TryGetTrackedIndex_UsesReferenceEquality()
    {
        var ctx = new NormalizationContext();
        var source = new TestDto("Alice", 30);
        var clone = new TestDto("Alice", 30);
        ctx.TrackSource(source, 7);
        Assert.That(ctx.TryGetTrackedIndex(source, out var idx), Is.True);
        Assert.That(idx, Is.EqualTo(7));
        Assert.That(ctx.TryGetTrackedIndex(clone, out _), Is.False);
    }

    private sealed record TestDto(string Name, int Age) : IEquatable<TestDto>;

    private sealed record OtherDto(string Value) : IEquatable<OtherDto>;
}
