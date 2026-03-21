using System.Collections.Generic;
using System.Collections.Immutable;
using DataNormalizer.Generators.Emitters;
using DataNormalizer.Generators.Models;
using NUnit.Framework;

namespace DataNormalizer.Generators.Tests.Emitters;

[TestFixture]
public sealed class NormalizerEmitterTests
{
    [Test]
    public void Emit_SimpleFlatType_GeneratesNormalizeMethodAndHelper()
    {
        var model = CreateModel("TestConfig", "TestApp", "TestApp.Person");
        var nodes = new[]
        {
            CreateNode(
                "TestApp.Person",
                "Person",
                SimpleProp("Name", "string", isRef: true),
                SimpleProp("Age", "int", isRef: false)
            ),
        };

        var result = NormalizerEmitter.Emit(model, nodes);

        // Public Normalize method returns container type
        Assert.That(
            result,
            Does.Contain("public static TestApp.NormalizedPersonResult Normalize(TestApp.Person source)")
        );
        // Populates entity lists on container
        Assert.That(result, Does.Contain("result.RootIndex = rootIndex;"));
        Assert.That(result, Does.Contain("result.PersonList = "));
        Assert.That(result, Does.Contain("return result;"));
        // Helper method
        Assert.That(
            result,
            Does.Contain(
                "private static int NormalizePerson(TestApp.Person source, DataNormalizer.Runtime.NormalizationContext context)"
            )
        );
        // Simple property assignments
        Assert.That(result, Does.Contain("dto.Name = source.Name;"));
        Assert.That(result, Does.Contain("dto.Age = source.Age;"));
        // No nested normalize calls
        Assert.That(result, Does.Not.Contain("NormalizeAddress"));
    }

    [Test]
    public void Emit_NestedNormalizableType_GeneratesTwoHelpers()
    {
        var model = CreateModel("TestConfig", "TestApp", "TestApp.Person");
        var nodes = new[]
        {
            CreateNode(
                "TestApp.Person",
                "Person",
                SimpleProp("Name", "string", isRef: true),
                NormalizedProp("HomeAddress", "TestApp.Address", nullable: false)
            ),
            CreateNode(
                "TestApp.Address",
                "Address",
                SimpleProp("Street", "string", isRef: true),
                SimpleProp("City", "string", isRef: true)
            ),
        };

        var result = NormalizerEmitter.Emit(model, nodes);

        // Person helper calls NormalizeAddress
        Assert.That(result, Does.Contain("dto.HomeAddressIndex = NormalizeAddress(source.HomeAddress, context);"));
        // Address helper exists
        Assert.That(
            result,
            Does.Contain(
                "private static int NormalizeAddress(TestApp.Address source, DataNormalizer.Runtime.NormalizationContext context)"
            )
        );
        // Address helper assigns simple props
        Assert.That(result, Does.Contain("dto.Street = source.Street;"));
        Assert.That(result, Does.Contain("dto.City = source.City;"));
        // Public method populates entity lists for ALL types in graph
        Assert.That(result, Does.Contain("result.PersonList = "));
        Assert.That(result, Does.Contain("result.AddressList = "));
    }

    [Test]
    public void Emit_NullableNestedProperty_GeneratesNullCheck()
    {
        var model = CreateModel("TestConfig", "TestApp", "TestApp.Person");
        var nodes = new[]
        {
            CreateNode("TestApp.Person", "Person", NormalizedProp("WorkAddress", "TestApp.Address", nullable: true)),
            CreateNode("TestApp.Address", "Address", SimpleProp("City", "string", isRef: true)),
        };

        var result = NormalizerEmitter.Emit(model, nodes);

        Assert.That(
            result,
            Does.Contain(
                "dto.WorkAddressIndex = source.WorkAddress is null ? (int?)null : NormalizeAddress(source.WorkAddress, context);"
            )
        );
    }

