using System.Collections.Immutable;
using DataNormalizer.Generators.Emitters;
using DataNormalizer.Generators.Models;
using NUnit.Framework;

namespace DataNormalizer.Generators.Tests.Emitters;

[TestFixture]
public sealed class DtoEmitterTests
{
    private static readonly NamingModel DefaultNaming = NamingModel.Default;

    [Test]
    public void Emit_SimpleFlatType_GeneratesPartialClassWithIEquatable()
    {
        var node = CreateNode(
            "TestApp.Person",
            "Person",
            SimpleProp("Name", "string", isRef: true),
            SimpleProp("Age", "int", isRef: false)
        );

        var result = DtoEmitter.Emit(node, copySourceAttributes: false, naming: DefaultNaming);

        Assert.That(result, Does.Contain("public partial class PersonDto : System.IEquatable<PersonDto>"));
        Assert.That(result, Does.Contain("[System.CodeDom.Compiler.GeneratedCode(\"DataNormalizer\""));
        Assert.That(result, Does.Contain("namespace TestApp;"));
        Assert.That(result, Does.Contain("public string Name { get; set; }"));
        Assert.That(result, Does.Contain("public int Age { get; set; }"));

        EmitterCompilationHelper.AssertCompiles(result);
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

        var result = DtoEmitter.Emit(node, copySourceAttributes: false, naming: DefaultNaming);

        Assert.That(result, Does.Contain("public int HomeAddressIndex { get; set; }"));
        Assert.That(result, Does.Not.Contain("public TestApp.Address"));

        EmitterCompilationHelper.AssertCompiles(result);
    }

    [Test]
    public void Emit_NullableNormalizedProperty_GeneratesNullableIndex()
    {
        var node = CreateNode(
            "TestApp.Person",
            "Person",
            NormalizedProp("WorkAddress", "TestApp.Address", nullable: true)
        );

        var result = DtoEmitter.Emit(node, copySourceAttributes: false, naming: DefaultNaming);

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

        var result = DtoEmitter.Emit(node, copySourceAttributes: false, naming: DefaultNaming);

        Assert.That(result, Does.Contain("public int[] PhoneNumbersIndices { get; set; }"));

        EmitterCompilationHelper.AssertCompiles(result);
    }

    [Test]
    public void Emit_InlinedProperty_KeepsOriginalType()
    {
        var node = CreateNode("TestApp.Person", "Person", InlinedProp("Meta", "TestApp.Metadata", isRef: true));

        var result = DtoEmitter.Emit(node, copySourceAttributes: false, naming: DefaultNaming);

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

        var result = DtoEmitter.Emit(node, copySourceAttributes: false, naming: DefaultNaming);

        Assert.That(result, Does.Contain("public string Name { get; set; }"));
        Assert.That(result, Does.Contain("public int Age { get; set; }"));
        Assert.That(result, Does.Contain("public int HomeAddressIndex { get; set; }"));
        Assert.That(result, Does.Contain("public int? WorkAddressIndex { get; set; }"));
        Assert.That(result, Does.Contain("public int[] PhoneNumbersIndices { get; set; }"));
        Assert.That(result, Does.Contain("public TestApp.Metadata Meta { get; set; }"));

        // Inlined type needs a stub for compilation
        var metadataStub = "namespace TestApp { public class Metadata { } }";
        EmitterCompilationHelper.AssertCompilesWithStubs(new[] { result }, new[] { metadataStub });
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

        var result = DtoEmitter.Emit(node, copySourceAttributes: false, naming: DefaultNaming);

        Assert.That(result, Does.Contain("public bool Equals(PersonDto? other)"));
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

        var result = DtoEmitter.Emit(node, copySourceAttributes: false, naming: DefaultNaming);

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

        var result = DtoEmitter.Emit(node, copySourceAttributes: false, naming: DefaultNaming);

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

        var result = DtoEmitter.Emit(node, copySourceAttributes: false, naming: DefaultNaming);

        // Array equality must use SequenceEqual, not ==
        Assert.That(result, Does.Contain("SequenceEqual"));
    }

