using System.Collections.Generic;
using System.Collections.Immutable;
using DataNormalizer.Generators.Emitters;
using DataNormalizer.Generators.Models;
using NUnit.Framework;

namespace DataNormalizer.Generators.Tests.Emitters;

[TestFixture]
public sealed class ContainerEmitterTests
{
    [Test]
    public void Emit_SimpleGraph_GeneratesContainerWithRootIndexAndEntityLists()
    {
        var personNode = CreateNode(
            "TestApp.Person",
            "Person",
            SimpleProp("Name", "string", isRef: true),
            NormalizedProp("HomeAddress", "TestApp.Address", nullable: false)
        );
        var addressNode = CreateNode("TestApp.Address", "Address", SimpleProp("Street", "string", isRef: true));
        var allNodes = new List<TypeGraphNode> { personNode, addressNode };

        var result = ContainerEmitter.Emit(personNode, allNodes, jsonNamingPolicy: null);

        Assert.That(result, Does.Contain("public partial class NormalizedPersonResult"));
        Assert.That(result, Does.Contain("public int RootIndex { get; set; }"));
        Assert.That(
            result,
            Does.Contain(
                "public TestApp.NormalizedPerson[] PersonList { get; set; } = System.Array.Empty<TestApp.NormalizedPerson>();"
            )
        );
        Assert.That(
            result,
            Does.Contain(
                "public TestApp.NormalizedAddress[] AddressList { get; set; } = System.Array.Empty<TestApp.NormalizedAddress>();"
            )
        );
    }

    [Test]
    public void Emit_CamelCaseJsonNaming_EmitsJsonPropertyNameAttributes()
    {
        var personNode = CreateNode("TestApp.Person", "Person", SimpleProp("Name", "string", isRef: true));
        var addressNode = CreateNode("TestApp.Address", "Address", SimpleProp("Street", "string", isRef: true));
        var allNodes = new List<TypeGraphNode> { personNode, addressNode };

        var result = ContainerEmitter.Emit(personNode, allNodes, jsonNamingPolicy: "CamelCase");

        Assert.That(result, Does.Contain("[System.Text.Json.Serialization.JsonPropertyName(\"rootIndex\")]"));
        Assert.That(result, Does.Contain("[System.Text.Json.Serialization.JsonPropertyName(\"personList\")]"));
        Assert.That(result, Does.Contain("[System.Text.Json.Serialization.JsonPropertyName(\"addressList\")]"));
    }

    [Test]
    public void Emit_Namespace_ContainerEmittedInRootTypeNamespace()
    {
        var personNode = CreateNode("TestApp.Models.Person", "Person", SimpleProp("Name", "string", isRef: true));
        var allNodes = new List<TypeGraphNode> { personNode };

        var result = ContainerEmitter.Emit(personNode, allNodes, jsonNamingPolicy: null);

        Assert.That(result, Does.Contain("namespace TestApp.Models;"));
    }

    [Test]
    public void Emit_NoIEquatable_ContainerDoesNotImplementIEquatable()
    {
        var personNode = CreateNode("TestApp.Person", "Person", SimpleProp("Name", "string", isRef: true));
        var allNodes = new List<TypeGraphNode> { personNode };

        var result = ContainerEmitter.Emit(personNode, allNodes, jsonNamingPolicy: null);

        Assert.That(result, Does.Not.Contain("IEquatable"));
        Assert.That(result, Does.Not.Contain("Equals"));
        Assert.That(result, Does.Not.Contain("GetHashCode"));
    }

    [Test]
    public void Emit_RootTypeAppearsInEntityLists()
    {
        var personNode = CreateNode("TestApp.Person", "Person", SimpleProp("Name", "string", isRef: true));
        var allNodes = new List<TypeGraphNode> { personNode };

        var result = ContainerEmitter.Emit(personNode, allNodes, jsonNamingPolicy: null);

        Assert.That(result, Does.Contain("public TestApp.NormalizedPerson[] PersonList { get; set; }"));
    }

    [Test]
    public void Emit_GeneratedCodeAttribute_Present()
    {
        var personNode = CreateNode("TestApp.Person", "Person", SimpleProp("Name", "string", isRef: true));
        var allNodes = new List<TypeGraphNode> { personNode };

        var result = ContainerEmitter.Emit(personNode, allNodes, jsonNamingPolicy: null);

        Assert.That(result, Does.Contain("[System.CodeDom.Compiler.GeneratedCode(\"DataNormalizer\", \"1.0.0\")]"));
    }

    [Test]
    public void Emit_NullableEnable_Present()
    {
        var personNode = CreateNode("TestApp.Person", "Person", SimpleProp("Name", "string", isRef: true));
        var allNodes = new List<TypeGraphNode> { personNode };

        var result = ContainerEmitter.Emit(personNode, allNodes, jsonNamingPolicy: null);

        Assert.That(result, Does.Contain("#nullable enable"));
    }