    [Test]
    public void Emit_CollectionOfNormalizableType_GeneratesForLoop()
    {
        var model = CreateModel("TestConfig", "TestApp", "TestApp.Person");
        var nodes = new[]
        {
            CreateNode(
                "TestApp.Person",
                "Person",
                CollectionProp(
                    "PhoneNumbers",
                    "System.Collections.Generic.List<TestApp.PhoneNumber>",
                    "TestApp.PhoneNumber"
                )
            ),
            CreateNode("TestApp.PhoneNumber", "PhoneNumber", SimpleProp("Number", "string", isRef: true)),
        };

        var result = NormalizerEmitter.Emit(model, nodes);

        // Should use manual for-loop instead of LINQ
        Assert.That(result, Does.Not.Contain("System.Linq.Enumerable"));
        Assert.That(result, Does.Contain("var __col = source.PhoneNumbers;"));
        Assert.That(result, Does.Contain("var __indices = new int[__col.Count];"));
        Assert.That(result, Does.Contain("for (var __i = 0; __i < __col.Count; __i++)"));
        Assert.That(result, Does.Contain("__indices[__i] = NormalizePhoneNumber(__col[__i], context);"));
        Assert.That(result, Does.Contain("dto.PhoneNumbersIndices = __indices;"));
    }

    [Test]
    public void Emit_NullCollectionProperty_GeneratesNullSafeHandling()
    {
        var model = CreateModel("TestConfig", "TestApp", "TestApp.Person");
        var nodes = new[]
        {
            CreateNode(
                "TestApp.Person",
                "Person",
                CollectionProp(
                    "PhoneNumbers",
                    "System.Collections.Generic.List<TestApp.PhoneNumber>",
                    "TestApp.PhoneNumber"
                )
            ),
            CreateNode("TestApp.PhoneNumber", "PhoneNumber", SimpleProp("Number", "string", isRef: true)),
        };

        var result = NormalizerEmitter.Emit(model, nodes);

        // Null-safe: checks for null before selecting
        Assert.That(result, Does.Contain("source.PhoneNumbers is null"));
        Assert.That(result, Does.Contain("System.Array.Empty<int>()"));
    }

    [Test]
    public void Emit_CircularReference_GeneratesTwoDtoPattern()
    {
        var model = CreateModel("TestConfig", "TestApp", "TestApp.TreeNode");
        var nodes = new[]
        {
            CreateNode(
                "TestApp.TreeNode",
                "TreeNode",
                hasCircularReference: true,
                SimpleProp("Label", "string", isRef: true),
                NormalizedProp("Parent", "TestApp.TreeNode", nullable: true, isCircular: true),
                CollectionProp(
                    "Children",
                    "System.Collections.Generic.List<TestApp.TreeNode>",
                    "TestApp.TreeNode",
                    isCircular: true
                )
            ),
        };

        var result = NormalizerEmitter.Emit(model, nodes);

        // Should use two-DTO pattern: keyDto (partial) and fullDto (complete)
        Assert.That(result, Does.Contain("var keyDto = new"));
        Assert.That(result, Does.Contain("var fullDto = new"));
        // Should use GetOrAddIndexAndStore BEFORE recursion for value equality dedup + cycle detection
        Assert.That(result, Does.Contain("context.GetOrAddIndexAndStore"));
        Assert.That(result, Does.Contain("if (!isNew)"));
        Assert.That(result, Does.Contain("return index;"));
        // Should have AddToCollection for the fullDto overwrite
        Assert.That(result, Does.Contain("context.AddToCollection"));
        // Should NOT have old source-identity-based patterns
        Assert.That(result, Does.Not.Contain("MarkVisited"));
        Assert.That(result, Does.Not.Contain("IsVisited"));
        Assert.That(result, Does.Not.Contain("TryGetSourceIndex"));
        Assert.That(result, Does.Not.Contain("SetSourceIndex"));
        Assert.That(result, Does.Not.Contain("lookupDto"));
    }