    [Test]
    public void Emit_GeneratedCodeAttribute_Present()
    {
        var node = CreateNode("TestApp.Person", "Person", SimpleProp("Name", "string", isRef: true));

        var result = DtoEmitter.Emit(node, copySourceAttributes: false, naming: DefaultNaming);

        Assert.That(result, Does.Contain("[System.CodeDom.Compiler.GeneratedCode(\"DataNormalizer\""));
    }

    [Test]
    public void Emit_NullableEnable_Present()
    {
        var node = CreateNode("TestApp.Person", "Person", SimpleProp("Name", "string", isRef: true));

        var result = DtoEmitter.Emit(node, copySourceAttributes: false, naming: DefaultNaming);

        Assert.That(result, Does.Contain("#nullable enable"));
    }

    [Test]
    public void Emit_EnumProperty_SimpleType()
    {
        var node = CreateNode("TestApp.Order", "Order", SimpleProp("Status", "TestApp.OrderStatus", isRef: false));

        var result = DtoEmitter.Emit(node, copySourceAttributes: false, naming: DefaultNaming);

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

        var result = DtoEmitter.Emit(
            node,
            copySourceAttributes: true,
            naming: new NamingModel { EmitJsonPropertyNames = false }
        );

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

        var result = DtoEmitter.Emit(
            node,
            copySourceAttributes: true,
            naming: new NamingModel { EmitJsonPropertyNames = false }
        );

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

        var result = DtoEmitter.Emit(
            node,
            copySourceAttributes: false,
            naming: new NamingModel { EmitJsonPropertyNames = false }
        );

        Assert.That(result, Does.Not.Contain("[System.Text.Json.Serialization.JsonPropertyName(\"name\")]"));
        Assert.That(result, Does.Contain("public string Name { get; set; }"));
    }

    [Test]
    public void Emit_EmitJsonPropertyNames_True_EmitsJsonPropertyNameOnAllProperties()
    {
        var node = CreateNode(
            "TestApp.Person",
            "Person",
            SimpleProp("Name", "string", isRef: true),
            SimpleProp("Age", "int", isRef: false),
            NormalizedProp("HomeAddress", "TestApp.Address", nullable: false)
        );

        var result = DtoEmitter.Emit(
            node,
            copySourceAttributes: false,
            naming: new NamingModel { EmitJsonPropertyNames = true }
        );

        Assert.That(result, Does.Contain("[System.Text.Json.Serialization.JsonPropertyName(\"name\")]"));
        Assert.That(result, Does.Contain("[System.Text.Json.Serialization.JsonPropertyName(\"age\")]"));
        Assert.That(result, Does.Contain("[System.Text.Json.Serialization.JsonPropertyName(\"homeAddressIndex\")]"));
    }

    [Test]
    public void Emit_EmitJsonPropertyNames_True_CollectionProperty_UsesIndicesSuffix()
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

        var result = DtoEmitter.Emit(
            node,
            copySourceAttributes: false,
            naming: new NamingModel { EmitJsonPropertyNames = true }
        );

