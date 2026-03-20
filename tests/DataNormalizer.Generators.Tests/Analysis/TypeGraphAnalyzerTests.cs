using System.Collections.Immutable;
using DataNormalizer.Generators.Analysis;
using DataNormalizer.Generators.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;

namespace DataNormalizer.Generators.Tests.Analysis;

[TestFixture]
public sealed class TypeGraphAnalyzerTests
{
    [Test]
    public void Analyze_SimpleFlatType_AllPropertiesSimple()
    {
        var source = """
            namespace TestApp;
            public class Person
            {
                public string Name { get; set; } = "";
                public int Age { get; set; }
                public bool IsActive { get; set; }
            }
            """;
        var (compilation, rootType) = CompileAndGetType(source, "TestApp.Person");

        var nodes = TypeGraphAnalyzer.Analyze(
            rootType,
            ImmutableHashSet<string>.Empty,
            ImmutableHashSet<string>.Empty,
            ImmutableDictionary<string, TypeConfiguration>.Empty,
            autoDiscover: true
        );

        Assert.That(nodes, Has.Count.EqualTo(1));
        Assert.That(nodes[0].TypeFullName, Is.EqualTo("TestApp.Person"));
        Assert.That(nodes[0].Properties, Has.Length.EqualTo(3));
        Assert.That(nodes[0].Properties.All(p => p.Kind == PropertyKind.Simple), Is.True);
    }

    [Test]
    public void Analyze_EnumProperty_IsSimple()
    {
        var source = """
            namespace TestApp;
            public enum Status { Active, Inactive }
            public class Order
            {
                public Status OrderStatus { get; set; }
            }
            """;
        var (_, rootType) = CompileAndGetType(source, "TestApp.Order");

        var nodes = TypeGraphAnalyzer.Analyze(
            rootType,
            ImmutableHashSet<string>.Empty,
            ImmutableHashSet<string>.Empty,
            ImmutableDictionary<string, TypeConfiguration>.Empty,
            autoDiscover: true
        );

        var prop = nodes[0].Properties.Single(p => p.Name == "OrderStatus");
        Assert.That(prop.Kind, Is.EqualTo(PropertyKind.Simple));
    }

    [Test]
    public void Analyze_NullableValueType_IsSimple()
    {
        var source = """
            namespace TestApp;
            public class Order
            {
                public int? Quantity { get; set; }
                public decimal? Price { get; set; }
            }
            """;
        var (_, rootType) = CompileAndGetType(source, "TestApp.Order");

        var nodes = TypeGraphAnalyzer.Analyze(
            rootType,
            ImmutableHashSet<string>.Empty,
            ImmutableHashSet<string>.Empty,
            ImmutableDictionary<string, TypeConfiguration>.Empty,
            autoDiscover: true
        );

        Assert.That(nodes[0].Properties.All(p => p.Kind == PropertyKind.Simple), Is.True);
    }

    [Test]
    public void Analyze_StringProperty_IsSimple_NotCollection()
    {
        var source = """
            namespace TestApp;
            public class Person
            {
                public string Name { get; set; } = "";
            }
            """;
        var (_, rootType) = CompileAndGetType(source, "TestApp.Person");

        var nodes = TypeGraphAnalyzer.Analyze(
            rootType,
            ImmutableHashSet<string>.Empty,
            ImmutableHashSet<string>.Empty,
            ImmutableDictionary<string, TypeConfiguration>.Empty,
            autoDiscover: true
        );

        var prop = nodes[0].Properties.Single(p => p.Name == "Name");
        Assert.That(prop.Kind, Is.EqualTo(PropertyKind.Simple));
        Assert.That(prop.IsCollection, Is.False);
    }

