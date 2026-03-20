using System.Collections.Immutable;
using DataNormalizer.Generators.Emitters;
using DataNormalizer.Generators.Models;
using NUnit.Framework;

namespace DataNormalizer.Generators.Tests.Emitters;

[TestFixture]
public sealed class DtoEmitterTests
{
    [Test]
    public void Emit_SimpleFlatType_GeneratesPartialClassWithIEquatable()
    {
        var node = CreateNode(
            "TestApp.Person",
            "Person",
            SimpleProp("Name", "string", isRef: true),
            SimpleProp("Age", "int", isRef: false)
        );

        var result = DtoEmitter.Emit(node);

        Assert.That(
            result,
            Does.Contain("public partial class NormalizedPerson : System.IEquatable<NormalizedPerson>")
        );
        Assert.That(result, Does.Contain("[System.CodeDom.Compiler.GeneratedCode(\"DataNormalizer\""));
        Assert.That(result, Does.Contain("namespace TestApp;"));
        Assert.That(result, Does.Contain("public string Name { get; set; }"));
        Assert.That(result, Does.Contain("public int Age { get; set; }"));
    }

    [Test]
    public void Emit_NormalizedProperty_GeneratesIndexProperty()
    {
        var node = CreateNode(
            "TestApp.Person",
            "Person",
            SimpleProp("Name", "string", isRef: true),
            NormalizedProp("HomeAddress", "TestApp.Address", nullable: false)
        );

        var result = DtoEmitter.Emit(node);

        Assert.That(result, Does.Contain("public int HomeAddressIndex { get; set; }"));
        Assert.That(result, Does.Not.Contain("public TestApp.Address"));
    }

    [Test]
    public void Emit_NullableNormalizedProperty_GeneratesNullableIndex()
    {
        var node = CreateNode(
            "TestApp.Person",
            "Person",
            NormalizedProp("WorkAddress", "TestApp.Address", nullable: true)
        );

        var result = DtoEmitter.Emit(node);

        Assert.That(result, Does.Contain("public int? WorkAddressIndex { get; set; }"));
    }

    [Test]
    public void Emit_CollectionProperty_GeneratesIndicesArray()
    {
        var node = CreateNode(
            "TestApp.Person",
            "Person",
            CollectionProp(
                "PhoneNumbers",
                "System.Collections.Generic.List<TestApp.PhoneNumber>",
                "TestApp.PhoneNumber"
            )
        );

        var result = DtoEmitter.Emit(node);

        Assert.That(result, Does.Contain("public int[] PhoneNumbersIndices { get; set; }"));
    }

    [Test]
    public void Emit_InlinedProperty_KeepsOriginalType()
    {
        var node = CreateNode("TestApp.Person", "Person", InlinedProp("Meta", "TestApp.Metadata", isRef: true));

        var result = DtoEmitter.Emit(node);

        Assert.That(result, Does.Contain("public TestApp.Metadata Meta { get; set; }"));
    }

    [Test]
    public void Emit_MixedProperties_CorrectCombination()
    {
        var node = CreateNode(
            "TestApp.Person",
            "Person",
            SimpleProp("Name", "string", isRef: true),
            SimpleProp("Age", "int", isRef: false),
            NormalizedProp("HomeAddress", "TestApp.Address", nullable: false),
            NormalizedProp("WorkAddress", "TestApp.Address", nullable: true),
            CollectionProp(
                "PhoneNumbers",
                "System.Collections.Generic.List<TestApp.PhoneNumber>",
                "TestApp.PhoneNumber"
            ),
            InlinedProp("Meta", "TestApp.Metadata", isRef: true)
        );

        var result = DtoEmitter.Emit(node);

        Assert.That(result, Does.Contain("public string Name { get; set; }"));
        Assert.That(result, Does.Contain("public int Age { get; set; }"));
        Assert.That(result, Does.Contain("public int HomeAddressIndex { get; set; }"));
        Assert.That(result, Does.Contain("public int? WorkAddressIndex { get; set; }"));
        Assert.That(result, Does.Contain("public int[] PhoneNumbersIndices { get; set; }"));
        Assert.That(result, Does.Contain("public TestApp.Metadata Meta { get; set; }"));
    }