    [Test]
    public void Emit_CircularReference_RegistersEarlyBeforeRecursion()
    {
        var model = CreateModel("TestConfig", "TestApp", "TestApp.TreeNode");
        var nodes = new[]
        {
            CreateNode(
                "TestApp.TreeNode",
                "TreeNode",
                hasCircularReference: true,
                SimpleProp("Label", "string", isRef: true),
                NormalizedProp("Parent", "TestApp.TreeNode", nullable: true, isCircular: true),
                CollectionProp(
                    "Children",
                    "System.Collections.Generic.List<TestApp.TreeNode>",
                    "TestApp.TreeNode",
                    isCircular: true
                )
            ),
        };

        var result = NormalizerEmitter.Emit(model, nodes);

        // Verify the order: keyDto simple props → GetOrAddIndexAndStore → recursion → fullDto → AddToCollection (overwrite)
        var keyDtoIndex = result.IndexOf("var keyDto = new", System.StringComparison.Ordinal);
        var getOrAddIndex = result.IndexOf("GetOrAddIndexAndStore", System.StringComparison.Ordinal);
        var normalizeTreeNodeCall = result.IndexOf("NormalizeTreeNode(source.Parent", System.StringComparison.Ordinal);
        var fullDtoIndex = result.IndexOf("var fullDto = new", System.StringComparison.Ordinal);

        // keyDto should appear first
        Assert.That(keyDtoIndex, Is.GreaterThan(-1), "Should have keyDto creation");

        // GetOrAddIndexAndStore (registration + placeholder storage) should appear BEFORE the recursive NormalizeTreeNode call
        Assert.That(getOrAddIndex, Is.GreaterThan(-1), "Should have GetOrAddIndexAndStore registration");
        Assert.That(normalizeTreeNodeCall, Is.GreaterThan(-1), "Should have recursive NormalizeTreeNode call");
        Assert.That(
            getOrAddIndex,
            Is.LessThan(normalizeTreeNodeCall),
            "For circular types, GetOrAddIndexAndStore must happen before recursive property assignments"
        );

        // Simple properties should be set on keyDto BEFORE GetOrAddIndexAndStore
        var simplePropAssignment = result.IndexOf("keyDto.Label = source.Label;", System.StringComparison.Ordinal);
        Assert.That(
            simplePropAssignment,
            Is.LessThan(getOrAddIndex),
            "Simple properties must be set on keyDto before early registration"
        );

        // GetOrAddIndexAndStore handles placeholder storage, so no separate AddToCollection before recursion needed

        // fullDto should appear AFTER the recursive call
        Assert.That(fullDtoIndex, Is.GreaterThan(-1), "Should have fullDto creation");
        Assert.That(
            fullDtoIndex,
            Is.GreaterThan(normalizeTreeNodeCall),
            "fullDto must be created after recursive property assignments"
        );

        // AddToCollection (overwrite) should appear AFTER fullDto
        var addToCollection = result.IndexOf("AddToCollection(\"TreeNode\"", System.StringComparison.Ordinal);
        Assert.That(addToCollection, Is.GreaterThan(-1), "Should have AddToCollection for overwrite");
        Assert.That(
            addToCollection,
            Is.GreaterThan(fullDtoIndex),
            "AddToCollection (overwrite) must happen after fullDto creation"
        );
    }

    [Test]
    public void Emit_MultiLevelNesting_GeneratesThreeHelpers()
    {
        var model = CreateModel("TestConfig", "TestApp", "TestApp.A");
        var nodes = new[]
        {
            CreateNode(
                "TestApp.A",
                "A",
                SimpleProp("Value", "string", isRef: true),
                NormalizedProp("B", "TestApp.B", nullable: false)
            ),
            CreateNode(
                "TestApp.B",
                "B",
                SimpleProp("Value", "string", isRef: true),
                NormalizedProp("C", "TestApp.C", nullable: false)
            ),
            CreateNode("TestApp.C", "C", SimpleProp("Value", "string", isRef: true)),
        };

        var result = NormalizerEmitter.Emit(model, nodes);

        // Three helpers
        Assert.That(result, Does.Contain("private static int NormalizeA("));
        Assert.That(result, Does.Contain("private static int NormalizeB("));
        Assert.That(result, Does.Contain("private static int NormalizeC("));
        // Call chain: A→B→C
        Assert.That(result, Does.Contain("dto.BIndex = NormalizeB(source.B, context);"));
        Assert.That(result, Does.Contain("dto.CIndex = NormalizeC(source.C, context);"));
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

        var result = NormalizerEmitter.Emit(model, new[] { personNode, treeNode });

        // Should have TWO public Normalize methods (overloaded by parameter type)
        Assert.That(result, Does.Contain("Normalize(TestApp.Person source)"));
        Assert.That(result, Does.Contain("Normalize(TestApp.TreeNode source)"));
        // Each returns its own container type
        Assert.That(
            result,
            Does.Contain("public static TestApp.NormalizedPersonResult Normalize(TestApp.Person source)")
        );
        Assert.That(
            result,
            Does.Contain("public static TestApp.NormalizedTreeNodeResult Normalize(TestApp.TreeNode source)")
        );
    }

