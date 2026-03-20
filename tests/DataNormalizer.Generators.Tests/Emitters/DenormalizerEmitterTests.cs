using System.Collections.Generic;
using System.Collections.Immutable;
using DataNormalizer.Generators.Emitters;
using DataNormalizer.Generators.Models;
using NUnit.Framework;

namespace DataNormalizer.Generators.Tests.Emitters;

[TestFixture]
public sealed class DenormalizerEmitterTests
{
    [Test]
    public void Emit_SimpleFlatType_GeneratesDenormalizeMethod()
    {
        var (model, nodes) = CreateSimplePersonScenario();
        var result = DenormalizerEmitter.Emit(model, nodes);

        Assert.That(result, Does.Contain("public static TestApp.Person Denormalize("));
        Assert.That(result, Does.Contain("DataNormalizer.Runtime.NormalizedResult<TestApp.NormalizedPerson>"));
    }

    [Test]
    public void Emit_SimpleFlatType_Pass1_CreatesObjectsAndSetsSimpleProps()
    {
        var (model, nodes) = CreateSimplePersonScenario();
        var result = DenormalizerEmitter.Emit(model, nodes);

        Assert.That(result, Does.Contain("var persons = new TestApp.Person[personDtos.Count]"));
        Assert.That(result, Does.Contain("persons[i].Name = personDtos[i].Name;"));
        Assert.That(result, Does.Contain("persons[i].Age = personDtos[i].Age;"));
    }

    [Test]
    public void Emit_NestedNormalizableType_Pass2_ResolvesIndices()
    {
        var (model, nodes) = CreatePersonWithAddressScenario();
        var result = DenormalizerEmitter.Emit(model, nodes);

        // Pass 2 resolves HomeAddress index
        Assert.That(result, Does.Contain("persons[i].HomeAddress = addresses[personDtos[i].HomeAddressIndex]"));
    }

    [Test]
    public void Emit_NullableNested_Pass2_ChecksNullIndex()
    {
        var (model, nodes) = CreatePersonWithNullableAddressScenario();
        var result = DenormalizerEmitter.Emit(model, nodes);

        // Nullable: check if index has value
        Assert.That(result, Does.Contain("WorkAddressIndex").And.Contain("null"));
    }

    [Test]
    public void Emit_ListCollection_Pass2_ReconstructsList()
    {
        var (model, nodes) = CreatePersonWithPhoneListScenario();
        var result = DenormalizerEmitter.Emit(model, nodes);

        Assert.That(result, Does.Contain("new System.Collections.Generic.List<TestApp.PhoneNumber>()"));
        Assert.That(result, Does.Contain("PhoneNumbersIndices"));
    }

    [Test]
    public void Emit_ArrayCollection_Pass2_ReconstructsArray()
    {
        var (model, nodes) = CreatePersonWithPhoneArrayScenario();
        var result = DenormalizerEmitter.Emit(model, nodes);

        Assert.That(result, Does.Contain("new TestApp.PhoneNumber["));
    }

    [Test]
    public void Emit_IReadOnlyListCollection_Pass2_ReconstructsViaTempList()
    {
        var (model, nodes) = CreatePersonWithPhoneIReadOnlyListScenario();
        var result = DenormalizerEmitter.Emit(model, nodes);

        // Should create a List<T> and assign to the IReadOnlyList<T> property
        Assert.That(result, Does.Contain("new System.Collections.Generic.List<TestApp.PhoneNumber>()"));
    }

    [Test]
    public void Emit_CircularReference_TwoPassStructure()
    {
        var (model, nodes) = CreateTreeNodeScenario();
        var result = DenormalizerEmitter.Emit(model, nodes);

        // Must have distinct pass 1 and pass 2 sections
        var pass1Index = result.IndexOf("treeNodes[i] = new TestApp.TreeNode()");
        var pass2Index = result.IndexOf("treeNodes[i].Parent");
        Assert.That(pass1Index, Is.GreaterThan(-1), "Pass 1 should create objects");
        Assert.That(pass2Index, Is.GreaterThan(pass1Index), "Pass 2 should come after Pass 1");
    }