    [Test]
    public void Emit_EqualsMethod_HandlesReferenceAndValueTypes()
    {
        var node = CreateNode(
            "TestApp.Person",
            "Person",
            SimpleProp("Name", "string", isRef: true),
            SimpleProp("Age", "int", isRef: false)
        );

        var result = DtoEmitter.Emit(node);

        Assert.That(result, Does.Contain("public bool Equals(NormalizedPerson? other)"));
        Assert.That(result, Does.Contain("if (other is null) return false;"));
        Assert.That(result, Does.Contain("ReferenceEquals(this, other)"));
    }

    [Test]
    public void Emit_GetHashCode_NullSafeForReferenceTypes()
    {
        var node = CreateNode(
            "TestApp.Person",
            "Person",
            SimpleProp("Name", "string", isRef: true),
            SimpleProp("Age", "int", isRef: false)
        );

        var result = DtoEmitter.Emit(node);

        Assert.That(result, Does.Contain("GetHashCode()"));
        // Reference type should have null-safe hash
        Assert.That(
            result,
            Does.Contain("Name?.GetHashCode() ?? 0").Or.Contain("Name is null ? 0 : Name.GetHashCode()")
        );
        // Value type should use direct GetHashCode (no null check)
        Assert.That(result, Does.Contain("Age.GetHashCode()"));
    }

    [Test]
    public void Emit_GetHashCode_CachesComputedHash()
    {
        var node = CreateNode(
            "TestApp.Person",
            "Person",
            SimpleProp("Name", "string", isRef: true),
            SimpleProp("Age", "int", isRef: false)
        );

        var result = DtoEmitter.Emit(node);

        // Cache fields should be present in the class body
        Assert.That(result, Does.Contain("private int _cachedHashCode;"));
        Assert.That(result, Does.Contain("private bool _hashComputed;"));

        // GetHashCode should check cache first and store result
        Assert.That(result, Does.Contain("if (_hashComputed) return _cachedHashCode;"));
        Assert.That(result, Does.Contain("_cachedHashCode = hash;"));
        Assert.That(result, Does.Contain("_hashComputed = true;"));
    }

    [Test]
    public void Emit_ArrayProperty_SequenceEqualInEquals()
    {
        var node = CreateNode(
            "TestApp.Person",
            "Person",
            CollectionProp(
                "PhoneNumbers",
                "System.Collections.Generic.List<TestApp.PhoneNumber>",
                "TestApp.PhoneNumber"
            )
        );

        var result = DtoEmitter.Emit(node);

        // Array equality must use SequenceEqual, not ==
        Assert.That(result, Does.Contain("SequenceEqual"));
    }

    [Test]
    public void Emit_GeneratedCodeAttribute_Present()
    {
        var node = CreateNode("TestApp.Person", "Person", SimpleProp("Name", "string", isRef: true));

        var result = DtoEmitter.Emit(node);

        Assert.That(result, Does.Contain("[System.CodeDom.Compiler.GeneratedCode(\"DataNormalizer\""));
    }

    [Test]
    public void Emit_NullableEnable_Present()
    {
        var node = CreateNode("TestApp.Person", "Person", SimpleProp("Name", "string", isRef: true));

        var result = DtoEmitter.Emit(node);

        Assert.That(result, Does.Contain("#nullable enable"));
    }

    [Test]
    public void Emit_EnumProperty_SimpleType()
    {
        var node = CreateNode("TestApp.Order", "Order", SimpleProp("Status", "TestApp.OrderStatus", isRef: false));

        var result = DtoEmitter.Emit(node);

        Assert.That(result, Does.Contain("public TestApp.OrderStatus Status { get; set; }"));
    }

    [Test]
    public void Emit_CopySourceAttributes_EmitsAttributesOnProperties()
    {
        var node = CreateNode(
            "TestApp.Person",
            "Person",
            new AnalyzedProperty
            {
                Name = "Name",
                TypeFullName = "string",
                Kind = PropertyKind.Simple,
                IsReferenceType = true,
                SourceAttributes = ImmutableArray.Create("[System.Text.Json.Serialization.JsonPropertyName(\"name\")]"),
            }
        );

        var result = DtoEmitter.Emit(node, copySourceAttributes: true, jsonNamingPolicy: null);

        Assert.That(result, Does.Contain("[System.Text.Json.Serialization.JsonPropertyName(\"name\")]"));
        Assert.That(result, Does.Contain("public string Name { get; set; }"));
    }