    [Test]
    public void Emit_SingleRootType_StillGeneratesSingleNormalizeMethod()
    {
        var model = CreateModel("TestConfig", "TestApp", "TestApp.Person");
        var nodes = new[] { CreateNode("TestApp.Person", "Person", SimpleProp("Name", "string", isRef: true)) };

        var result = NormalizerEmitter.Emit(model, nodes);

        Assert.That(result, Does.Contain("Normalize(TestApp.Person source)"));
        // Should only have one Normalize method
        var count = CountOccurrences(result, "public static TestApp.NormalizedPersonResult Normalize(");
        Assert.That(count, Is.EqualTo(1));
    }

    [Test]
    public void Emit_ConfigClassInfo_UsesNamespaceAndClassName()
    {
        var model = CreateModel("MyNormalizer", "MyApp.Config", "MyApp.Config.Entity");
        var nodes = new[] { CreateNode("MyApp.Config.Entity", "Entity", SimpleProp("Id", "int", isRef: false)) };

        var result = NormalizerEmitter.Emit(model, nodes);

        Assert.That(result, Does.Contain("namespace MyApp.Config;"));
        Assert.That(result, Does.Contain("public partial class MyNormalizer"));
    }

    [Test]
    public void Emit_PublicNormalizeMethodSignature_HasCorrectReturnTypeAndParameter()
    {
        var model = CreateModel("TestConfig", "TestApp", "TestApp.Order");
        var nodes = new[] { CreateNode("TestApp.Order", "Order", SimpleProp("Total", "decimal", isRef: false)) };

        var result = NormalizerEmitter.Emit(model, nodes);

        // Returns container type, not NormalizedResult<T>
        Assert.That(
            result,
            Does.Contain("public static TestApp.NormalizedOrderResult Normalize(TestApp.Order source)")
        );
        Assert.That(result, Does.Contain("var context = new DataNormalizer.Runtime.NormalizationContext(1);"));
        Assert.That(result, Does.Contain("var rootIndex = NormalizeOrder(source, context);"));
        // Creates container and populates it
        Assert.That(result, Does.Contain("var result = new TestApp.NormalizedOrderResult();"));
        Assert.That(result, Does.Contain("result.RootIndex = rootIndex;"));
        // Entity list population
        Assert.That(result, Does.Contain("context.GetCollection<TestApp.NormalizedOrder>(\"Order\")"));
        Assert.That(result, Does.Contain("result.OrderList = "));
        Assert.That(result, Does.Contain("return result;"));
        // Should NOT have old NormalizedResult pattern
        Assert.That(result, Does.Not.Contain("NormalizedResult<"));
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

        var result = NormalizerEmitter.Emit(model, new[] { personNode });

        Assert.That(result, Does.Contain("GetOrAddIndexAndStore(\"People\""));
        // Custom key is used in GetCollection lookup in public method
        Assert.That(result, Does.Contain("GetCollection<TestApp.NormalizedPerson>(\"People\")"));
        // But property name on container still uses TypeName, not custom key
        Assert.That(result, Does.Contain("result.PersonList = "));
    }

