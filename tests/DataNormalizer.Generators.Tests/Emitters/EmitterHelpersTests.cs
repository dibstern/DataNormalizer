using System.Collections.Immutable;
using DataNormalizer.Generators.Emitters;
using DataNormalizer.Generators.Models;
using NUnit.Framework;

namespace DataNormalizer.Generators.Tests.Emitters;

[TestFixture]
public sealed class EmitterHelpersTests
{
    private static readonly NamingModel DefaultNaming = NamingModel.Default;

    // ---- GetDtoName ----

    [Test]
    public void GetDtoName_DefaultNaming_AppendsDtoSuffix()
    {
        var result = EmitterHelpers.GetDtoName("Person", DefaultNaming);

        Assert.That(result, Is.EqualTo("PersonDto"));
    }

    [Test]
    public void GetDtoName_CustomPrefix_PrependsPrefixWithNoSuffix()
    {
        var naming = new NamingModel { DtoPrefix = "Normalized", DtoSuffix = "" };

        var result = EmitterHelpers.GetDtoName("Person", naming);

        Assert.That(result, Is.EqualTo("NormalizedPerson"));
    }

    [Test]
    public void GetDtoName_Address_DefaultNaming_AppendsDto()
    {
        var result = EmitterHelpers.GetDtoName("Address", DefaultNaming);

        Assert.That(result, Is.EqualTo("AddressDto"));
    }

    // ---- GetContainerName ----

    [Test]
    public void GetContainerName_DefaultNaming_AppendsResultAndContainerSuffix()
    {
        var result = EmitterHelpers.GetContainerName("Person", DefaultNaming);

        Assert.That(result, Is.EqualTo("PersonResultDto"));
    }

    [Test]
    public void GetContainerName_EmptyContainerSuffix_AppendsResultOnly()
    {
        var naming = new NamingModel { ContainerSuffix = "" };

        var result = EmitterHelpers.GetContainerName("Person", naming);

        Assert.That(result, Is.EqualTo("PersonResult"));
    }

    // ---- GetDtoFullName (with NamingModel) ----

    [Test]
    public void GetDtoFullName_WithNamespace_DefaultNaming_PrependsDotSeparatedNamespace()
    {
        var result = EmitterHelpers.GetDtoFullName("TestApp.Person", "Person", DefaultNaming);

        Assert.That(result, Is.EqualTo("TestApp.PersonDto"));
    }

    [Test]
    public void GetDtoFullName_NoNamespace_ReturnsJustDtoName()
    {
        var result = EmitterHelpers.GetDtoFullName("Person", "Person", DefaultNaming);

        Assert.That(result, Is.EqualTo("PersonDto"));
    }

    // ---- GetContainerFullName (with NamingModel) ----

    [Test]
    public void GetContainerFullName_WithNamespace_DefaultNaming_PrependsDotSeparatedNamespace()
    {
        var result = EmitterHelpers.GetContainerFullName("TestApp.Person", "Person", DefaultNaming);

        Assert.That(result, Is.EqualTo("TestApp.PersonResultDto"));
    }

    [Test]
    public void GetContainerFullName_NoNamespace_ReturnsJustContainerName()
    {
        var result = EmitterHelpers.GetContainerFullName("Person", "Person", DefaultNaming);

        Assert.That(result, Is.EqualTo("PersonResultDto"));
    }

    // ---- GetListPropertyName (with NamingModel) ----

    [Test]
    public void GetListPropertyName_DefaultNaming_PluralizesDtoSuffix()
    {
        var node = CreateNode("TestApp.Person", "Person");
        var allNodes = new[] { node };

        var result = EmitterHelpers.GetListPropertyName(node, allNodes, DefaultNaming);

        Assert.That(result, Is.EqualTo("PersonDtos"));
    }

    [Test]
    public void GetListPropertyName_EmptySuffix_FallsBackToListSuffix()
    {
        var naming = new NamingModel { DtoSuffix = "" };
        var node = CreateNode("TestApp.Person", "Person");
        var allNodes = new[] { node };

        var result = EmitterHelpers.GetListPropertyName(node, allNodes, naming);

        Assert.That(result, Is.EqualTo("PersonList"));
    }

    [Test]
    public void GetListPropertyName_EntitySuffix_PluralizesEntity()
    {
        var naming = new NamingModel { DtoSuffix = "Entity" };
        var node = CreateNode("TestApp.Person", "Person");
        var allNodes = new[] { node };

        var result = EmitterHelpers.GetListPropertyName(node, allNodes, naming);

        Assert.That(result, Is.EqualTo("PersonEntities"));
    }

    [Test]
    public void GetListPropertyName_DuplicateTypeNames_AddsNamespacePrefix()
    {
        var node1 = CreateNode("App.Models.Person", "Person");
        var node2 = CreateNode("App.Dto.Person", "Person");
        var allNodes = new[] { node1, node2 };

        var result = EmitterHelpers.GetListPropertyName(node1, allNodes, DefaultNaming);

        Assert.That(result, Is.EqualTo("AppModelsPersonDtos"));
    }

    [Test]
    public void GetListPropertyName_Address_DefaultNaming_PluralizesCorrectly()
    {
        var node = CreateNode("TestApp.Address", "Address");
        var allNodes = new[] { node };

        var result = EmitterHelpers.GetListPropertyName(node, allNodes, DefaultNaming);

        Assert.That(result, Is.EqualTo("AddressDtos"));
    }

    // ---- Helpers ----

    private static TypeGraphNode CreateNode(string fullName, string name)
    {
        return new TypeGraphNode
        {
            TypeFullName = fullName,
            TypeName = name,
            Properties = ImmutableArray<AnalyzedProperty>.Empty,
        };
    }
}