    [Test]
    public void Emit_CopySourceAttributes_OnInlinedProperty_EmitsAttributes()
    {
        var node = CreateNode(
            "TestApp.Person",
            "Person",
            new AnalyzedProperty
            {
                Name = "Meta",
                TypeFullName = "TestApp.Metadata",
                Kind = PropertyKind.Inlined,
                IsReferenceType = true,
                SourceAttributes = ImmutableArray.Create("[System.Obsolete]"),
            }
        );

        var result = DtoEmitter.Emit(node, copySourceAttributes: true, jsonNamingPolicy: null);

        Assert.That(result, Does.Contain("[System.Obsolete]"));
        Assert.That(result, Does.Contain("public TestApp.Metadata Meta { get; set; }"));
    }

    [Test]
    public void Emit_CopySourceAttributes_False_DoesNotEmitAttributes()
    {
        var node = CreateNode(
            "TestApp.Person",
            "Person",
            new AnalyzedProperty
            {
                Name = "Name",
                TypeFullName = "string",
                Kind = PropertyKind.Simple,
                IsReferenceType = true,
                SourceAttributes = ImmutableArray.Create("[System.Text.Json.Serialization.JsonPropertyName(\"name\")]"),
            }
        );

        var result = DtoEmitter.Emit(node, copySourceAttributes: false, jsonNamingPolicy: null);

        Assert.That(result, Does.Not.Contain("[System.Text.Json.Serialization.JsonPropertyName(\"name\")]"));
        Assert.That(result, Does.Contain("public string Name { get; set; }"));
    }

    [Test]
    public void Emit_UseJsonNaming_CamelCase_EmitsJsonPropertyNameOnAllProperties()
    {
        var node = CreateNode(
            "TestApp.Person",
            "Person",
            SimpleProp("Name", "string", isRef: true),
            SimpleProp("Age", "int", isRef: false),
            NormalizedProp("HomeAddress", "TestApp.Address", nullable: false)
        );

        var result = DtoEmitter.Emit(node, copySourceAttributes: false, jsonNamingPolicy: "CamelCase");

        Assert.That(result, Does.Contain("[System.Text.Json.Serialization.JsonPropertyName(\"name\")]"));
        Assert.That(result, Does.Contain("[System.Text.Json.Serialization.JsonPropertyName(\"age\")]"));
        Assert.That(result, Does.Contain("[System.Text.Json.Serialization.JsonPropertyName(\"homeAddressIndex\")]"));
    }

    [Test]
    public void Emit_UseJsonNaming_CamelCase_CollectionProperty_UsesIndicesSuffix()
    {
        var node = CreateNode(
            "TestApp.Person",
            "Person",
            CollectionProp(
                "PhoneNumbers",
                "System.Collections.Generic.List<TestApp.PhoneNumber>",
                "TestApp.PhoneNumber"
            )
        );

        var result = DtoEmitter.Emit(node, copySourceAttributes: false, jsonNamingPolicy: "CamelCase");

        Assert.That(result, Does.Contain("[System.Text.Json.Serialization.JsonPropertyName(\"phoneNumbersIndices\")]"));
    }

    [Test]
    public void Emit_UseJsonNaming_DoesNotOverrideExistingJsonPropertyName()
    {
        var node = CreateNode(
            "TestApp.Person",
            "Person",
            new AnalyzedProperty
            {
                Name = "Name",
                TypeFullName = "string",
                Kind = PropertyKind.Simple,
                IsReferenceType = true,
                SourceAttributes = ImmutableArray.Create(
                    "[System.Text.Json.Serialization.JsonPropertyName(\"custom_name\")]"
                ),
            }
        );

        var result = DtoEmitter.Emit(node, copySourceAttributes: true, jsonNamingPolicy: "CamelCase");

        // Should keep the explicit attribute, NOT add a generated one
        Assert.That(result, Does.Contain("[System.Text.Json.Serialization.JsonPropertyName(\"custom_name\")]"));
        Assert.That(result, Does.Not.Contain("JsonPropertyName(\"name\")"));
    }

    [Test]
    public void Emit_UseJsonNaming_Null_DoesNotEmitJsonPropertyName()
    {
        var node = CreateNode("TestApp.Person", "Person", SimpleProp("Name", "string", isRef: true));

        var result = DtoEmitter.Emit(node, copySourceAttributes: false, jsonNamingPolicy: null);

        Assert.That(result, Does.Not.Contain("JsonPropertyName"));
    }