    [Test]
    public void Emit_CircularType_NormalizesNonCircularPropsBeforeGetOrAddIndex()
    {
        // Employee with Mentor (circular) and Department (non-circular)
        var deptNode = new TypeGraphNode
        {
            TypeFullName = "TestApp.Department",
            TypeName = "Department",
            Properties = ImmutableArray.Create(
                new AnalyzedProperty
                {
                    Name = "Name",
                    TypeFullName = "string",
                    Kind = PropertyKind.Simple,
                    IsReferenceType = true,
                }
            ),
        };
        var empNode = new TypeGraphNode
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

        var model = new NormalizationModel
        {
            ConfigClassName = "TestConfig",
            ConfigNamespace = "TestApp",
            RootTypes = ImmutableArray.Create(
                new RootTypeInfo { TypeSymbol = null!, FullyQualifiedName = "TestApp.Employee" }
            ),
            AutoDiscover = true,
        };

        var result = NormalizerEmitter.Emit(model, new[] { deptNode, empNode });

        // Non-circular Department should be normalized BEFORE GetOrAddIndexAndStore
        var normalizeDeptPos = result.IndexOf("NormalizeDepartment(source.Department", System.StringComparison.Ordinal);
        var getOrAddPos = result.IndexOf("GetOrAddIndexAndStore(\"Employee\"", System.StringComparison.Ordinal);
        Assert.That(normalizeDeptPos, Is.GreaterThan(-1), "Should normalize non-circular Department");
        Assert.That(getOrAddPos, Is.GreaterThan(-1));
        Assert.That(
            normalizeDeptPos,
            Is.LessThan(getOrAddPos),
            "Non-circular Department normalization should happen BEFORE GetOrAddIndexAndStore"
        );

        // Circular Mentor should be normalized AFTER GetOrAddIndexAndStore
        var normalizeMentorPos = result.IndexOf("NormalizeEmployee(source.Mentor", System.StringComparison.Ordinal);
        Assert.That(
            normalizeMentorPos,
            Is.GreaterThan(getOrAddPos),
            "Circular Mentor normalization should happen AFTER GetOrAddIndexAndStore"
        );

        // Circular Mentor should be left at defaults on keyDto (excluded from equality)
        Assert.That(result, Does.Not.Contain("source.Mentor is null ? (int?)null : (int?)0"));

        // keyDto should have real normalization for Department (on keyDto, before GetOrAddIndexAndStore)
        Assert.That(result, Does.Contain("keyDto.DepartmentIndex = NormalizeDepartment(source.Department, context);"));

        // fullDto should reuse keyDto values for non-circular Department
        Assert.That(result, Does.Contain("fullDto.DepartmentIndex = keyDto.DepartmentIndex;"));

        // fullDto should use local variable for circular Mentor
        Assert.That(result, Does.Contain("fullDto.MentorIndex = mentorIndex;"));
    }

    [Test]
    public void Emit_CircularType_WithNonCircularCollection_NormalizesCollectionBeforeGetOrAddIndex()
    {
        // Employee with Mentor (circular) and Certifications (non-circular collection)
        var certNode = CreateNode("TestApp.Certification", "Certification", SimpleProp("Title", "string", isRef: true));
        var empNode = CreateNode(
            "TestApp.Employee",
            "Employee",
            hasCircularReference: true,
            SimpleProp("Name", "string", isRef: true),
            NormalizedProp("Mentor", "TestApp.Employee", nullable: true, isCircular: true),
            CollectionProp(
                "Certifications",
                "System.Collections.Generic.List<TestApp.Certification>",
                "TestApp.Certification",
                isCircular: false
            )
        );

        var model = CreateModel("TestConfig", "TestApp", "TestApp.Employee");
        var result = NormalizerEmitter.Emit(model, new[] { certNode, empNode });

        // Non-circular Certifications collection should be normalized BEFORE GetOrAddIndexAndStore
        var normalizeCertPos = result.IndexOf(
            "NormalizeCertification(__col[__i], context)",
            System.StringComparison.Ordinal
        );
        var getOrAddPos = result.IndexOf("GetOrAddIndexAndStore(\"Employee\"", System.StringComparison.Ordinal);
        Assert.That(normalizeCertPos, Is.GreaterThan(-1), "Should normalize non-circular Certifications");
        Assert.That(getOrAddPos, Is.GreaterThan(-1));
        Assert.That(
            normalizeCertPos,
            Is.LessThan(getOrAddPos),
            "Non-circular Certifications normalization should happen BEFORE GetOrAddIndexAndStore"
        );

        // keyDto should have real collection indices for non-circular Certifications
        Assert.That(result, Does.Contain("keyDto.CertificationsIndices = __indices;"));

        // fullDto should reuse keyDto values for non-circular Certifications
        Assert.That(result, Does.Contain("fullDto.CertificationsIndices = keyDto.CertificationsIndices;"));
    }

