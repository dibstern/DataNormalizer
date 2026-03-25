using System.Collections.Generic;
using System.Collections.Immutable;
using DataNormalizer.Generators.Emitters;
using DataNormalizer.Generators.Models;
using NUnit.Framework;

namespace DataNormalizer.Generators.Tests.Emitters;

[TestFixture]
public sealed class ContainerEmitterTests
{
    private static readonly NamingModel DefaultNaming = NamingModel.Default;

    private static readonly NamingModel NoJsonNaming = new NamingModel { EmitJsonPropertyNames = false };

    [Test]
    public void Emit_SimpleGraph_GeneratesContainerWithEntityLists()
    {
        var personNode = CreateNode(
            "TestApp.Person",
            "Person",
            SimpleProp("Name", "string", isRef: true),
            NormalizedProp("HomeAddress", "TestApp.Address", nullable: false)
        );
        var addressNode = CreateNode("TestApp.Address", "Address", SimpleProp("Street", "string", isRef: true));
        var allNodes = new List<TypeGraphNode> { personNode, addressNode };

        var result = ContainerEmitter.Emit(personNode, allNodes, DefaultNaming);

        Assert.That(result, Does.Contain("public partial class PersonResultDto"));
        Assert.That(result, Does.Not.Contain("RootIndex"));
        Assert.That(
            result,
            Does.Contain(
                "public TestApp.PersonDto[] PersonDtos { get; set; } = System.Array.Empty<TestApp.PersonDto>();"
            )
        );
        Assert.That(
            result,
            Does.Contain(
                "public TestApp.AddressDto[] AddressDtos { get; set; } = System.Array.Empty<TestApp.AddressDto>();"
            )
        );
    }

    [Test]
    public void Emit_DefaultNaming_EmitsJsonPropertyNameAttributes()
    {
        var personNode = CreateNode("TestApp.Person", "Person", SimpleProp("Name", "string", isRef: true));
        var addressNode = CreateNode("TestApp.Address", "Address", SimpleProp("Street", "string", isRef: true));
        var allNodes = new List<TypeGraphNode> { personNode, addressNode };

        var result = ContainerEmitter.Emit(personNode, allNodes, DefaultNaming);

        Assert.That(result, Does.Not.Contain("rootIndex"));
        Assert.That(result, Does.Contain("[System.Text.Json.Serialization.JsonPropertyName(\"personDtos\")]"));
        Assert.That(result, Does.Contain("[System.Text.Json.Serialization.JsonPropertyName(\"addressDtos\")]"));
    }

    [Test]
    public void Emit_Namespace_ContainerEmittedInRootTypeNamespace()
    {
        var personNode = CreateNode("TestApp.Models.Person", "Person", SimpleProp("Name", "string", isRef: true));
        var allNodes = new List<TypeGraphNode> { personNode };

        var result = ContainerEmitter.Emit(personNode, allNodes, NoJsonNaming);

        Assert.That(result, Does.Contain("namespace TestApp.Models;"));
    }

    [Test]
    public void Emit_NoIEquatable_ContainerDoesNotImplementIEquatable()
    {
        var personNode = CreateNode("TestApp.Person", "Person", SimpleProp("Name", "string", isRef: true));
        var allNodes = new List<TypeGraphNode> { personNode };

        var result = ContainerEmitter.Emit(personNode, allNodes, NoJsonNaming);

        Assert.That(result, Does.Not.Contain("IEquatable"));
        Assert.That(result, Does.Not.Contain("Equals"));
        Assert.That(result, Does.Not.Contain("GetHashCode"));
    }

    [Test]
    public void Emit_RootTypeAppearsInEntityLists()
    {
        var personNode = CreateNode("TestApp.Person", "Person", SimpleProp("Name", "string", isRef: true));
        var allNodes = new List<TypeGraphNode> { personNode };

        var result = ContainerEmitter.Emit(personNode, allNodes, NoJsonNaming);

        Assert.That(result, Does.Contain("public TestApp.PersonDto[] PersonDtos { get; set; }"));
    }