    [Test]
    public void Emit_NoNamespace_OmitsNamespaceDeclaration()
    {
        var personNode = CreateNode("Person", "Person", SimpleProp("Name", "string", isRef: true));
        var allNodes = new List<TypeGraphNode> { personNode };

        var result = ContainerEmitter.Emit(personNode, allNodes, jsonNamingPolicy: null);

        Assert.That(result, Does.Not.Contain("namespace"));
    }

    [Test]
    public void Emit_ZeroPropertyType_StillGetsEntityList()
    {
        var personNode = CreateNode("TestApp.Person", "Person", SimpleProp("Name", "string", isRef: true));
        var emptyNode = CreateNode("TestApp.Marker", "Marker");
        var allNodes = new List<TypeGraphNode> { personNode, emptyNode };

        var result = ContainerEmitter.Emit(personNode, allNodes, jsonNamingPolicy: null);

        Assert.That(
            result,
            Does.Contain(
                "public TestApp.NormalizedMarker[] MarkerList { get; set; } = System.Array.Empty<TestApp.NormalizedMarker>();"
            )
        );
    }

    [Test]
    public void Emit_NullJsonNamingPolicy_NoJsonPropertyNameAttributes()
    {
        var personNode = CreateNode("TestApp.Person", "Person", SimpleProp("Name", "string", isRef: true));
        var allNodes = new List<TypeGraphNode> { personNode };

        var result = ContainerEmitter.Emit(personNode, allNodes, jsonNamingPolicy: null);

        Assert.That(result, Does.Not.Contain("JsonPropertyName"));
    }

    [Test]
    public void Emit_DuplicateTypeNames_DisambiguatesWithNamespace()
    {
        var personNode = CreateNode("TestApp.Person", "Person", SimpleProp("Name", "string", isRef: true));
        var acmeAddress = CreateNode("Acme.Address", "Address", SimpleProp("Street", "string", isRef: true));
        var contosoAddress = CreateNode("Contoso.Address", "Address", SimpleProp("City", "string", isRef: true));
        var allNodes = new List<TypeGraphNode> { personNode, acmeAddress, contosoAddress };

        var result = ContainerEmitter.Emit(personNode, allNodes, jsonNamingPolicy: null);

        // The two Address types must be disambiguated with namespace prefix
        Assert.That(result, Does.Contain("AcmeAddressList"));
        Assert.That(result, Does.Contain("ContosoAddressList"));
        // The simple "AddressList" should NOT appear (both are disambiguated)
        Assert.That(result, Does.Not.Contain("public Acme.NormalizedAddress[] AddressList"));
        Assert.That(result, Does.Not.Contain("public Contoso.NormalizedAddress[] AddressList"));
        // Person has no collision, so stays simple
        Assert.That(result, Does.Contain("PersonList"));
    }

    [Test]
    public void Emit_DuplicateTypeNames_CamelCase_DisambiguatesJsonPropertyNames()
    {
        var personNode = CreateNode("TestApp.Person", "Person", SimpleProp("Name", "string", isRef: true));
        var acmeAddress = CreateNode("Acme.Address", "Address", SimpleProp("Street", "string", isRef: true));
        var contosoAddress = CreateNode("Contoso.Address", "Address", SimpleProp("City", "string", isRef: true));
        var allNodes = new List<TypeGraphNode> { personNode, acmeAddress, contosoAddress };

        var result = ContainerEmitter.Emit(personNode, allNodes, jsonNamingPolicy: "CamelCase");

        Assert.That(result, Does.Contain("[System.Text.Json.Serialization.JsonPropertyName(\"acmeAddressList\")]"));
        Assert.That(result, Does.Contain("[System.Text.Json.Serialization.JsonPropertyName(\"contosoAddressList\")]"));
    }

    [Test]
    public void Emit_UniqueTypeNames_KeepsSimplePropertyNames()
    {
        var personNode = CreateNode("TestApp.Person", "Person", SimpleProp("Name", "string", isRef: true));
        var addressNode = CreateNode("TestApp.Address", "Address", SimpleProp("Street", "string", isRef: true));
        var allNodes = new List<TypeGraphNode> { personNode, addressNode };

        var result = ContainerEmitter.Emit(personNode, allNodes, jsonNamingPolicy: null);

        // No collision, so simple names used
        Assert.That(result, Does.Contain("PersonList"));
        Assert.That(result, Does.Contain("AddressList"));
        // No namespace-prefixed names
        Assert.That(result, Does.Not.Contain("TestAppPersonList"));
        Assert.That(result, Does.Not.Contain("TestAppAddressList"));
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

        var result = ContainerEmitter.Emit(personNode, allNodes, jsonNamingPolicy: null);

        Assert.That(result, Does.Not.Contain("public string Name"));
        Assert.That(result, Does.Not.Contain("public int Age"));
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