    [Test]
    public void Emit_ConfigClassInfo_UsesNamespaceAndClassName()
    {
        var (model, nodes) = CreateSimplePersonScenario();
        var result = DenormalizerEmitter.Emit(model, nodes);

        Assert.That(result, Does.Contain("namespace TestApp;"));
        Assert.That(result, Does.Contain("public partial class TestConfig"));
    }

    [Test]
    public void Emit_RootResolution_UsesRootIndex()
    {
        var (model, nodes) = CreateSimplePersonScenario();
        var result = DenormalizerEmitter.Emit(model, nodes);

        // Should use result.RootIndex for direct index access (no reference equality)
        Assert.That(result, Does.Contain("result.RootIndex"));
        Assert.That(result, Does.Not.Contain("ReferenceEquals"));
    }

    [Test]
    public void Emit_NestedNormalizableType_GetsDtoCollections()
    {
        var (model, nodes) = CreatePersonWithAddressScenario();
        var result = DenormalizerEmitter.Emit(model, nodes);

        Assert.That(result, Does.Contain("result.GetCollection<TestApp.NormalizedPerson>(\"Person\")"));
        Assert.That(result, Does.Contain("result.GetCollection<TestApp.NormalizedAddress>(\"Address\")"));
    }

    [Test]
    public void Emit_InlinedProperty_CopiedInPass1()
    {
        var (model, nodes) = CreatePersonWithInlinedMetadataScenario();
        var result = DenormalizerEmitter.Emit(model, nodes);

        // Inlined properties are simple copies in pass 1
        Assert.That(result, Does.Contain("persons[i].Metadata = personDtos[i].Metadata;"));
    }

    [Test]
    public void Emit_MultipleRootTypes_GeneratesOverloadsForEach()
    {
        var personNode = CreateNode("TestApp.Person", "Person", SimpleProp("Name", "string", isRef: true));
        var treeNode = CreateNode("TestApp.TreeNode", "TreeNode", SimpleProp("Label", "string", isRef: true));

        var model = new NormalizationModel
        {
            ConfigClassName = "TestConfig",
            ConfigNamespace = "TestApp",
            RootTypes = ImmutableArray.Create(
                new RootTypeInfo { TypeSymbol = null!, FullyQualifiedName = "TestApp.Person" },
                new RootTypeInfo { TypeSymbol = null!, FullyQualifiedName = "TestApp.TreeNode" }
            ),
            AutoDiscover = true,
        };

        var result = DenormalizerEmitter.Emit(model, new[] { personNode, treeNode });

        Assert.That(
            result,
            Does.Contain("Denormalize(DataNormalizer.Runtime.NormalizedResult<TestApp.NormalizedPerson>")
        );
        Assert.That(
            result,
            Does.Contain("Denormalize(DataNormalizer.Runtime.NormalizedResult<TestApp.NormalizedTreeNode>")
        );
    }

    [Test]
    public void Emit_SingleRootType_GeneratesSingleDenormalizeMethod()
    {
        var (model, nodes) = CreateSimplePersonScenario();
        var result = DenormalizerEmitter.Emit(model, nodes);

        // Should have exactly one Denormalize method
        var count = CountOccurrences(result, "public static TestApp.Person Denormalize(");
        Assert.That(count, Is.EqualTo(1));
    }

    [Test]
    public void Emit_WithCustomName_UsesCustomCollectionKey()
    {
        var personNode = CreateNode("TestApp.Person", "Person", SimpleProp("Name", "string", isRef: true));

        var model = new NormalizationModel
        {
            ConfigClassName = "TestConfig",
            ConfigNamespace = "TestApp",
            RootTypes = ImmutableArray.Create(
                new RootTypeInfo { TypeSymbol = null!, FullyQualifiedName = "TestApp.Person" }
            ),
            TypeConfigurations = ImmutableDictionary.CreateRange(
                new[]
                {
                    KeyValuePair.Create(
                        "TestApp.Person",
                        new TypeConfiguration { FullyQualifiedName = "TestApp.Person", CustomName = "People" }
                    ),
                }
            ),
        };

        var result = DenormalizerEmitter.Emit(model, new[] { personNode });

        Assert.That(result, Does.Contain("GetCollection<TestApp.NormalizedPerson>(\"People\")"));
    }