    [Test]
    public void Emit_CircularProperties_ExcludedFromEquals()
    {
        var node = new TypeGraphNode
        {
            TypeFullName = "TestApp.TreeNode",
            TypeName = "TreeNode",
            HasCircularReference = true,
            Properties = ImmutableArray.Create(
                new AnalyzedProperty
                {
                    Name = "Label",
                    TypeFullName = "string",
                    Kind = PropertyKind.Simple,
                    IsReferenceType = true,
                },
                new AnalyzedProperty
                {
                    Name = "Parent",
                    TypeFullName = "TestApp.TreeNode",
                    Kind = PropertyKind.Normalized,
                    IsNullable = true,
                    IsCircularReference = true,
                    IsReferenceType = true,
                },
                new AnalyzedProperty
                {
                    Name = "Children",
                    TypeFullName = "System.Collections.Generic.List<TestApp.TreeNode>",
                    Kind = PropertyKind.Collection,
                    IsCollection = true,
                    IsCircularReference = true,
                    CollectionElementTypeFullName = "TestApp.TreeNode",
                    CollectionKind = CollectionTypeKind.List,
                    IsReferenceType = true,
                }
            ),
        };

        var result = DtoEmitter.Emit(node);

        // Equals should compare Label but NOT ParentIndex or ChildrenIndices
        Assert.That(result, Does.Contain("Label == other.Label"));
        Assert.That(result, Does.Not.Match("other\\.ParentIndex"));
        Assert.That(result, Does.Not.Match("other\\.ChildrenIndices"));

        // GetHashCode should hash Label but NOT ParentIndex or ChildrenIndices
        var hashSection = result.Substring(result.IndexOf("GetHashCode()"));
        Assert.That(hashSection, Does.Contain("Label"));
        Assert.That(hashSection, Does.Not.Contain("ParentIndex"));
        Assert.That(hashSection, Does.Not.Contain("ChildrenIndices"));
    }

    [Test]
    public void Emit_MixedCircularAndNonCircular_OnlyNonCircularInEquals()
    {
        var node = new TypeGraphNode
        {
            TypeFullName = "TestApp.Employee",
            TypeName = "Employee",
            HasCircularReference = true,
            Properties = ImmutableArray.Create(
                new AnalyzedProperty
                {
                    Name = "Name",
                    TypeFullName = "string",
                    Kind = PropertyKind.Simple,
                    IsReferenceType = true,
                },
                new AnalyzedProperty
                {
                    Name = "Mentor",
                    TypeFullName = "TestApp.Employee",
                    Kind = PropertyKind.Normalized,
                    IsNullable = true,
                    IsCircularReference = true,
                    IsReferenceType = true,
                },
                new AnalyzedProperty
                {
                    Name = "Department",
                    TypeFullName = "TestApp.Department",
                    Kind = PropertyKind.Normalized,
                    IsCircularReference = false,
                    IsReferenceType = true,
                }
            ),
        };

        var result = DtoEmitter.Emit(node);

        // Equals should compare Name and DepartmentIndex but NOT MentorIndex
        Assert.That(result, Does.Contain("DepartmentIndex"));
        Assert.That(result, Does.Not.Match("other\\.MentorIndex"));
    }

    [Test]
    public void Emit_AllCircularProperties_EqualsReturnsTrue()
    {
        var node = new TypeGraphNode
        {
            TypeFullName = "TestApp.PureLink",
            TypeName = "PureLink",
            HasCircularReference = true,
            Properties = ImmutableArray.Create(
                new AnalyzedProperty
                {
                    Name = "Next",
                    TypeFullName = "TestApp.PureLink",
                    Kind = PropertyKind.Normalized,
                    IsNullable = true,
                    IsCircularReference = true,
                    IsReferenceType = true,
                }
            ),
        };

        var result = DtoEmitter.Emit(node);

        // With all properties circular, Equals should return true
        Assert.That(result, Does.Contain("return true;"));
        // GetHashCode should not reference NextIndex
        var hashSection = result.Substring(result.IndexOf("GetHashCode()"));
        Assert.That(hashSection, Does.Not.Contain("NextIndex"));
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

    private static AnalyzedProperty CollectionProp(string name, string type, string elementType)
    {
        return new AnalyzedProperty
        {
            Name = name,
            TypeFullName = type,
            Kind = PropertyKind.Collection,
            IsCollection = true,
            CollectionElementTypeFullName = elementType,
            CollectionKind = CollectionTypeKind.List,
            IsReferenceType = true,
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
}
