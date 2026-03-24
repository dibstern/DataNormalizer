using DataNormalizer.Generators.Models;
using NUnit.Framework;

namespace DataNormalizer.Generators.Tests.Models;

[TestFixture]
public sealed class NamingModelTests
{
    [Test]
    public void Equals_IdenticalInstances_ReturnsTrue()
    {
        var a = new NamingModel
        {
            DtoPrefix = "N",
            DtoSuffix = "Dto",
            ContainerSuffix = "Result",
            EmitJsonPropertyNames = true,
        };
        var b = new NamingModel
        {
            DtoPrefix = "N",
            DtoSuffix = "Dto",
            ContainerSuffix = "Result",
            EmitJsonPropertyNames = true,
        };

        Assert.That(a.Equals(b), Is.True);
        Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
    }

    [Test]
    public void Equals_DifferingDtoSuffix_ReturnsFalse()
    {
        var a = new NamingModel { DtoSuffix = "Dto" };
        var b = new NamingModel { DtoSuffix = "Record" };

        Assert.That(a.Equals(b), Is.False);
    }

    [Test]
    public void Equals_DifferingDtoPrefix_ReturnsFalse()
    {
        var a = new NamingModel { DtoPrefix = "" };
        var b = new NamingModel { DtoPrefix = "Normalized" };

        Assert.That(a.Equals(b), Is.False);
    }

    [Test]
    public void Equals_DifferingContainerSuffix_ReturnsFalse()
    {
        var a = new NamingModel { ContainerSuffix = "Dto" };
        var b = new NamingModel { ContainerSuffix = "Result" };

        Assert.That(a.Equals(b), Is.False);
    }

    [Test]
    public void Equals_DifferingEmitJsonPropertyNames_ReturnsFalse()
    {
        var a = new NamingModel { EmitJsonPropertyNames = true };
        var b = new NamingModel { EmitJsonPropertyNames = false };

        Assert.That(a.Equals(b), Is.False);
    }

    [Test]
    public void Default_EqualsNewInstance_ReturnsTrue()
    {
        Assert.That(NamingModel.Default.Equals(new NamingModel()), Is.True);
    }

    [Test]
    public void Equals_Null_ReturnsFalse()
    {
        var model = new NamingModel();

        Assert.That(model.Equals(null), Is.False);
    }

    [Test]
    public void Equals_SameReference_ReturnsTrue()
    {
        var model = new NamingModel();

        Assert.That(model.Equals(model), Is.True);
    }

    [Test]
    public void Equals_ObjectOverload_WorksCorrectly()
    {
        var a = new NamingModel { DtoSuffix = "Dto" };
        object b = new NamingModel { DtoSuffix = "Dto" };
        object c = "not a NamingModel";

        Assert.That(a.Equals(b), Is.True);
        Assert.That(a.Equals(c), Is.False);
    }
}