    // ---- Scenario Builders ----

    private static (NormalizationModel Model, IReadOnlyList<TypeGraphNode> Nodes) CreateSimplePersonScenario()
    {
        var personNode = CreateNode(
            "TestApp.Person",
            "Person",
            SimpleProp("Name", "string", isRef: true),
            SimpleProp("Age", "int", isRef: false)
        );
        var model = CreateModel("TestConfig", "TestApp", "TestApp.Person");
        return (model, new[] { personNode });
    }

    private static (NormalizationModel Model, IReadOnlyList<TypeGraphNode> Nodes) CreatePersonWithAddressScenario()
    {
        var addressNode = CreateNode(
            "TestApp.Address",
            "Address",
            SimpleProp("Street", "string", isRef: true),
            SimpleProp("City", "string", isRef: true)
        );
        var personNode = CreateNode(
            "TestApp.Person",
            "Person",
            SimpleProp("Name", "string", isRef: true),
            NormalizedProp("HomeAddress", "TestApp.Address", nullable: false)
        );
        var model = CreateModel("TestConfig", "TestApp", "TestApp.Person");
        return (model, new[] { personNode, addressNode });
    }

    private static (
        NormalizationModel Model,
        IReadOnlyList<TypeGraphNode> Nodes
    ) CreatePersonWithNullableAddressScenario()
    {
        var addressNode = CreateNode("TestApp.Address", "Address", SimpleProp("Street", "string", isRef: true));
        var personNode = CreateNode(
            "TestApp.Person",
            "Person",
            NormalizedProp("WorkAddress", "TestApp.Address", nullable: true)
        );
        var model = CreateModel("TestConfig", "TestApp", "TestApp.Person");
        return (model, new[] { personNode, addressNode });
    }

    private static (NormalizationModel Model, IReadOnlyList<TypeGraphNode> Nodes) CreatePersonWithPhoneListScenario()
    {
        var phoneNode = CreateNode("TestApp.PhoneNumber", "PhoneNumber", SimpleProp("Number", "string", isRef: true));
        var personNode = CreateNode(
            "TestApp.Person",
            "Person",
            CollectionProp(
                "PhoneNumbers",
                "System.Collections.Generic.List<TestApp.PhoneNumber>",
                "TestApp.PhoneNumber",
                CollectionTypeKind.List
            )
        );
        var model = CreateModel("TestConfig", "TestApp", "TestApp.Person");
        return (model, new[] { personNode, phoneNode });
    }

    private static (NormalizationModel Model, IReadOnlyList<TypeGraphNode> Nodes) CreatePersonWithPhoneArrayScenario()
    {
        var phoneNode = CreateNode("TestApp.PhoneNumber", "PhoneNumber", SimpleProp("Number", "string", isRef: true));
        var personNode = CreateNode(
            "TestApp.Person",
            "Person",
            CollectionProp("PhoneNumbers", "TestApp.PhoneNumber[]", "TestApp.PhoneNumber", CollectionTypeKind.Array)
        );
        var model = CreateModel("TestConfig", "TestApp", "TestApp.Person");
        return (model, new[] { personNode, phoneNode });
    }