    [Test]
    public void Analyze_ByteArrayProperty_IsSimple_NotCollection()
    {
        var source = """
            namespace TestApp;
            public class Document
            {
                public byte[] Data { get; set; } = System.Array.Empty<byte>();
            }
            """;
        var (_, rootType) = CompileAndGetType(source, "TestApp.Document");

        var nodes = TypeGraphAnalyzer.Analyze(
            rootType,
            ImmutableHashSet<string>.Empty,
            ImmutableHashSet<string>.Empty,
            ImmutableDictionary<string, TypeConfiguration>.Empty,
            autoDiscover: true
        );

        var prop = nodes[0].Properties.Single(p => p.Name == "Data");
        Assert.That(prop.Kind, Is.EqualTo(PropertyKind.Simple));
        Assert.That(prop.IsCollection, Is.False);
    }

    [Test]
    public void Analyze_DictionaryProperty_IsSimple_NotCollection()
    {
        var source = """
            using System.Collections.Generic;
            namespace TestApp;
            public class Config
            {
                public Dictionary<string, string> Settings { get; set; } = new();
            }
            """;
        var (_, rootType) = CompileAndGetType(source, "TestApp.Config");

        var nodes = TypeGraphAnalyzer.Analyze(
            rootType,
            ImmutableHashSet<string>.Empty,
            ImmutableHashSet<string>.Empty,
            ImmutableDictionary<string, TypeConfiguration>.Empty,
            autoDiscover: true
        );

        var prop = nodes[0].Properties.Single(p => p.Name == "Settings");
        // Dictionary is not a normalizable collection — it's treated as simple/inlined
        Assert.That(prop.Kind, Is.Not.EqualTo(PropertyKind.Collection));
    }

    [Test]
    public void Analyze_NestedComplexType_PropertyKindNormalized_DfsOrder()
    {
        var source = """
            namespace TestApp;
            public class Address
            {
                public string Street { get; set; } = "";
                public string City { get; set; } = "";
            }
            public class Person
            {
                public string Name { get; set; } = "";
                public Address HomeAddress { get; set; } = new();
            }
            """;
        var (_, rootType) = CompileAndGetType(source, "TestApp.Person");

        var nodes = TypeGraphAnalyzer.Analyze(
            rootType,
            ImmutableHashSet<string>.Empty,
            ImmutableHashSet<string>.Empty,
            ImmutableDictionary<string, TypeConfiguration>.Empty,
            autoDiscover: true
        );

        Assert.That(nodes, Has.Count.EqualTo(2));
        // DFS post-order: child (Address) before parent (Person)
        Assert.That(nodes[0].TypeFullName, Is.EqualTo("TestApp.Address"));
        Assert.That(nodes[1].TypeFullName, Is.EqualTo("TestApp.Person"));

        var homeAddr = nodes[1].Properties.Single(p => p.Name == "HomeAddress");
        Assert.That(homeAddr.Kind, Is.EqualTo(PropertyKind.Normalized));
    }

    [Test]
    public void Analyze_MultiLevelNesting_CorrectDfsOrder()
    {
        var source = """
            namespace TestApp;
            public class City { public string Name { get; set; } = ""; }
            public class Address
            {
                public string Street { get; set; } = "";
                public City City { get; set; } = new();
            }
            public class Person
            {
                public string Name { get; set; } = "";
                public Address HomeAddress { get; set; } = new();
            }
            """;
        var (_, rootType) = CompileAndGetType(source, "TestApp.Person");

        var nodes = TypeGraphAnalyzer.Analyze(
            rootType,
            ImmutableHashSet<string>.Empty,
            ImmutableHashSet<string>.Empty,
            ImmutableDictionary<string, TypeConfiguration>.Empty,
            autoDiscover: true
        );

        Assert.That(nodes, Has.Count.EqualTo(3));
        // DFS post-order: City, Address, Person
        Assert.That(nodes[0].TypeFullName, Is.EqualTo("TestApp.City"));
        Assert.That(nodes[1].TypeFullName, Is.EqualTo("TestApp.Address"));
        Assert.That(nodes[2].TypeFullName, Is.EqualTo("TestApp.Person"));
    }