    [Test]
    public void Emit_GeneratedCodeAttribute_Present()
    {
        var personNode = CreateNode("TestApp.Person", "Person", SimpleProp("Name", "string", isRef: true));
        var allNodes = new List<TypeGraphNode> { personNode };

        var result = ContainerEmitter.Emit(personNode, allNodes, NoJsonNaming);

        Assert.That(result, Does.Contain("[System.CodeDom.Compiler.GeneratedCode(\"DataNormalizer\", \"1.0.0\")]"));
    }

    [Test]
    public void Emit_NullableEnable_Present()
    {
        var personNode = CreateNode("TestApp.Person", "Person", SimpleProp("Name", "string", isRef: true));
        var allNodes = new List<TypeGraphNode> { personNode };

        var result = ContainerEmitter.Emit(personNode, allNodes, NoJsonNaming);

        Assert.That(result, Does.Contain("#nullable enable"));
    }

    [Test]
    public void Emit_NoNamespace_OmitsNamespaceDeclaration()
    {
        var personNode = CreateNode("Person", "Person", SimpleProp("Name", "string", isRef: true));
        var allNodes = new List<TypeGraphNode> { personNode };

        var result = ContainerEmitter.Emit(personNode, allNodes, NoJsonNaming);

        Assert.That(result, Does.Not.Contain("namespace"));
    }

    [Test]
    public void Emit_ZeroPropertyType_StillGetsEntityList()
    {
        var personNode = CreateNode("TestApp.Person", "Person", SimpleProp("Name", "string", isRef: true));
        var emptyNode = CreateNode("TestApp.Marker", "Marker");
        var allNodes = new List<TypeGraphNode> { personNode, emptyNode };

        var result = ContainerEmitter.Emit(personNode, allNodes, NoJsonNaming);

        Assert.That(
            result,
            Does.Contain(
                "public TestApp.MarkerDto[] MarkerDtos { get; set; } = System.Array.Empty<TestApp.MarkerDto>();"
            )
        );
    }

    [Test]
    public void Emit_EmitJsonPropertyNamesFalse_NoJsonPropertyNameAttributes()
    {
        var personNode = CreateNode("TestApp.Person", "Person", SimpleProp("Name", "string", isRef: true));
        var allNodes = new List<TypeGraphNode> { personNode };

        var result = ContainerEmitter.Emit(personNode, allNodes, NoJsonNaming);

        Assert.That(result, Does.Not.Contain("JsonPropertyName"));
    }

    [Test]
    public void Emit_DuplicateTypeNames_DisambiguatesWithNamespace()
    {
        var personNode = CreateNode("TestApp.Person", "Person", SimpleProp("Name", "string", isRef: true));
        var acmeAddress = CreateNode("Acme.Address", "Address", SimpleProp("Street", "string", isRef: true));
        var contosoAddress = CreateNode("Contoso.Address", "Address", SimpleProp("City", "string", isRef: true));
        var allNodes = new List<TypeGraphNode> { personNode, acmeAddress, contosoAddress };

        var result = ContainerEmitter.Emit(personNode, allNodes, NoJsonNaming);

        // The two Address types must be disambiguated with namespace prefix
        Assert.That(result, Does.Contain("AcmeAddressDtos"));
        Assert.That(result, Does.Contain("ContosoAddressDtos"));
        // The simple "AddressDtos" should NOT appear (both are disambiguated)
        Assert.That(result, Does.Not.Contain("public Acme.AddressDto[] AddressDtos"));
        Assert.That(result, Does.Not.Contain("public Contoso.AddressDto[] AddressDtos"));
        // Person has no collision, so stays simple
        Assert.That(result, Does.Contain("PersonDtos"));
    }

    [Test]
    public void Emit_DuplicateTypeNames_WithJsonNames_DisambiguatesJsonPropertyNames()
    {
        var personNode = CreateNode("TestApp.Person", "Person", SimpleProp("Name", "string", isRef: true));
        var acmeAddress = CreateNode("Acme.Address", "Address", SimpleProp("Street", "string", isRef: true));
        var contosoAddress = CreateNode("Contoso.Address", "Address", SimpleProp("City", "string", isRef: true));
        var allNodes = new List<TypeGraphNode> { personNode, acmeAddress, contosoAddress };

        var result = ContainerEmitter.Emit(personNode, allNodes, DefaultNaming);

        Assert.That(
            result,
            Does.Contain("[System.Text.Json.Serialization.JsonPropertyName(\"acmeAddressDtos\")]")
        );
        Assert.That(
            result,
            Does.Contain("[System.Text.Json.Serialization.JsonPropertyName(\"contosoAddressDtos\")]")
        );
    }