    [Test]
    public void Emit_CircularType_CircularCollectionLeftAtDefaults()
    {
        var empNode = CreateNode(
            "TestApp.Employee",
            "Employee",
            hasCircularReference: true,
            SimpleProp("Name", "string", isRef: true),
            CollectionProp(
                "Friends",
                "System.Collections.Generic.List<TestApp.Employee>",
                "TestApp.Employee",
                isCircular: true
            )
        );

        var model = CreateModel("TestConfig", "TestApp", "TestApp.Employee");
        var result = NormalizerEmitter.Emit(model, new[] { empNode });

        // Circular collection should NOT have shape encoding on keyDto (excluded from equality)
        Assert.That(result, Does.Not.Contain("keyDto.FriendsIndices"));
        Assert.That(result, Does.Not.Contain("new int[source.Friends.Count]"));

        // Real normalization should happen AFTER GetOrAddIndexAndStore
        var getOrAddPos = result.IndexOf("GetOrAddIndexAndStore(\"Employee\"", System.StringComparison.Ordinal);
        var normalizeCallPos = result.IndexOf(
            "NormalizeEmployee(__col[__i], context)",
            System.StringComparison.Ordinal
        );
        Assert.That(normalizeCallPos, Is.GreaterThan(-1));
        Assert.That(
            normalizeCallPos,
            Is.GreaterThan(getOrAddPos),
            "Circular Friends normalization should happen AFTER GetOrAddIndexAndStore"
        );

        // fullDto should use local variable for circular Friends
        Assert.That(result, Does.Contain("fullDto.FriendsIndices = friendsIndices;"));
    }

    [Test]
    public void Emit_UseReferenceTrackingForCycles_EmitsTrackSourceAndTryGetTrackedIndex()
    {
        var treeNode = CreateNode(
            "TestApp.TreeNode",
            "TreeNode",
            hasCircularReference: true,
            SimpleProp("Label", "string", isRef: true),
            NormalizedProp("Parent", "TestApp.TreeNode", nullable: true, isCircular: true)
        );

        var model = new NormalizationModel
        {
            ConfigClassName = "TestConfig",
            ConfigNamespace = "TestApp",
            RootTypes = ImmutableArray.Create(
                new RootTypeInfo { TypeSymbol = null!, FullyQualifiedName = "TestApp.TreeNode" }
            ),
            UseReferenceTrackingForCycles = true,
            AutoDiscover = true,
        };

        var result = NormalizerEmitter.Emit(model, new[] { treeNode });

        Assert.That(result, Does.Contain("TryGetTrackedIndex(source"));
        Assert.That(result, Does.Contain("TrackSource(source, index)"));
    }

    [Test]
    public void Emit_DefaultMode_NoReferenceTracking()
    {
        var treeNode = CreateNode(
            "TestApp.TreeNode",
            "TreeNode",
            hasCircularReference: true,
            SimpleProp("Label", "string", isRef: true),
            NormalizedProp("Parent", "TestApp.TreeNode", nullable: true, isCircular: true)
        );

        var model = new NormalizationModel
        {
            ConfigClassName = "TestConfig",
            ConfigNamespace = "TestApp",
            RootTypes = ImmutableArray.Create(
                new RootTypeInfo { TypeSymbol = null!, FullyQualifiedName = "TestApp.TreeNode" }
            ),
            UseReferenceTrackingForCycles = false,
            AutoDiscover = true,
        };

        var result = NormalizerEmitter.Emit(model, new[] { treeNode });

        Assert.That(result, Does.Not.Contain("TryGetTrackedIndex"));
        Assert.That(result, Does.Not.Contain("TrackSource"));
    }

    // ---- Helpers ----

    private static NormalizationModel CreateModel(
        string configClassName,
        string configNamespace,
        string rootTypeFullyQualifiedName
    )
    {
        var rootTypeName = rootTypeFullyQualifiedName.Substring(rootTypeFullyQualifiedName.LastIndexOf('.') + 1);
        return new NormalizationModel
        {
            ConfigClassName = configClassName,
            ConfigNamespace = configNamespace,
            RootTypes = ImmutableArray.Create(
                new RootTypeInfo { TypeSymbol = null!, FullyQualifiedName = rootTypeFullyQualifiedName }
            ),
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
            CollectionKind = CollectionTypeKind.List,
            IsReferenceType = true,
            IsCircularReference = isCircular,
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