    [Test]
    public void Analyze_ListOfComplexType_IsCollection()
    {
        var source = """
            using System.Collections.Generic;
            namespace TestApp;
            public class PhoneNumber { public string Number { get; set; } = ""; }
            public class Person
            {
                public string Name { get; set; } = "";
                public List<PhoneNumber> PhoneNumbers { get; set; } = new();
            }
            """;
        var (_, rootType) = CompileAndGetType(source, "TestApp.Person");

        var nodes = TypeGraphAnalyzer.Analyze(
            rootType,
            ImmutableHashSet<string>.Empty,
            ImmutableHashSet<string>.Empty,
            ImmutableDictionary<string, TypeConfiguration>.Empty,
            autoDiscover: true
        );

        var prop = nodes.Last().Properties.Single(p => p.Name == "PhoneNumbers");
        Assert.That(prop.Kind, Is.EqualTo(PropertyKind.Collection));
        Assert.That(prop.IsCollection, Is.True);
        Assert.That(prop.CollectionElementTypeFullName, Is.EqualTo("TestApp.PhoneNumber"));
    }

    [Test]
    public void Analyze_ArrayOfComplexType_IsCollection()
    {
        var source = """
            namespace TestApp;
            public class PhoneNumber { public string Number { get; set; } = ""; }
            public class Person
            {
                public string Name { get; set; } = "";
                public PhoneNumber[] PhoneNumbers { get; set; } = System.Array.Empty<PhoneNumber>();
            }
            """;
        var (_, rootType) = CompileAndGetType(source, "TestApp.Person");

        var nodes = TypeGraphAnalyzer.Analyze(
            rootType,
            ImmutableHashSet<string>.Empty,
            ImmutableHashSet<string>.Empty,
            ImmutableDictionary<string, TypeConfiguration>.Empty,
            autoDiscover: true
        );

        var prop = nodes.Last().Properties.Single(p => p.Name == "PhoneNumbers");
        Assert.That(prop.Kind, Is.EqualTo(PropertyKind.Collection));
        Assert.That(prop.IsCollection, Is.True);
        Assert.That(prop.CollectionElementTypeFullName, Is.EqualTo("TestApp.PhoneNumber"));
    }

    [Test]
    public void Analyze_IReadOnlyListOfComplexType_IsCollection()
    {
        var source = """
            using System.Collections.Generic;
            namespace TestApp;
            public class Tag { public string Label { get; set; } = ""; }
            public class Item
            {
                public IReadOnlyList<Tag> Tags { get; set; } = new List<Tag>();
            }
            """;
        var (_, rootType) = CompileAndGetType(source, "TestApp.Item");

        var nodes = TypeGraphAnalyzer.Analyze(
            rootType,
            ImmutableHashSet<string>.Empty,
            ImmutableHashSet<string>.Empty,
            ImmutableDictionary<string, TypeConfiguration>.Empty,
            autoDiscover: true
        );

        var prop = nodes.Last().Properties.Single(p => p.Name == "Tags");
        Assert.That(prop.Kind, Is.EqualTo(PropertyKind.Collection));
        Assert.That(prop.IsCollection, Is.True);
    }

    [Test]
    public void Analyze_SelfReferentialType_DetectsCircularReference()
    {
        var source = """
            using System.Collections.Generic;
            namespace TestApp;
            public class TreeNode
            {
                public string Label { get; set; } = "";
                public TreeNode? Parent { get; set; }
                public List<TreeNode> Children { get; set; } = new();
            }
            """;
        var (_, rootType) = CompileAndGetType(source, "TestApp.TreeNode");

        var nodes = TypeGraphAnalyzer.Analyze(
            rootType,
            ImmutableHashSet<string>.Empty,
            ImmutableHashSet<string>.Empty,
            ImmutableDictionary<string, TypeConfiguration>.Empty,
            autoDiscover: true
        );

        Assert.That(nodes, Has.Count.EqualTo(1)); // TreeNode only (self-referencing doesn't add new types)
        Assert.That(nodes[0].HasCircularReference, Is.True);

        var parentProp = nodes[0].Properties.Single(p => p.Name == "Parent");
        Assert.That(parentProp.IsCircularReference, Is.True);
    }