        Assert.That(result, Does.Contain("[System.Text.Json.Serialization.JsonPropertyName(\"phoneNumbersIndices\")]"));
    }

    [Test]
    public void Emit_EmitJsonPropertyNames_DoesNotOverrideExistingJsonPropertyName()
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

        var result = DtoEmitter.Emit(
            node,
            copySourceAttributes: true,
            naming: new NamingModel { EmitJsonPropertyNames = true }
        );

        // Should keep the explicit attribute, NOT add a generated one
        Assert.That(result, Does.Contain("[System.Text.Json.Serialization.JsonPropertyName(\"custom_name\")]"));
        Assert.That(result, Does.Not.Contain("JsonPropertyName(\"name\")"));
    }

    [Test]
    public void Emit_EmitJsonPropertyNames_False_DoesNotEmitJsonPropertyName()
    {
        var node = CreateNode("TestApp.Person", "Person", SimpleProp("Name", "string", isRef: true));

        var result = DtoEmitter.Emit(
            node,
            copySourceAttributes: false,
            naming: new NamingModel { EmitJsonPropertyNames = false }
        );

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

        var result = DtoEmitter.Emit(node, copySourceAttributes: false, naming: DefaultNaming);

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

        var result = DtoEmitter.Emit(node, copySourceAttributes: false, naming: DefaultNaming);

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

        var result = DtoEmitter.Emit(node, copySourceAttributes: false, naming: DefaultNaming);

        // With all properties circular, Equals should return true
        Assert.That(result, Does.Contain("return true;"));
        // GetHashCode should not reference NextIndex
        var hashSection = result.Substring(result.IndexOf("GetHashCode()"));
        Assert.That(hashSection, Does.Not.Contain("NextIndex"));

        EmitterCompilationHelper.AssertCompiles(result);
    }

    // ---- New NamingModel-specific tests ----

    [Test]
    public void Emit_EmitJsonPropertyNames_True_AllPropertiesHaveCamelCaseJsonAttributes()
    {
        var node = CreateNode(
            "TestApp.Person",
            "Person",
            SimpleProp("FirstName", "string", isRef: true),
            SimpleProp("Age", "int", isRef: false),
            NormalizedProp("HomeAddress", "TestApp.Address", nullable: false),
            CollectionProp(
                "PhoneNumbers",
                "System.Collections.Generic.List<TestApp.PhoneNumber>",
                "TestApp.PhoneNumber"
            ),
            InlinedProp("Meta", "TestApp.Metadata", isRef: true)
        );

        var result = DtoEmitter.Emit(node, copySourceAttributes: false, naming: DefaultNaming);

        Assert.That(result, Does.Contain("[System.Text.Json.Serialization.JsonPropertyName(\"firstName\")]"));
        Assert.That(result, Does.Contain("[System.Text.Json.Serialization.JsonPropertyName(\"age\")]"));
        Assert.That(result, Does.Contain("[System.Text.Json.Serialization.JsonPropertyName(\"homeAddressIndex\")]"));
        Assert.That(result, Does.Contain("[System.Text.Json.Serialization.JsonPropertyName(\"phoneNumbersIndices\")]"));
        Assert.That(result, Does.Contain("[System.Text.Json.Serialization.JsonPropertyName(\"meta\")]"));
    }

    [Test]
    public void Emit_EmitJsonPropertyNames_False_NoJsonPropertyNameAttributes()
    {
        var node = CreateNode(
            "TestApp.Person",
            "Person",
            SimpleProp("Name", "string", isRef: true),
            SimpleProp("Age", "int", isRef: false),
            NormalizedProp("HomeAddress", "TestApp.Address", nullable: false)
        );

        var result = DtoEmitter.Emit(
            node,
            copySourceAttributes: false,
            naming: new NamingModel { EmitJsonPropertyNames = false }
        );

        Assert.That(result, Does.Not.Contain("JsonPropertyName"));
    }

    [Test]
    public void Emit_CustomPrefixNoSuffix_ClassNamedMyPerson()
    {
        var node = CreateNode("TestApp.Person", "Person", SimpleProp("Name", "string", isRef: true));

        var naming = new NamingModel { DtoPrefix = "My", DtoSuffix = "" };
        var result = DtoEmitter.Emit(node, copySourceAttributes: false, naming: naming);

        Assert.That(result, Does.Contain("public partial class MyPerson : System.IEquatable<MyPerson>"));
        Assert.That(result, Does.Contain("public bool Equals(MyPerson? other)"));
        Assert.That(result, Does.Contain("obj is MyPerson other"));
    }

    [Test]
    public void Emit_NormalizedPrefix_ClassNamedNormalizedPerson()
    {
        var node = CreateNode("TestApp.Person", "Person", SimpleProp("Name", "string", isRef: true));

        var naming = new NamingModel { DtoPrefix = "Normalized", DtoSuffix = "" };
        var result = DtoEmitter.Emit(node, copySourceAttributes: false, naming: naming);

        Assert.That(
            result,
            Does.Contain("public partial class NormalizedPerson : System.IEquatable<NormalizedPerson>")
        );
    }

    // ---- JsonNameOverride tests ----

    [Test]
    public void Emit_NormalizedProperty_WithJsonNameOverride_EmitsOverrideOnIndexProperty()
    {
        var node = CreateNode(
            "TestApp.Person",
            "Person",
            new AnalyzedProperty
            {
                Name = "Line",
                TypeFullName = "TestApp.Line",
                Kind = PropertyKind.Normalized,
                IsReferenceType = true,
                JsonNameOverride = "line",
            }
        );

        var result = DtoEmitter.Emit(
            node,
            copySourceAttributes: false,
            naming: new NamingModel { EmitJsonPropertyNames = true }
        );

        Assert.That(result, Does.Contain("[System.Text.Json.Serialization.JsonPropertyName(\"line\")]"));
        Assert.That(result, Does.Not.Contain("JsonPropertyName(\"lineIndex\")"));

        EmitterCompilationHelper.AssertCompiles(result);
    }

    [Test]
    public void Emit_CollectionProperty_WithJsonNameOverride_EmitsOverrideOnIndicesArray()
    {
        var node = CreateNode(
            "TestApp.Person",
            "Person",
            new AnalyzedProperty
            {
                Name = "TransitImages",
                TypeFullName = "System.Collections.Generic.List<TestApp.Image>",
                Kind = PropertyKind.Collection,
                IsCollection = true,
                CollectionElementTypeFullName = "TestApp.Image",
                CollectionKind = CollectionTypeKind.List,
                IsReferenceType = true,
                JsonNameOverride = "transitImages",
            }
        );

        var result = DtoEmitter.Emit(
            node,
            copySourceAttributes: false,
            naming: new NamingModel { EmitJsonPropertyNames = true }
        );

        Assert.That(result, Does.Contain("[System.Text.Json.Serialization.JsonPropertyName(\"transitImages\")]"));
        Assert.That(result, Does.Not.Contain("JsonPropertyName(\"transitImagesIndices\")"));
    }

    [Test]
    public void Emit_SimpleProperty_WithJsonNameOverride_EmitsOverride()
    {
        var node = CreateNode(
            "TestApp.Person",
            "Person",
            new AnalyzedProperty
            {
                Name = "FullName",
                TypeFullName = "string",
                Kind = PropertyKind.Simple,
                IsReferenceType = true,
                JsonNameOverride = "name",
            }
        );

        var result = DtoEmitter.Emit(
            node,
            copySourceAttributes: false,
            naming: new NamingModel { EmitJsonPropertyNames = true }
        );

        Assert.That(result, Does.Contain("[System.Text.Json.Serialization.JsonPropertyName(\"name\")]"));
        Assert.That(result, Does.Not.Contain("JsonPropertyName(\"fullName\")"));
    }

    [Test]
    public void Emit_InlinedProperty_WithJsonNameOverride_EmitsOverride()
    {
        var node = CreateNode(
            "TestApp.Person",
            "Person",
            new AnalyzedProperty
            {
                Name = "Metadata",
                TypeFullName = "TestApp.Metadata",
                Kind = PropertyKind.Inlined,
                IsReferenceType = true,
                JsonNameOverride = "details",
            }
        );

        var result = DtoEmitter.Emit(
            node,
            copySourceAttributes: false,
            naming: new NamingModel { EmitJsonPropertyNames = true }
        );

        Assert.That(result, Does.Contain("[System.Text.Json.Serialization.JsonPropertyName(\"details\")]"));
        Assert.That(result, Does.Not.Contain("JsonPropertyName(\"metadata\")"));
    }

    [Test]
    public void Emit_PropertyWithNoOverride_UsesDefaultCamelCase()
    {
        var node = CreateNode("TestApp.Person", "Person", NormalizedProp("Line", "TestApp.Line", nullable: false));

        var result = DtoEmitter.Emit(
            node,
            copySourceAttributes: false,
            naming: new NamingModel { EmitJsonPropertyNames = true }
        );

        Assert.That(result, Does.Contain("[System.Text.Json.Serialization.JsonPropertyName(\"lineIndex\")]"));
    }

    [Test]
    public void Emit_JsonNameOverride_WhenEmitJsonPropertyNamesFalse_StillEmitsOverride()
    {
        var node = CreateNode(
            "TestApp.Person",
            "Person",
            new AnalyzedProperty
            {
                Name = "Line",
                TypeFullName = "TestApp.Line",
                Kind = PropertyKind.Normalized,
                IsReferenceType = true,
                JsonNameOverride = "line",
            }
        );

        var result = DtoEmitter.Emit(
            node,
            copySourceAttributes: false,
            naming: new NamingModel { EmitJsonPropertyNames = false }
        );

        // Override is explicit — should still emit even when EmitJsonPropertyNames is false
        Assert.That(result, Does.Contain("[System.Text.Json.Serialization.JsonPropertyName(\"line\")]"));
    }

    [Test]
    public void Emit_MixedProperties_SomeWithOverridesSomeWithout_CorrectAttributes()
    {
        var node = CreateNode(
            "TestApp.Person",
            "Person",
            new AnalyzedProperty
            {
                Name = "Line",
                TypeFullName = "TestApp.Line",
                Kind = PropertyKind.Normalized,
                IsReferenceType = true,
                JsonNameOverride = "line",
            },
            SimpleProp("Age", "int", isRef: false),
            new AnalyzedProperty
            {
                Name = "Items",
                TypeFullName = "System.Collections.Generic.List<TestApp.Item>",
                Kind = PropertyKind.Collection,
                IsCollection = true,
                CollectionElementTypeFullName = "TestApp.Item",
                CollectionKind = CollectionTypeKind.List,
                IsReferenceType = true,
                JsonNameOverride = "items",
            }
        );

        var result = DtoEmitter.Emit(
            node,
            copySourceAttributes: false,
            naming: new NamingModel { EmitJsonPropertyNames = true }
        );

        // Override properties use the override value
        Assert.That(result, Does.Contain("JsonPropertyName(\"line\")"));
        Assert.That(result, Does.Contain("JsonPropertyName(\"items\")"));
        // Non-override property uses default camelCase
        Assert.That(result, Does.Contain("JsonPropertyName(\"age\")"));
    }

    [Test]
    public void Emit_CopySourceAttributes_WithExistingJsonPropertyNameAndOverride_EmitsOnlyOverride()
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
                    "[System.Text.Json.Serialization.JsonPropertyName(\"original\")]"
                ),
                JsonNameOverride = "override",
            }
        );

        var result = DtoEmitter.Emit(
            node,
            copySourceAttributes: true,
            naming: new NamingModel { EmitJsonPropertyNames = true }
        );

        // Should emit ONLY the override, not the source attribute's JsonPropertyName
        Assert.That(result, Does.Contain("JsonPropertyName(\"override\")"));
        Assert.That(result, Does.Not.Contain("JsonPropertyName(\"original\")"));
        // Should appear exactly once
        Assert.That(CountOccurrences(result, "JsonPropertyName"), Is.EqualTo(1));
    }

    [Test]
    public void Emit_EmptyStringJsonNameOverride_TreatedAsNull_DefaultBehavior()
    {
        var node = CreateNode(
            "TestApp.Person",
            "Person",
            new AnalyzedProperty
            {
                Name = "Line",
                TypeFullName = "TestApp.Line",
                Kind = PropertyKind.Normalized,
                IsReferenceType = true,
                JsonNameOverride = "",
            }
        );

        var result = DtoEmitter.Emit(
            node,
            copySourceAttributes: false,
            naming: new NamingModel { EmitJsonPropertyNames = true }
        );

        // Empty string treated as null — default camelCase behavior
        Assert.That(result, Does.Contain("JsonPropertyName(\"lineIndex\")"));
    }

    [Test]
    public void Emit_JsonNameOverride_DoesNotChangeCSharpPropertyName()
    {
        var node = CreateNode(
            "TestApp.Person",
            "Person",
            new AnalyzedProperty
            {
                Name = "Line",
                TypeFullName = "TestApp.Line",
                Kind = PropertyKind.Normalized,
                IsReferenceType = true,
                JsonNameOverride = "customLine",
            }
        );

        var result = DtoEmitter.Emit(
            node,
            copySourceAttributes: false,
            naming: new NamingModel { EmitJsonPropertyNames = true }
        );

        // C# property name should still be LineIndex, not customLine
        Assert.That(result, Does.Contain("public int LineIndex { get; set; }"));
        Assert.That(result, Does.Contain("JsonPropertyName(\"customLine\")"));
    }

    [Test]
    public void CompilationHelper_MalformedCode_FailsCompilation()
    {
        var malformed = """
            namespace TestApp;
            public class Broken
            {
                public UndefinedType Foo { get; set; }
            }
            """;

        EmitterCompilationHelper.AssertDoesNotCompile(malformed);
    }

    private static int CountOccurrences(string text, string pattern)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(pattern, index, System.StringComparison.Ordinal)) != -1)
        {
            count++;
            index += pattern.Length;
        }
        return count;
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
