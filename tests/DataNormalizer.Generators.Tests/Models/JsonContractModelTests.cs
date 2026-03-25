using System.Collections.Immutable;
using DataNormalizer.Generators.Models;
using NUnit.Framework;

namespace DataNormalizer.Generators.Tests.Models;

[TestFixture]
public sealed class JsonContractModelTests
{
    [Test]
    public void Equals_IdenticalInstances_ReturnsTrue()
    {
        var a = new JsonContractModel
        {
            RootPropertyName = "data",
            CollectionJsonNames = ImmutableDictionary<string, string>
                .Empty.Add("Person", "people")
                .Add("Address", "addresses"),
        };
        var b = new JsonContractModel
        {
            RootPropertyName = "data",
            CollectionJsonNames = ImmutableDictionary<string, string>
                .Empty.Add("Person", "people")
                .Add("Address", "addresses"),
        };

        Assert.That(a.Equals(b), Is.True);
        Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
    }

    [Test]
    public void Equals_DifferentRootPropertyName_ReturnsFalse()
    {
        var a = new JsonContractModel { RootPropertyName = "data" };
        var b = new JsonContractModel { RootPropertyName = "root" };

        Assert.That(a.Equals(b), Is.False);
    }

    [Test]
    public void Equals_BothRootPropertyNameNull_ReturnsTrue()
    {
        var a = new JsonContractModel { RootPropertyName = null };
        var b = new JsonContractModel { RootPropertyName = null };

        Assert.That(a.Equals(b), Is.True);
        Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
    }

    [Test]
    public void Equals_OneNullOneNonNullRootPropertyName_ReturnsFalse()
    {
        var a = new JsonContractModel { RootPropertyName = null };
        var b = new JsonContractModel { RootPropertyName = "data" };

        Assert.That(a.Equals(b), Is.False);
    }

    [Test]
    public void Equals_SameCountDifferentKeys_ReturnsFalse()
    {
        var a = new JsonContractModel
        {
            CollectionJsonNames = ImmutableDictionary<string, string>.Empty.Add("Person", "people"),
        };
        var b = new JsonContractModel
        {
            CollectionJsonNames = ImmutableDictionary<string, string>.Empty.Add("Address", "people"),
        };

        Assert.That(a.Equals(b), Is.False);
    }

    [Test]
    public void GetHashCode_SameCountDifferentKeys_Differs()
    {
        var a = new JsonContractModel
        {
            CollectionJsonNames = ImmutableDictionary<string, string>.Empty.Add("Person", "people"),
        };
        var b = new JsonContractModel
        {
            CollectionJsonNames = ImmutableDictionary<string, string>.Empty.Add("Address", "people"),
        };

        Assert.That(a.GetHashCode(), Is.Not.EqualTo(b.GetHashCode()));
    }

    [Test]
    public void Equals_SameCountDifferentValues_ReturnsFalse()
    {
        var a = new JsonContractModel
        {
            CollectionJsonNames = ImmutableDictionary<string, string>.Empty.Add("Person", "people"),
        };
        var b = new JsonContractModel
        {
            CollectionJsonNames = ImmutableDictionary<string, string>.Empty.Add("Person", "persons"),
        };

        Assert.That(a.Equals(b), Is.False);
    }

    [Test]
    public void Equals_EmptyVsNonEmptyCollectionJsonNames_ReturnsFalse()
    {
        var a = new JsonContractModel();
        var b = new JsonContractModel
        {
            CollectionJsonNames = ImmutableDictionary<string, string>.Empty.Add("Person", "people"),
        };

        Assert.That(a.Equals(b), Is.False);
    }

    [Test]
    public void Default_EqualsNewInstance_ReturnsTrue()
    {
        Assert.That(JsonContractModel.Default.Equals(new JsonContractModel()), Is.True);
    }

    [Test]
    public void GetHashCode_EqualObjects_ProduceEqualHashes()
    {
        var a = new JsonContractModel
        {
            RootPropertyName = "data",
            CollectionJsonNames = ImmutableDictionary<string, string>
                .Empty.Add("Person", "people")
                .Add("Address", "addresses")
                .Add("Order", "orders"),
        };
        var b = new JsonContractModel
        {
            RootPropertyName = "data",
            CollectionJsonNames = ImmutableDictionary<string, string>
                .Empty.Add("Person", "people")
                .Add("Address", "addresses")
                .Add("Order", "orders"),
        };

        Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
    }

    [Test]
    public void GetHashCode_OrderIndependence_SameHashForDifferentInsertionOrder()
    {
        var a = new JsonContractModel
        {
            RootPropertyName = "data",
            CollectionJsonNames = ImmutableDictionary<string, string>
                .Empty.Add("Person", "people")
                .Add("Address", "addresses")
                .Add("Order", "orders"),
        };
        var b = new JsonContractModel
        {
            RootPropertyName = "data",
            CollectionJsonNames = ImmutableDictionary<string, string>
                .Empty.Add("Order", "orders")
                .Add("Person", "people")
                .Add("Address", "addresses"),
        };

        Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
    }

    [Test]
    public void Equals_Null_ReturnsFalse()
    {
        var model = new JsonContractModel();

        Assert.That(model.Equals(null), Is.False);
    }

    [Test]
    public void Equals_SameReference_ReturnsTrue()
    {
        var model = new JsonContractModel();

        Assert.That(model.Equals(model), Is.True);
    }

    [Test]
    public void Equals_ObjectOverload_WorksCorrectly()
    {
        var a = new JsonContractModel { RootPropertyName = "data" };
        object b = new JsonContractModel { RootPropertyName = "data" };
        object c = "not a JsonContractModel";

        Assert.That(a.Equals(b), Is.True);
        Assert.That(a.Equals(c), Is.False);
    }
}