    [Test]
    public void Analyze_InheritedProperties_Included()
    {
        var source = """
            namespace TestApp;
            public class Entity
            {
                public int Id { get; set; }
            }
            public class Person : Entity
            {
                public string Name { get; set; } = "";
            }
            """;
        var (_, rootType) = CompileAndGetType(source, "TestApp.Person");

        var nodes = TypeGraphAnalyzer.Analyze(
            rootType,
            ImmutableHashSet<string>.Empty,
            ImmutableHashSet<string>.Empty,
            ImmutableDictionary<string, TypeConfiguration>.Empty,
            autoDiscover: true
        );

        var allPropNames = nodes[0].Properties.Select(p => p.Name).ToArray();
        Assert.That(allPropNames, Does.Contain("Id")); // inherited
        Assert.That(allPropNames, Does.Contain("Name")); // declared
    }

    [Test]
    public void Analyze_InlinedType_PropertyKindInlined()
    {
        var source = """
            namespace TestApp;
            public class Metadata { public string Key { get; set; } = ""; }
            public class Person
            {
                public string Name { get; set; } = "";
                public Metadata Meta { get; set; } = new();
            }
            """;
        var (_, rootType) = CompileAndGetType(source, "TestApp.Person");

        // Mark Metadata as inlined
        var inlinedTypes = ImmutableHashSet.Create("TestApp.Metadata");
        var nodes = TypeGraphAnalyzer.Analyze(
            rootType,
            inlinedTypes,
            ImmutableHashSet<string>.Empty,
            ImmutableDictionary<string, TypeConfiguration>.Empty,
            autoDiscover: true
        );

        // Should only have Person (Metadata is inlined, not in graph)
        Assert.That(nodes, Has.Count.EqualTo(1));
        var metaProp = nodes[0].Properties.Single(p => p.Name == "Meta");
        Assert.That(metaProp.Kind, Is.EqualTo(PropertyKind.Inlined));
    }

    [Test]
    public void Analyze_ExplicitOnlyMode_OnlyRecursesExplicitTypes()
    {
        var source = """
            namespace TestApp;
            public class Address { public string Street { get; set; } = ""; }
            public class PhoneNumber { public string Number { get; set; } = ""; }
            public class Person
            {
                public string Name { get; set; } = "";
                public Address HomeAddress { get; set; } = new();
                public PhoneNumber Phone { get; set; } = new();
            }
            """;
        var (_, rootType) = CompileAndGetType(source, "TestApp.Person");

        // Only Address is explicit, PhoneNumber is not
        var explicitTypes = ImmutableHashSet.Create("TestApp.Address");
        var nodes = TypeGraphAnalyzer.Analyze(
            rootType,
            ImmutableHashSet<string>.Empty,
            explicitTypes,
            ImmutableDictionary<string, TypeConfiguration>.Empty,
            autoDiscover: false
        );

        // Should have Address (explicit) and Person (root), but NOT PhoneNumber
        var typeNames = nodes.Select(n => n.TypeFullName).ToArray();
        Assert.That(typeNames, Does.Contain("TestApp.Address"));
        Assert.That(typeNames, Does.Contain("TestApp.Person"));
        Assert.That(typeNames, Does.Not.Contain("TestApp.PhoneNumber"));

        // PhoneNumber property should be inlined (not normalized)
        var phoneProp = nodes.Single(n => n.TypeFullName == "TestApp.Person").Properties.Single(p => p.Name == "Phone");
        Assert.That(phoneProp.Kind, Is.EqualTo(PropertyKind.Inlined));
    }

    [Test]
    public void Analyze_ListOfSimpleType_IsSimple()
    {
        var source = """
            using System.Collections.Generic;
            namespace TestApp;
            public class Person
            {
                public List<string> Tags { get; set; } = new();
            }
            """;
        var (_, rootType) = CompileAndGetType(source, "TestApp.Person");

        var nodes = TypeGraphAnalyzer.Analyze(
            rootType,
            ImmutableHashSet<string>.Empty,
            ImmutableHashSet<string>.Empty,
            ImmutableDictionary<string, TypeConfiguration>.Empty,
            autoDiscover: true
        );

        // List<string> — collection of simple type, kept as-is (Simple, not Collection)
        var prop = nodes[0].Properties.Single(p => p.Name == "Tags");
        Assert.That(prop.Kind, Is.EqualTo(PropertyKind.Simple));
    }