    private static (
        NormalizationModel Model,
        IReadOnlyList<TypeGraphNode> Nodes
    ) CreatePersonWithPhoneIReadOnlyListScenario()
    {
        var phoneNode = CreateNode("TestApp.PhoneNumber", "PhoneNumber", SimpleProp("Number", "string", isRef: true));
        var personNode = CreateNode(
            "TestApp.Person",
            "Person",
            CollectionProp(
                "PhoneNumbers",
                "System.Collections.Generic.IReadOnlyList<TestApp.PhoneNumber>",
                "TestApp.PhoneNumber",
                CollectionTypeKind.IReadOnlyList
            )
        );
        var model = CreateModel("TestConfig", "TestApp", "TestApp.Person");
        return (model, new[] { personNode, phoneNode });
    }

    private static (NormalizationModel Model, IReadOnlyList<TypeGraphNode> Nodes) CreateTreeNodeScenario()
    {
        var treeNode = CreateNode(
            "TestApp.TreeNode",
            "TreeNode",
            hasCircularReference: true,
            SimpleProp("Label", "string", isRef: true),
            NormalizedProp("Parent", "TestApp.TreeNode", nullable: true, isCircular: true),
            CollectionProp(
                "Children",
                "System.Collections.Generic.List<TestApp.TreeNode>",
                "TestApp.TreeNode",
                CollectionTypeKind.List
            )
        );
        var model = CreateModel("TestConfig", "TestApp", "TestApp.TreeNode");
        return (model, new[] { treeNode });
    }

    private static (
        NormalizationModel Model,
        IReadOnlyList<TypeGraphNode> Nodes
    ) CreatePersonWithInlinedMetadataScenario()
    {
        var personNode = CreateNode(
            "TestApp.Person",
            "Person",
            SimpleProp("Name", "string", isRef: true),
            InlinedProp("Metadata", "TestApp.Metadata", isRef: true)
        );
        var model = CreateModel("TestConfig", "TestApp", "TestApp.Person");
        return (model, new[] { personNode });
    }

    // ---- Node/Property Helpers (matching NormalizerEmitterTests style) ----

    private static NormalizationModel CreateModel(
        string configClassName,
        string configNamespace,
        string rootTypeFullyQualifiedName
    )
    {
        return new NormalizationModel
        {
            ConfigClassName = configClassName,
            ConfigNamespace = configNamespace,
            RootTypes = ImmutableArray.Create(
                new RootTypeInfo { TypeSymbol = null!, FullyQualifiedName = rootTypeFullyQualifiedName }
            ),
            AutoDiscover = true,
        };
    }

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
        bool hasCircularReference,
        params AnalyzedProperty[] props
    )
    {
        return new TypeGraphNode
        {
            TypeFullName = fullName,
            TypeName = name,
            Properties = props.ToImmutableArray(),
            HasCircularReference = hasCircularReference,
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

    private static AnalyzedProperty NormalizedProp(string name, string type, bool nullable, bool isCircular = false)
    {
        return new AnalyzedProperty
        {
            Name = name,
            TypeFullName = type,
            Kind = PropertyKind.Normalized,
            IsNullable = nullable,
            IsReferenceType = true,
            IsCircularReference = isCircular,
        };
    }

    private static AnalyzedProperty CollectionProp(
        string name,
        string type,
        string elementType,
        CollectionTypeKind collectionKind,
        bool isCircular = false
    )
    {
        return new AnalyzedProperty
        {
            Name = name,
            TypeFullName = type,
            Kind = PropertyKind.Collection,
            IsCollection = true,
            CollectionElementTypeFullName = elementType,
            CollectionKind = collectionKind,
            IsReferenceType = true,
            IsCircularReference = isCircular,
        };
    }

    private static AnalyzedProperty InlinedProp(string name, string type, bool isRef)
    {
        return new AnalyzedProperty
        {
            Name = name,
            TypeFullName = type,
            Kind = PropertyKind.Inlined,
            IsReferenceType = isRef,
        };
    }

    private static int CountOccurrences(string source, string substring)
    {
        var count = 0;
        var index = 0;
        while ((index = source.IndexOf(substring, index, System.StringComparison.Ordinal)) != -1)
        {
            count++;
            index += substring.Length;
        }
        return count;
    }
}