    [Test]
    public void Emit_UniqueTypeNames_KeepsSimplePropertyNames()
    {
        var personNode = CreateNode("TestApp.Person", "Person", SimpleProp("Name", "string", isRef: true));
        var addressNode = CreateNode("TestApp.Address", "Address", SimpleProp("Street", "string", isRef: true));
        var allNodes = new List<TypeGraphNode> { personNode, addressNode };

        var result = ContainerEmitter.Emit(personNode, allNodes, NoJsonNaming);

        // No collision, so simple names used
        Assert.That(result, Does.Contain("PersonDtos"));
        Assert.That(result, Does.Contain("AddressDtos"));
        // No namespace-prefixed names
        Assert.That(result, Does.Not.Contain("TestAppPersonDtos"));
        Assert.That(result, Does.Not.Contain("TestAppAddressDtos"));
    }

    [Test]
    public void Emit_DoesNotEmitRootProperties_OnContainer()
    {
        var personNode = CreateNode(
            "TestApp.Person",
            "Person",
            SimpleProp("Name", "string", isRef: true),
            SimpleProp("Age", "int", isRef: false)
        );
        var allNodes = new List<TypeGraphNode> { personNode };

        var result = ContainerEmitter.Emit(personNode, allNodes, NoJsonNaming);

        Assert.That(result, Does.Not.Contain("public string Name"));
        Assert.That(result, Does.Not.Contain("public int Age"));
    }

    [Test]
    public void Emit_CustomDtoPrefix_UsesInContainerAndDtoNames()
    {
        var naming = new NamingModel
        {
            DtoPrefix = "Norm",
            DtoSuffix = "",
            EmitJsonPropertyNames = false,
        };
        var personNode = CreateNode("TestApp.Person", "Person", SimpleProp("Name", "string", isRef: true));
        var allNodes = new List<TypeGraphNode> { personNode };

        var result = ContainerEmitter.Emit(personNode, allNodes, naming);

        Assert.That(result, Does.Contain("public TestApp.NormPerson[] PersonList { get; set; }"));
    }

    [Test]
    public void Emit_EmptyDtoSuffix_UsesListSuffix()
    {
        var naming = new NamingModel
        {
            DtoPrefix = "",
            DtoSuffix = "",
            EmitJsonPropertyNames = false,
        };
        var personNode = CreateNode("TestApp.Person", "Person", SimpleProp("Name", "string", isRef: true));
        var allNodes = new List<TypeGraphNode> { personNode };

        var result = ContainerEmitter.Emit(personNode, allNodes, naming);

        // When DtoSuffix is empty, GetListPropertyName falls back to "{TypeName}List"
        Assert.That(result, Does.Contain("PersonList"));
    }

    [Test]
    public void Emit_LegacyOverload_ProducesOldStyleNames()
    {
        var personNode = CreateNode("TestApp.Person", "Person", SimpleProp("Name", "string", isRef: true));
        var allNodes = new List<TypeGraphNode> { personNode };

        var result = ContainerEmitter.Emit(personNode, allNodes, jsonNamingPolicy: null);

        Assert.That(result, Does.Contain("NormalizedPersonResult"));
        Assert.That(result, Does.Contain("NormalizedPerson[]"));
        Assert.That(result, Does.Contain("PersonList"));
    }

    // ---- Helpers ----

    private static TypeGraphNode CreateNode(string fullName, string name, params AnalyzedProperty[] props)
    {
        return new TypeGraphNode
        {
            TypeFullName = fullName,
            TypeName = name,
            Properties = props.ToImmutableArray(),
        };
    }

    private static AnalyzedProperty SimpleProp(string name, string type, bool isRef)
    {
        return new AnalyzedProperty
        {
            Name = name,
            TypeFullName = type,
            Kind = PropertyKind.Simple,
            IsReferenceType = isRef,
        };
    }

    private static AnalyzedProperty NormalizedProp(string name, string type, bool nullable)
    {
        return new AnalyzedProperty
        {
            Name = name,
            TypeFullName = type,
            Kind = PropertyKind.Normalized,
            IsNullable = nullable,
            IsReferenceType = true,
        };
    }
}