    [Test]
    public void Analyze_ListOfComplexType_CollectionKindIsList()
    {
        var source = """
            using System.Collections.Generic;
            namespace TestApp;
            public class PhoneNumber { public string Number { get; set; } = ""; }
            public class Person
            {
                public List<PhoneNumber> Phones { get; set; } = new();
            }
            """;
        var (_, rootType) = CompileAndGetType(source, "TestApp.Person");
        var nodes = TypeGraphAnalyzer.Analyze(
            rootType,
            ImmutableHashSet<string>.Empty,
            ImmutableHashSet<string>.Empty,
            ImmutableDictionary<string, TypeConfiguration>.Empty,
            autoDiscover: true
        );
        var prop = nodes.Last().Properties.Single(p => p.Name == "Phones");
        Assert.That(prop.CollectionKind, Is.EqualTo(CollectionTypeKind.List));
    }

    [Test]
    public void Analyze_ArrayOfComplexType_CollectionKindIsArray()
    {
        var source = """
            namespace TestApp;
            public class PhoneNumber { public string Number { get; set; } = ""; }
            public class Person
            {
                public PhoneNumber[] Phones { get; set; } = System.Array.Empty<PhoneNumber>();
            }
            """;
        var (_, rootType) = CompileAndGetType(source, "TestApp.Person");
        var nodes = TypeGraphAnalyzer.Analyze(
            rootType,
            ImmutableHashSet<string>.Empty,
            ImmutableHashSet<string>.Empty,
            ImmutableDictionary<string, TypeConfiguration>.Empty,
            autoDiscover: true
        );
        var prop = nodes.Last().Properties.Single(p => p.Name == "Phones");
        Assert.That(prop.CollectionKind, Is.EqualTo(CollectionTypeKind.Array));
    }

    [Test]
    public void Analyze_IReadOnlyListOfComplexType_CollectionKindIsIReadOnlyList()
    {
        var source = """
            using System.Collections.Generic;
            namespace TestApp;
            public class Tag { public string Label { get; set; } = ""; }
            public class Item
            {
                public IReadOnlyList<Tag> Tags { get; set; } = new List<Tag>();
            }
            """;
        var (_, rootType) = CompileAndGetType(source, "TestApp.Item");
        var nodes = TypeGraphAnalyzer.Analyze(
            rootType,
            ImmutableHashSet<string>.Empty,
            ImmutableHashSet<string>.Empty,
            ImmutableDictionary<string, TypeConfiguration>.Empty,
            autoDiscover: true
        );
        var prop = nodes.Last().Properties.Single(p => p.Name == "Tags");
        Assert.That(prop.CollectionKind, Is.EqualTo(CollectionTypeKind.IReadOnlyList));
    }

    [Test]
    public void Analyze_NullableReferenceTypeProperty_IsNullableTrue()
    {
        var source = """
            #nullable enable
            namespace TestApp;
            public class Address { public string Street { get; set; } = ""; }
            public class Person
            {
                public string Name { get; set; } = "";
                public Address? WorkAddress { get; set; }
            }
            """;
        var (_, rootType) = CompileAndGetType(source, "TestApp.Person");
        var nodes = TypeGraphAnalyzer.Analyze(
            rootType,
            ImmutableHashSet<string>.Empty,
            ImmutableHashSet<string>.Empty,
            ImmutableDictionary<string, TypeConfiguration>.Empty,
            autoDiscover: true
        );
        var workAddr = nodes.Last().Properties.Single(p => p.Name == "WorkAddress");
        Assert.That(workAddr.Kind, Is.EqualTo(PropertyKind.Normalized));
        Assert.That(workAddr.IsNullable, Is.True);
    }

