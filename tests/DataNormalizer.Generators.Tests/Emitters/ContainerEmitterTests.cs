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

    // ---- Existing Tests (updated with JsonContractModel.Default as 4th arg) ----

    [Test]
    public void Emit_SimpleGraph_GeneratesContainerWithEntityLists()
    {
        var personNode = CreateNode(
            "TestApp.Person",
            "Person",
            needsList: true,
            isRootType: true,
            SimpleProp("Name", "string", isRef: true),
            NormalizedProp("HomeAddress", "TestApp.Address", nullable: false)
        );
        var addressNode = CreateNode("TestApp.Address", "Address", SimpleProp("Street", "string", isRef: true));
        var allNodes = new List<TypeGraphNode> { personNode, addressNode };

        var result = ContainerEmitter.Emit(personNode, allNodes, DefaultNaming, JsonContractModel.Default);

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

        // Container references DTO types — provide stubs
        var stubs = new[]
        {
            "namespace TestApp { public class PersonDto { } }",
            "namespace TestApp { public class AddressDto { } }",
        };
        EmitterCompilationHelper.AssertCompilesWithStubs(new[] { result }, stubs);
    }

    [Test]
    public void Emit_DefaultNaming_EmitsJsonPropertyNameAttributes()
    {
        var personNode = CreateNode(
            "TestApp.Person",
            "Person",
            needsList: true,
            isRootType: true,
            SimpleProp("Name", "string", isRef: true)
        );
        var addressNode = CreateNode("TestApp.Address", "Address", SimpleProp("Street", "string", isRef: true));
        var allNodes = new List<TypeGraphNode> { personNode, addressNode };

        var result = ContainerEmitter.Emit(personNode, allNodes, DefaultNaming, JsonContractModel.Default);

        Assert.That(result, Does.Not.Contain("rootIndex"));
        Assert.That(result, Does.Contain("[System.Text.Json.Serialization.JsonPropertyName(\"personDtos\")]"));
        Assert.That(result, Does.Contain("[System.Text.Json.Serialization.JsonPropertyName(\"addressDtos\")]"));
    }

    [Test]
    public void Emit_Namespace_ContainerEmittedInRootTypeNamespace()
    {
        var personNode = CreateNode("TestApp.Models.Person", "Person", SimpleProp("Name", "string", isRef: true));
        var allNodes = new List<TypeGraphNode> { personNode };

        var result = ContainerEmitter.Emit(personNode, allNodes, NoJsonNaming, JsonContractModel.Default);

        Assert.That(result, Does.Contain("namespace TestApp.Models;"));
    }

    [Test]
    public void Emit_NoIEquatable_ContainerDoesNotImplementIEquatable()
    {
        var personNode = CreateNode("TestApp.Person", "Person", SimpleProp("Name", "string", isRef: true));
        var allNodes = new List<TypeGraphNode> { personNode };

        var result = ContainerEmitter.Emit(personNode, allNodes, NoJsonNaming, JsonContractModel.Default);

        Assert.That(result, Does.Not.Contain("IEquatable"));
        Assert.That(result, Does.Not.Contain("Equals"));
        Assert.That(result, Does.Not.Contain("GetHashCode"));
    }

    [Test]
    public void Emit_RootTypeAppearsInEntityLists()
    {
        var personNode = CreateNode(
            "TestApp.Person",
            "Person",
            needsList: true,
            isRootType: true,
            SimpleProp("Name", "string", isRef: true)
        );
        var allNodes = new List<TypeGraphNode> { personNode };

        var result = ContainerEmitter.Emit(personNode, allNodes, NoJsonNaming, JsonContractModel.Default);

        Assert.That(result, Does.Contain("public TestApp.PersonDto[] PersonDtos { get; set; }"));
    }

    [Test]
    public void Emit_GeneratedCodeAttribute_Present()
    {
        var personNode = CreateNode("TestApp.Person", "Person", SimpleProp("Name", "string", isRef: true));
        var allNodes = new List<TypeGraphNode> { personNode };

        var result = ContainerEmitter.Emit(personNode, allNodes, NoJsonNaming, JsonContractModel.Default);

        Assert.That(result, Does.Contain("[System.CodeDom.Compiler.GeneratedCode(\"DataNormalizer\", \"1.0.0\")]"));
    }

    [Test]
    public void Emit_NullableEnable_Present()
    {
        var personNode = CreateNode("TestApp.Person", "Person", SimpleProp("Name", "string", isRef: true));
        var allNodes = new List<TypeGraphNode> { personNode };

        var result = ContainerEmitter.Emit(personNode, allNodes, NoJsonNaming, JsonContractModel.Default);

        Assert.That(result, Does.Contain("#nullable enable"));
    }

    [Test]
    public void Emit_NoNamespace_OmitsNamespaceDeclaration()
    {
        var personNode = CreateNode("Person", "Person", SimpleProp("Name", "string", isRef: true));
        var allNodes = new List<TypeGraphNode> { personNode };

        var result = ContainerEmitter.Emit(personNode, allNodes, NoJsonNaming, JsonContractModel.Default);

        Assert.That(result, Does.Not.Contain("namespace"));
    }

    [Test]
    public void Emit_ZeroPropertyType_StillGetsEntityList()
    {
        var personNode = CreateNode("TestApp.Person", "Person", SimpleProp("Name", "string", isRef: true));
        var emptyNode = CreateNode("TestApp.Marker", "Marker");
        var allNodes = new List<TypeGraphNode> { personNode, emptyNode };

        var result = ContainerEmitter.Emit(personNode, allNodes, NoJsonNaming, JsonContractModel.Default);

        Assert.That(
            result,
            Does.Contain(
                "public TestApp.MarkerDto[] MarkerDtos { get; set; } = System.Array.Empty<TestApp.MarkerDto>();"
            )
        );
    }

    [Test]
    public void Emit_EmitJsonPropertyNamesFalse_NoJsonPropertyNameAttributesOnLists()
    {
        var personNode = CreateNode(
            "TestApp.Person",
            "Person",
            needsList: true,
            isRootType: true,
            SimpleProp("Name", "string", isRef: true)
        );
        var allNodes = new List<TypeGraphNode> { personNode };

        var result = ContainerEmitter.Emit(personNode, allNodes, NoJsonNaming, JsonContractModel.Default);

        // Result property always gets JsonPropertyName even when EmitJsonPropertyNames=false
        Assert.That(result, Does.Contain("[System.Text.Json.Serialization.JsonPropertyName(\"result\")]"));
    }

    [Test]
    public void Emit_DuplicateTypeNames_DisambiguatesWithNamespace()
    {
        var personNode = CreateNode("TestApp.Person", "Person", SimpleProp("Name", "string", isRef: true));
        var acmeAddress = CreateNode("Acme.Address", "Address", SimpleProp("Street", "string", isRef: true));
        var contosoAddress = CreateNode("Contoso.Address", "Address", SimpleProp("City", "string", isRef: true));
        var allNodes = new List<TypeGraphNode> { personNode, acmeAddress, contosoAddress };

        var result = ContainerEmitter.Emit(personNode, allNodes, NoJsonNaming, JsonContractModel.Default);

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

        var result = ContainerEmitter.Emit(personNode, allNodes, DefaultNaming, JsonContractModel.Default);

        Assert.That(result, Does.Contain("[System.Text.Json.Serialization.JsonPropertyName(\"acmeAddressDtos\")]"));
        Assert.That(result, Does.Contain("[System.Text.Json.Serialization.JsonPropertyName(\"contosoAddressDtos\")]"));
    }

    [Test]
    public void Emit_UniqueTypeNames_KeepsSimplePropertyNames()
    {
        var personNode = CreateNode("TestApp.Person", "Person", SimpleProp("Name", "string", isRef: true));
        var addressNode = CreateNode("TestApp.Address", "Address", SimpleProp("Street", "string", isRef: true));
        var allNodes = new List<TypeGraphNode> { personNode, addressNode };

        var result = ContainerEmitter.Emit(personNode, allNodes, NoJsonNaming, JsonContractModel.Default);

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

        var result = ContainerEmitter.Emit(personNode, allNodes, NoJsonNaming, JsonContractModel.Default);

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

        var result = ContainerEmitter.Emit(personNode, allNodes, naming, JsonContractModel.Default);

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

        var result = ContainerEmitter.Emit(personNode, allNodes, naming, JsonContractModel.Default);

        // When DtoSuffix is empty, GetListPropertyName falls back to "{TypeName}List"
        Assert.That(result, Does.Contain("PersonList"));
    }

    // ---- New Tests: Root Property ----

    [Test]
    public void Emit_DefaultContainer_HasResultPropertyWithJsonPropertyName()
    {
        var rootNode = CreateNode(
            "TestApp.Person",
            "Person",
            needsList: true,
            isRootType: true,
            SimpleProp("Name", "string", isRef: true)
        );
        var allNodes = new List<TypeGraphNode> { rootNode };

        var result = ContainerEmitter.Emit(rootNode, allNodes, DefaultNaming, JsonContractModel.Default);

        Assert.That(result, Does.Contain("[System.Text.Json.Serialization.JsonPropertyName(\"result\")]"));
        Assert.That(result, Does.Contain("public TestApp.PersonDto Result { get; set; } = default!;"));

        var stubs = new[] { "namespace TestApp { public class PersonDto { } }" };
        EmitterCompilationHelper.AssertCompilesWithStubs(new[] { result }, stubs);
    }

    [Test]
    public void Emit_RootTypeWithResultProperty_TypeIsRootDtoFullName()
    {
        var rootNode = CreateNode(
            "TestApp.Models.Person",
            "Person",
            needsList: false,
            isRootType: true,
            SimpleProp("Name", "string", isRef: true)
        );
        var allNodes = new List<TypeGraphNode> { rootNode };

        var result = ContainerEmitter.Emit(rootNode, allNodes, NoJsonNaming, JsonContractModel.Default);

        Assert.That(result, Does.Contain("public TestApp.Models.PersonDto Result { get; set; } = default!;"));
    }

    // ---- New Tests: NeedsList ----

    [Test]
    public void Emit_RootNeedsListFalse_NoListPropertyForRoot()
    {
        var rootNode = CreateNode(
            "TestApp.Person",
            "Person",
            needsList: false,
            isRootType: true,
            SimpleProp("Name", "string", isRef: true)
        );
        var addressNode = CreateNode("TestApp.Address", "Address", SimpleProp("Street", "string", isRef: true));
        var allNodes = new List<TypeGraphNode> { rootNode, addressNode };

        var result = ContainerEmitter.Emit(rootNode, allNodes, NoJsonNaming, JsonContractModel.Default);

        // Root should have Result property but NOT a list
        Assert.That(result, Does.Contain("Result { get; set; } = default!;"));
        Assert.That(result, Does.Not.Contain("PersonDtos"));
        Assert.That(result, Does.Not.Contain("PersonList"));
        // Non-root still gets a list
        Assert.That(result, Does.Contain("AddressDtos"));
    }

    [Test]
    public void Emit_RootNeedsListTrue_BothResultPropertyAndListEmitted()
    {
        var rootNode = CreateNode(
            "TestApp.Person",
            "Person",
            needsList: true,
            isRootType: true,
            SimpleProp("Name", "string", isRef: true)
        );
        var allNodes = new List<TypeGraphNode> { rootNode };

        var result = ContainerEmitter.Emit(rootNode, allNodes, NoJsonNaming, JsonContractModel.Default);

        Assert.That(result, Does.Contain("Result { get; set; } = default!;"));
        Assert.That(result, Does.Contain("PersonDtos"));
    }

    // ---- New Tests: RootPropertyName override ----

    [Test]
    public void Emit_CustomRootPropertyName_UsesCustomJsonPropertyName()
    {
        var rootNode = CreateNode(
            "TestApp.Person",
            "Person",
            needsList: false,
            isRootType: true,
            SimpleProp("Name", "string", isRef: true)
        );
        var allNodes = new List<TypeGraphNode> { rootNode };
        var jsonContract = new JsonContractModel { RootPropertyName = "searchResult" };

        var result = ContainerEmitter.Emit(rootNode, allNodes, DefaultNaming, jsonContract);

        Assert.That(result, Does.Contain("[System.Text.Json.Serialization.JsonPropertyName(\"searchResult\")]"));
        Assert.That(result, Does.Contain("Result { get; set; } = default!;"));

        var stubs = new[] { "namespace TestApp { public class PersonDto { } }" };
        EmitterCompilationHelper.AssertCompilesWithStubs(new[] { result }, stubs);
    }

    [Test]
    public void Emit_EmptyRootPropertyName_FallsBackToResult()
    {
        var rootNode = CreateNode(
            "TestApp.Person",
            "Person",
            needsList: false,
            isRootType: true,
            SimpleProp("Name", "string", isRef: true)
        );
        var allNodes = new List<TypeGraphNode> { rootNode };
        var jsonContract = new JsonContractModel { RootPropertyName = "" };

        var result = ContainerEmitter.Emit(rootNode, allNodes, DefaultNaming, jsonContract);

        Assert.That(result, Does.Contain("[System.Text.Json.Serialization.JsonPropertyName(\"result\")]"));
    }

    // ---- New Tests: CollectionJsonNames overrides ----

    [Test]
    public void Emit_CollectionJsonNamesOverride_UsesCustomJsonName()
    {
        var rootNode = CreateNode(
            "TestApp.Person",
            "Person",
            needsList: true,
            isRootType: true,
            SimpleProp("Name", "string", isRef: true)
        );
        var routeNode = CreateNode("TestApp.Route", "Route", SimpleProp("Path", "string", isRef: true));
        var allNodes = new List<TypeGraphNode> { rootNode, routeNode };
        var jsonContract = new JsonContractModel
        {
            CollectionJsonNames = ImmutableDictionary<string, string>.Empty.Add("TestApp.Route", "routes"),
        };

        var result = ContainerEmitter.Emit(rootNode, allNodes, DefaultNaming, jsonContract);

        Assert.That(result, Does.Contain("[System.Text.Json.Serialization.JsonPropertyName(\"routes\")]"));

        var stubs = new[]
        {
            "namespace TestApp { public class PersonDto { } }",
            "namespace TestApp { public class RouteDto { } }",
        };
        EmitterCompilationHelper.AssertCompilesWithStubs(new[] { result }, stubs);
    }

    [Test]
    public void Emit_CollectionJsonNamesKeyNoMatch_NoCrash()
    {
        var rootNode = CreateNode(
            "TestApp.Person",
            "Person",
            needsList: false,
            isRootType: true,
            SimpleProp("Name", "string", isRef: true)
        );
        var allNodes = new List<TypeGraphNode> { rootNode };
        var jsonContract = new JsonContractModel
        {
            CollectionJsonNames = ImmutableDictionary<string, string>.Empty.Add("TestApp.NonExistent", "whatever"),
        };

        // Should not crash
        var result = ContainerEmitter.Emit(rootNode, allNodes, DefaultNaming, jsonContract);

        Assert.That(result, Does.Contain("public partial class PersonResultDto"));
    }

    [Test]
    public void Emit_TypeWithoutCollectionOverride_DefaultCamelCaseJsonName()
    {
        var rootNode = CreateNode(
            "TestApp.Person",
            "Person",
            needsList: true,
            isRootType: true,
            SimpleProp("Name", "string", isRef: true)
        );
        var addressNode = CreateNode("TestApp.Address", "Address", SimpleProp("Street", "string", isRef: true));
        var allNodes = new List<TypeGraphNode> { rootNode, addressNode };

        var result = ContainerEmitter.Emit(rootNode, allNodes, DefaultNaming, JsonContractModel.Default);

        // No override for Address, so default camelCase
        Assert.That(result, Does.Contain("[System.Text.Json.Serialization.JsonPropertyName(\"addressDtos\")]"));
    }

    [Test]
    public void Emit_ResultJsonPropertyName_AlwaysEmittedEvenWhenEmitJsonPropertyNamesFalse()
    {
        var rootNode = CreateNode(
            "TestApp.Person",
            "Person",
            needsList: false,
            isRootType: true,
            SimpleProp("Name", "string", isRef: true)
        );
        var allNodes = new List<TypeGraphNode> { rootNode };

        var result = ContainerEmitter.Emit(rootNode, allNodes, NoJsonNaming, JsonContractModel.Default);

        Assert.That(result, Does.Contain("[System.Text.Json.Serialization.JsonPropertyName(\"result\")]"));
        Assert.That(result, Does.Contain("public TestApp.PersonDto Result { get; set; } = default!;"));
    }

    [Test]
    public void Emit_MultipleNonRootTypesWithCollectionJsonNames_AllApplied()
    {
        var rootNode = CreateNode(
            "TestApp.Person",
            "Person",
            needsList: false,
            isRootType: true,
            SimpleProp("Name", "string", isRef: true)
        );
        var routeNode = CreateNode("TestApp.Route", "Route", SimpleProp("Path", "string", isRef: true));
        var stopNode = CreateNode("TestApp.Stop", "Stop", SimpleProp("Name", "string", isRef: true));
        var allNodes = new List<TypeGraphNode> { rootNode, routeNode, stopNode };
        var jsonContract = new JsonContractModel
        {
            CollectionJsonNames = ImmutableDictionary<string, string>
                .Empty.Add("TestApp.Route", "routes")
                .Add("TestApp.Stop", "stops"),
        };

        var result = ContainerEmitter.Emit(rootNode, allNodes, DefaultNaming, jsonContract);

        Assert.That(result, Does.Contain("[System.Text.Json.Serialization.JsonPropertyName(\"routes\")]"));
        Assert.That(result, Does.Contain("[System.Text.Json.Serialization.JsonPropertyName(\"stops\")]"));
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

    private static TypeGraphNode CreateNode(
        string fullName,
        string name,
        bool needsList,
        bool isRootType,
        params AnalyzedProperty[] props
    )
    {
        return new TypeGraphNode
        {
            TypeFullName = fullName,
            TypeName = name,
            Properties = props.ToImmutableArray(),
            IsRootType = isRootType,
            NeedsList = needsList,
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