    [Test]
    public void Analyze_StringProperty_IsReferenceTypeTrue()
    {
        var source = """
            namespace TestApp;
            public class Person { public string Name { get; set; } = ""; public int Age { get; set; } }
            """;
        var (_, rootType) = CompileAndGetType(source, "TestApp.Person");
        var nodes = TypeGraphAnalyzer.Analyze(
            rootType,
            ImmutableHashSet<string>.Empty,
            ImmutableHashSet<string>.Empty,
            ImmutableDictionary<string, TypeConfiguration>.Empty,
            autoDiscover: true
        );
        var nameProp = nodes[0].Properties.Single(p => p.Name == "Name");
        var ageProp = nodes[0].Properties.Single(p => p.Name == "Age");
        Assert.That(nameProp.IsReferenceType, Is.True);
        Assert.That(ageProp.IsReferenceType, Is.False);
    }

    [Test]
    public void Analyze_IgnoredProperty_ExcludedFromNode()
    {
        var source = """
            namespace TestApp;
            public class Person
            {
                public string Name { get; set; } = "";
                public string InternalId { get; set; } = "";
                public int Age { get; set; }
            }
            """;
        var (_, rootType) = CompileAndGetType(source, "TestApp.Person");

        var typeConfigs = ImmutableDictionary.CreateBuilder<string, TypeConfiguration>();
        typeConfigs.Add(
            "TestApp.Person",
            new TypeConfiguration
            {
                FullyQualifiedName = "TestApp.Person",
                IgnoredProperties = ImmutableHashSet.Create("InternalId"),
            }
        );

        var nodes = TypeGraphAnalyzer.Analyze(
            rootType,
            ImmutableHashSet<string>.Empty,
            ImmutableHashSet<string>.Empty,
            typeConfigs.ToImmutable(),
            autoDiscover: true
        );

        var propNames = nodes[0].Properties.Select(p => p.Name).ToArray();
        Assert.That(propNames, Does.Contain("Name"));
        Assert.That(propNames, Does.Contain("Age"));
        Assert.That(propNames, Does.Not.Contain("InternalId"));
    }

    [Test]
    public void Analyze_ExplicitOnlyMode_OnlyIncludedProperties()
    {
        var source = """
            namespace TestApp;
            public class Person
            {
                public string Name { get; set; } = "";
                public int Age { get; set; }
                public string Secret { get; set; } = "";
            }
            """;
        var (_, rootType) = CompileAndGetType(source, "TestApp.Person");

        var typeConfigs = ImmutableDictionary.CreateBuilder<string, TypeConfiguration>();
        typeConfigs.Add(
            "TestApp.Person",
            new TypeConfiguration
            {
                FullyQualifiedName = "TestApp.Person",
                IncludedProperties = ImmutableHashSet.Create("Name", "Age"),
                PropertyMode = GeneratorPropertyMode.ExplicitOnly,
            }
        );

        var nodes = TypeGraphAnalyzer.Analyze(
            rootType,
            ImmutableHashSet<string>.Empty,
            ImmutableHashSet<string>.Empty,
            typeConfigs.ToImmutable(),
            autoDiscover: true
        );

        var propNames = nodes[0].Properties.Select(p => p.Name).ToArray();
        Assert.That(propNames, Does.Contain("Name"));
        Assert.That(propNames, Does.Contain("Age"));
        Assert.That(propNames, Does.Not.Contain("Secret"));
    }

    [Test]
    public void Analyze_NoTypeConfig_IncludesAllProperties()
    {
        var source = """
            namespace TestApp;
            public class Person
            {
                public string Name { get; set; } = "";
                public int Age { get; set; }
            }
            """;
        var (_, rootType) = CompileAndGetType(source, "TestApp.Person");

        var nodes = TypeGraphAnalyzer.Analyze(
            rootType,
            ImmutableHashSet<string>.Empty,
            ImmutableHashSet<string>.Empty,
            ImmutableDictionary<string, TypeConfiguration>.Empty,
            autoDiscover: true
        );

        Assert.That(nodes[0].Properties, Has.Length.EqualTo(2));
    }

    [Test]
    public void Analyze_TriangleCycle_AllThreeTypesHaveCircularReference()
    {
        var source = """
            namespace TestApp;
            public class A
            {
                public string Name { get; set; } = "";
                public B RefB { get; set; } = new();
            }
            public class B
            {
                public string Name { get; set; } = "";
                public C RefC { get; set; } = new();
            }
            public class C
            {
                public string Name { get; set; } = "";
                public A RefA { get; set; } = new();
            }
            """;
        var (_, rootType) = CompileAndGetType(source, "TestApp.A");

        var nodes = TypeGraphAnalyzer.Analyze(
            rootType,
            ImmutableHashSet<string>.Empty,
            ImmutableHashSet<string>.Empty,
            ImmutableDictionary<string, TypeConfiguration>.Empty,
            autoDiscover: true
        );

        // ALL three types should have HasCircularReference
        var nodeA = nodes.Single(n => n.TypeName == "A");
        var nodeB = nodes.Single(n => n.TypeName == "B");
        var nodeC = nodes.Single(n => n.TypeName == "C");

        Assert.That(nodeA.HasCircularReference, Is.True, "A should be marked as circular");
        Assert.That(nodeB.HasCircularReference, Is.True, "B should be marked as circular");
        Assert.That(nodeC.HasCircularReference, Is.True, "C should be marked as circular");
    }

    [Test]
    public void Analyze_MutualReference_BothTypesHaveCircularReference()
    {
        var source = """
            namespace TestApp;
            public class Person
            {
                public string Name { get; set; } = "";
                public Company Employer { get; set; } = new();
            }
            public class Company
            {
                public string Title { get; set; } = "";
                public Person Ceo { get; set; } = new();
            }
            """;
        var (_, rootType) = CompileAndGetType(source, "TestApp.Person");

        var nodes = TypeGraphAnalyzer.Analyze(
            rootType,
            ImmutableHashSet<string>.Empty,
            ImmutableHashSet<string>.Empty,
            ImmutableDictionary<string, TypeConfiguration>.Empty,
            autoDiscover: true
        );

        var personNode = nodes.Single(n => n.TypeName == "Person");
        var companyNode = nodes.Single(n => n.TypeName == "Company");

        Assert.That(personNode.HasCircularReference, Is.True, "Person should be marked as circular");
        Assert.That(companyNode.HasCircularReference, Is.True, "Company should be marked as circular");
    }

    [Test]
    public void Analyze_FourHopCycle_AllFourTypesHaveCircularReference()
    {
        var source = """
            namespace TestApp;
            public class W { public string Name { get; set; } = ""; public X RefX { get; set; } = new(); }
            public class X { public string Name { get; set; } = ""; public Y RefY { get; set; } = new(); }
            public class Y { public string Name { get; set; } = ""; public Z RefZ { get; set; } = new(); }
            public class Z { public string Name { get; set; } = ""; public W RefW { get; set; } = new(); }
            """;
        var (_, rootType) = CompileAndGetType(source, "TestApp.W");

        var nodes = TypeGraphAnalyzer.Analyze(
            rootType,
            ImmutableHashSet<string>.Empty,
            ImmutableHashSet<string>.Empty,
            ImmutableDictionary<string, TypeConfiguration>.Empty,
            autoDiscover: true
        );

        foreach (var node in nodes)
        {
            Assert.That(node.HasCircularReference, Is.True, $"{node.TypeName} should be marked as circular");
        }
    }

    // ---- Test Helper ----

    private static (CSharpCompilation Compilation, INamedTypeSymbol RootType) CompileAndGetType(
        string source,
        string fullyQualifiedTypeName
    )
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);

        var references = AppDomain
            .CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .Cast<MetadataReference>()
            .ToList();

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );

        var rootType = compilation.GetTypeByMetadataName(fullyQualifiedTypeName);
        Assert.That(rootType, Is.Not.Null, $"Could not find type '{fullyQualifiedTypeName}' in compilation");

        return (compilation, rootType!);
    }
}
