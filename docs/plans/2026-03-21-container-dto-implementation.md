# Container DTO Implementation Plan

> **For Agent:** REQUIRED SUB-SKILL: Use executing-plans to implement this plan task-by-task.

**Goal:** Replace `NormalizedResult<TRoot>` with a generated container DTO that holds both the root's normalized properties and `{TypeName}List` arrays for all types in the normalization graph.

**Architecture:** New `ContainerEmitter` generates the container class. `NormalizerEmitter` and `DenormalizerEmitter` are modified to use the container as input/output. Root type is no longer emitted as a per-type DTO — its normalization is inlined in the public `Normalize()` method. `NormalizedResult<TRoot>` is deleted.

**Tech Stack:** C# 12, Roslyn incremental source generators, NUnit 4, CSharpier formatting.

**Design doc:** `docs/plans/2026-03-21-container-dto-design.md`

**Skills:** @source-generator-dev @dotnet-tdd @csharpier @dotnet-architecture

---

### Task 1: Create ContainerEmitter with tests

**Files:**
- Create: `src/DataNormalizer.Generators/Emitters/ContainerEmitter.cs`
- Test: `tests/DataNormalizer.Generators.Tests/Emitters/ContainerEmitterTests.cs`

The ContainerEmitter generates a class with:
- Root type's properties normalized (same rules as DtoEmitter properties but NO IEquatable)
- `{TypeName}List` array properties for each non-root type in the graph
- `[GeneratedCode]` attribute
- `partial class` modifier
- JSON naming attributes if configured

**Step 1: Write the failing test**

Create `tests/DataNormalizer.Generators.Tests/Emitters/ContainerEmitterTests.cs`:

```csharp
using DataNormalizer.Generators.Emitters;
using DataNormalizer.Generators.Models;
using System.Collections.Immutable;

namespace DataNormalizer.Generators.Tests.Emitters;

[TestFixture]
public sealed class ContainerEmitterTests
{
    [Test]
    public void SimpleRoot_GeneratesContainerWithRootPropertiesAndEntityLists()
    {
        // Root: PeopleDto { Person[] People }
        // Graph: Person { string Name, int HomeIndex }, Address { string City }
        var rootNode = new TypeGraphNode
        {
            TypeFullName = "TestApp.PeopleDto",
            TypeName = "PeopleDto",
            HasCircularReference = false,
            Properties = ImmutableArray.Create(
                new AnalyzedProperty
                {
                    Name = "People",
                    TypeFullName = "TestApp.Person",
                    Kind = PropertyKind.Collection,
                    IsNullable = false,
                    IsCollection = true,
                    CollectionElementTypeFullName = "TestApp.Person",
                    CollectionKind = CollectionTypeKind.Array,
                    IsCircularReference = false,
                    IsReferenceType = true,
                    SourceAttributes = ImmutableArray<string>.Empty,
                }
            ),
        };

        var personNode = new TypeGraphNode
        {
            TypeFullName = "TestApp.Person",
            TypeName = "Person",
            HasCircularReference = false,
            Properties = ImmutableArray.Create(
                new AnalyzedProperty
                {
                    Name = "Name",
                    TypeFullName = "System.String",
                    Kind = PropertyKind.Simple,
                    IsNullable = false,
                    IsCollection = false,
                    CollectionKind = CollectionTypeKind.None,
                    IsCircularReference = false,
                    IsReferenceType = true,
                    SourceAttributes = ImmutableArray<string>.Empty,
                },
                new AnalyzedProperty
                {
                    Name = "Home",
                    TypeFullName = "TestApp.Address",
                    Kind = PropertyKind.Normalized,
                    IsNullable = false,
                    IsCollection = false,
                    CollectionKind = CollectionTypeKind.None,
                    IsCircularReference = false,
                    IsReferenceType = true,
                    SourceAttributes = ImmutableArray<string>.Empty,
                }
            ),
        };

        var addressNode = new TypeGraphNode
        {
            TypeFullName = "TestApp.Address",
            TypeName = "Address",
            HasCircularReference = false,
            Properties = ImmutableArray.Create(
                new AnalyzedProperty
                {
                    Name = "City",
                    TypeFullName = "System.String",
                    Kind = PropertyKind.Simple,
                    IsNullable = false,
                    IsCollection = false,
                    CollectionKind = CollectionTypeKind.None,
                    IsCircularReference = false,
                    IsReferenceType = true,
                    SourceAttributes = ImmutableArray<string>.Empty,
                }
            ),
        };

        var nonRootNodes = new[] { personNode, addressNode };

        var result = ContainerEmitter.Emit(rootNode, nonRootNodes, jsonNamingPolicy: null);

        // Container class
        Assert.That(result, Does.Contain("public partial class NormalizedPeopleDto"));
        Assert.That(result, Does.Contain("[System.CodeDom.Compiler.GeneratedCode("));
        // Should NOT have IEquatable
        Assert.That(result, Does.Not.Contain("IEquatable"));

        // Root's collection property → int[] Indices
        Assert.That(result, Does.Contain("public int[] PeopleIndices { get; set; } = System.Array.Empty<int>();"));

        // Entity list properties
        Assert.That(result, Does.Contain("public TestApp.NormalizedPerson[] PersonList { get; set; } = System.Array.Empty<TestApp.NormalizedPerson>();"));
        Assert.That(result, Does.Contain("public TestApp.NormalizedAddress[] AddressList { get; set; } = System.Array.Empty<TestApp.NormalizedAddress>();"));
    }

    [Test]
    public void RootWithSimpleProperty_EmitsSimplePropertyDirectly()
    {
        var rootNode = new TypeGraphNode
        {
            TypeFullName = "TestApp.OrderDto",
            TypeName = "OrderDto",
            HasCircularReference = false,
            Properties = ImmutableArray.Create(
                new AnalyzedProperty
                {
                    Name = "OrderNumber",
                    TypeFullName = "System.String",
                    Kind = PropertyKind.Simple,
                    IsNullable = false,
                    IsCollection = false,
                    CollectionKind = CollectionTypeKind.None,
                    IsCircularReference = false,
                    IsReferenceType = true,
                    SourceAttributes = ImmutableArray<string>.Empty,
                }
            ),
        };

        var result = ContainerEmitter.Emit(rootNode, nonRootNodes: [], jsonNamingPolicy: null);

        Assert.That(result, Does.Contain("public string OrderNumber { get; set; } = default!;"));
    }

    [Test]
    public void RootWithNullableNormalizedProperty_EmitsNullableIndex()
    {
        var rootNode = new TypeGraphNode
        {
            TypeFullName = "TestApp.OrderDto",
            TypeName = "OrderDto",
            HasCircularReference = false,
            Properties = ImmutableArray.Create(
                new AnalyzedProperty
                {
                    Name = "ShippingAddress",
                    TypeFullName = "TestApp.Address",
                    Kind = PropertyKind.Normalized,
                    IsNullable = true,
                    IsCollection = false,
                    CollectionKind = CollectionTypeKind.None,
                    IsCircularReference = false,
                    IsReferenceType = true,
                    SourceAttributes = ImmutableArray<string>.Empty,
                }
            ),
        };

        var addressNode = new TypeGraphNode
        {
            TypeFullName = "TestApp.Address",
            TypeName = "Address",
            HasCircularReference = false,
            Properties = ImmutableArray.Create(
                new AnalyzedProperty
                {
                    Name = "City",
                    TypeFullName = "System.String",
                    Kind = PropertyKind.Simple,
                    IsNullable = false,
                    IsCollection = false,
                    CollectionKind = CollectionTypeKind.None,
                    IsCircularReference = false,
                    IsReferenceType = true,
                    SourceAttributes = ImmutableArray<string>.Empty,
                }
            ),
        };

        var result = ContainerEmitter.Emit(rootNode, [addressNode], jsonNamingPolicy: null);

        Assert.That(result, Does.Contain("public int? ShippingAddressIndex { get; set; }"));
    }

    [Test]
    public void JsonNamingPolicy_EmitsJsonPropertyNameAttributes()
    {
        var rootNode = new TypeGraphNode
        {
            TypeFullName = "TestApp.PeopleDto",
            TypeName = "PeopleDto",
            HasCircularReference = false,
            Properties = ImmutableArray.Create(
                new AnalyzedProperty
                {
                    Name = "People",
                    TypeFullName = "TestApp.Person",
                    Kind = PropertyKind.Collection,
                    IsNullable = false,
                    IsCollection = true,
                    CollectionElementTypeFullName = "TestApp.Person",
                    CollectionKind = CollectionTypeKind.Array,
                    IsCircularReference = false,
                    IsReferenceType = true,
                    SourceAttributes = ImmutableArray<string>.Empty,
                }
            ),
        };

        var personNode = new TypeGraphNode
        {
            TypeFullName = "TestApp.Person",
            TypeName = "Person",
            HasCircularReference = false,
            Properties = ImmutableArray<AnalyzedProperty>.Empty,
        };

        var result = ContainerEmitter.Emit(rootNode, [personNode], jsonNamingPolicy: "CamelCase");

        Assert.That(result, Does.Contain("[System.Text.Json.Serialization.JsonPropertyName(\"peopleIndices\")]"));
        Assert.That(result, Does.Contain("[System.Text.Json.Serialization.JsonPropertyName(\"personList\")]"));
    }

    [Test]
    public void Namespace_EmitsCorrectNamespace()
    {
        var rootNode = new TypeGraphNode
        {
            TypeFullName = "TestApp.Models.PeopleDto",
            TypeName = "PeopleDto",
            HasCircularReference = false,
            Properties = ImmutableArray<AnalyzedProperty>.Empty,
        };

        var result = ContainerEmitter.Emit(rootNode, nonRootNodes: [], jsonNamingPolicy: null);

        Assert.That(result, Does.Contain("namespace TestApp.Models;"));
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test tests/DataNormalizer.Generators.Tests/ --filter "FullyQualifiedName~ContainerEmitterTests" -v minimal`
Expected: Compile error — `ContainerEmitter` doesn't exist yet.

**Step 3: Implement ContainerEmitter**

Create `src/DataNormalizer.Generators/Emitters/ContainerEmitter.cs`:

```csharp
using System.Collections.Generic;
using System.Text;
using DataNormalizer.Generators.Models;

namespace DataNormalizer.Generators.Emitters;

internal static class ContainerEmitter
{
    public static string Emit(
        TypeGraphNode rootNode,
        IReadOnlyList<TypeGraphNode> nonRootNodes,
        string? jsonNamingPolicy
    )
    {
        var sb = new StringBuilder();
        var ns = EmitterHelpers.GetNamespace(rootNode.TypeFullName);
        var containerName = $"Normalized{rootNode.TypeName}";

        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();

        if (!string.IsNullOrEmpty(ns))
        {
            sb.AppendLine($"namespace {ns};");
            sb.AppendLine();
        }

        sb.AppendLine(
            $"[System.CodeDom.Compiler.GeneratedCode(\"{EmitterHelpers.GeneratorName}\", \"{EmitterHelpers.GeneratorVersion}\")]"
        );
        sb.AppendLine($"public partial class {containerName}");
        sb.AppendLine("{");

        // Root's own properties (normalized)
        foreach (var prop in rootNode.Properties)
        {
            EmitRootProperty(sb, prop, jsonNamingPolicy);
        }

        // Entity list properties for each non-root type
        foreach (var node in nonRootNodes)
        {
            if (node.Properties.Length == 0)
                continue;
            EmitEntityListProperty(sb, node, jsonNamingPolicy);
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    private static void EmitRootProperty(StringBuilder sb, AnalyzedProperty prop, string? jsonNamingPolicy)
    {
        // Same mapping rules as DtoEmitter but no IEquatable participation
        switch (prop.Kind)
        {
            case PropertyKind.Simple:
            case PropertyKind.Inlined:
                EmitJsonNamingAttribute(sb, prop.Name, jsonNamingPolicy);
                var simpleType = GetCSharpTypeName(prop.TypeFullName, prop.IsNullable, prop.IsReferenceType);
                var simpleDefault = prop.IsReferenceType ? " = default!;" : "";
                sb.AppendLine($"    public {simpleType} {prop.Name} {{ get; set; }}{simpleDefault}");
                break;

            case PropertyKind.Normalized:
                var indexPropName = $"{prop.Name}Index";
                EmitJsonNamingAttribute(sb, indexPropName, jsonNamingPolicy);
                if (prop.IsNullable)
                    sb.AppendLine($"    public int? {indexPropName} {{ get; set; }}");
                else
                    sb.AppendLine($"    public int {indexPropName} {{ get; set; }}");
                break;

            case PropertyKind.Collection:
                var indicesPropName = $"{prop.Name}Indices";
                EmitJsonNamingAttribute(sb, indicesPropName, jsonNamingPolicy);
                sb.AppendLine(
                    $"    public int[] {indicesPropName} {{ get; set; }} = System.Array.Empty<int>();"
                );
                break;
        }
    }

    private static void EmitEntityListProperty(
        StringBuilder sb,
        TypeGraphNode node,
        string? jsonNamingPolicy
    )
    {
        var dtoFullName = EmitterHelpers.GetDtoFullName(node.TypeFullName, node.TypeName);
        var listPropName = $"{node.TypeName}List";

        EmitJsonNamingAttribute(sb, listPropName, jsonNamingPolicy);
        sb.AppendLine(
            $"    public {dtoFullName}[] {listPropName} {{ get; set; }} = System.Array.Empty<{dtoFullName}>();"
        );
    }

    private static void EmitJsonNamingAttribute(StringBuilder sb, string propName, string? jsonNamingPolicy)
    {
        if (jsonNamingPolicy == EmitterHelpers.CamelCasePolicy)
        {
            var camel = EmitterHelpers.ToCamelCase(propName);
            sb.AppendLine(
                $"    [System.Text.Json.Serialization.JsonPropertyName(\"{camel}\")]"
            );
        }
    }

    private static string GetCSharpTypeName(string typeFullName, bool isNullable, bool isReferenceType)
    {
        var baseName = typeFullName;
        if (isNullable && !isReferenceType)
            return baseName + "?";
        return baseName;
    }
}
```

**Step 4: Run tests to verify they pass**

Run: `dotnet test tests/DataNormalizer.Generators.Tests/ --filter "FullyQualifiedName~ContainerEmitterTests" -v minimal`
Expected: All PASS.

**Step 5: Run CSharpier**

Run: `dotnet csharpier format .`

**Step 6: Commit**

```bash
git add src/DataNormalizer.Generators/Emitters/ContainerEmitter.cs tests/DataNormalizer.Generators.Tests/Emitters/ContainerEmitterTests.cs
git commit -m "feat: add ContainerEmitter for generating container DTO classes"
```

---

### Task 2: Modify NormalizerEmitter to return container

**Files:**
- Modify: `src/DataNormalizer.Generators/Emitters/NormalizerEmitter.cs:57-79` (EmitPublicNormalizeMethod)
- Modify: `tests/DataNormalizer.Generators.Tests/Emitters/NormalizerEmitterTests.cs`

The public `Normalize()` method changes from returning `NormalizedResult<TRoot>` to returning the container DTO. Root property normalization is inlined (no `NormalizeRootType()` helper). Entity lists are populated from context.

**Step 1: Update NormalizerEmitterTests**

In `tests/DataNormalizer.Generators.Tests/Emitters/NormalizerEmitterTests.cs`, update assertions that check for `NormalizedResult<>` in the generated code. Change them to check for the container return type and inline root normalization pattern.

Key assertion changes:
- `NormalizedResult<TestApp.NormalizedPerson>` → `TestApp.NormalizedPerson` (return type is the container DTO itself — note: when root IS the entity, the container name matches)
- `return new DataNormalizer.Runtime.NormalizedResult<...>(root, rootIndex, context)` → `return result;` (return the built container)
- Check for entity list population: `result.PersonList = ...` or similar
- The root type should NOT have a `NormalizeRootType()` helper emitted — its normalization is inline

For multi-root tests: each root gets its own container type and `Normalize()` method.

**Step 2: Run tests to verify they fail**

Run: `dotnet test tests/DataNormalizer.Generators.Tests/ --filter "FullyQualifiedName~NormalizerEmitterTests" -v minimal`
Expected: FAIL — assertions still expect old NormalizedResult patterns.

**Step 3: Modify EmitPublicNormalizeMethod**

Replace `NormalizerEmitter.EmitPublicNormalizeMethod()` (lines 57-79) to:
1. Declare return type as `{containerFullName}` (the container DTO) instead of `NormalizedResult<...>`
2. Create NormalizationContext as before
3. Inline root property normalization:
   - For each root property, use the same patterns as EmitPropertyAssignment but targeting `result.{prop}` instead of `dto.{prop}`
   - Simple/Inlined: `result.Name = source.Name;`
   - Normalized: `result.{Name}Index = Normalize{Type}(source.{Name}, context);`
   - Collection: for-loop building `int[]`, assigning to `result.{Name}Indices`
4. Populate entity lists from context:
   - For each non-root node: `var __{camel}Col = context.GetCollection<{DtoFullName}>("{typeKey}"); var __{camel}Arr = new {DtoFullName}[__{camel}Col.Count]; for (var __i = 0; __i < __{camel}Col.Count; __i++) __{camel}Arr[__i] = __{camel}Col[__i]; result.{TypeName}List = __{camel}Arr;`
5. Return `result`

Also modify `Emit()` to **skip** emitting a `NormalizeXxx` helper for root type nodes. Add a `rootTypeFullNames` set parameter or check against `model.RootTypes` when iterating `allNodes`.

**Step 4: Run tests to verify they pass**

Run: `dotnet test tests/DataNormalizer.Generators.Tests/ --filter "FullyQualifiedName~NormalizerEmitterTests" -v minimal`
Expected: All PASS.

**Step 5: Run CSharpier and commit**

```bash
dotnet csharpier format .
git add -u && git commit -m "feat: NormalizerEmitter returns container DTO instead of NormalizedResult"
```

---

### Task 3: Modify DenormalizerEmitter to accept container

**Files:**
- Modify: `src/DataNormalizer.Generators/Emitters/DenormalizerEmitter.cs:46-76` (EmitDenormalizeMethod) + `78-92` (EmitGetCollections) + `334-339` (EmitRootResolution)
- Modify: `tests/DataNormalizer.Generators.Tests/Emitters/DenormalizerEmitterTests.cs`

The `Denormalize()` method changes from accepting `NormalizedResult<TRoot>` to accepting the container DTO. Collections are read from container's typed array properties. Root reconstruction is added.

**Step 1: Update DenormalizerEmitterTests**

Change assertions:
- Parameter type: `NormalizedResult<TestApp.NormalizedPerson>` → `TestApp.NormalizedPerson` (container type)
- Collection retrieval: `result.GetCollection<...>("...")` → `normalized.{TypeName}List` (direct array access)
- Root resolution: `return {plural}[result.RootIndex]` → reconstruct root from container's index properties

**Step 2: Run tests to verify they fail**

Run: `dotnet test tests/DataNormalizer.Generators.Tests/ --filter "FullyQualifiedName~DenormalizerEmitterTests" -v minimal`
Expected: FAIL.

**Step 3: Modify DenormalizerEmitter**

Key changes to `EmitDenormalizeMethod()`:
1. Parameter: `{containerFullName} normalized` instead of `NormalizedResult<{rootDtoFullName}> result`
2. `EmitGetCollections()`: instead of `result.GetCollection<...>("...")`, emit `var {camel}Dtos = normalized.{TypeName}List;` for each non-root node
3. Pass 1 and Pass 2: unchanged (they iterate `{camel}Dtos` which now comes from container properties)
4. `EmitRootResolution()`: reconstruct root object from container's root properties:
   - Simple/Inlined: `root.{Name} = normalized.{Name};`
   - Normalized: `root.{Name} = {targetPlural}[normalized.{Name}Index];` (with nullable check)
   - Collection: for-loop from `normalized.{Name}Indices` into source collection

**Step 4: Run tests to verify they pass**

Run: `dotnet test tests/DataNormalizer.Generators.Tests/ --filter "FullyQualifiedName~DenormalizerEmitterTests" -v minimal`
Expected: All PASS.

**Step 5: Run CSharpier and commit**

```bash
dotnet csharpier format .
git add -u && git commit -m "feat: DenormalizerEmitter accepts container DTO instead of NormalizedResult"
```

---

### Task 4: Update NormalizeGenerator pipeline

**Files:**
- Modify: `src/DataNormalizer.Generators/NormalizeGenerator.cs:69-143`

**Step 1: Write failing E2E test or update existing ones**

Update `tests/DataNormalizer.Generators.Tests/GeneratorEndToEndTests.cs` to check that:
- Container DTO is generated (one per root type)
- Per-type DTOs are generated for non-root types only
- Root type does NOT get a separate per-type DTO

**Step 2: Run to verify failure**

Run: `dotnet test tests/DataNormalizer.Generators.Tests/ --filter "FullyQualifiedName~GeneratorEndToEndTests" -v minimal`
Expected: FAIL.

**Step 3: Modify NormalizeGenerator.TransformConfig()**

Changes needed:
1. Build a `HashSet<string>` of root type full names from `model.RootTypes`
2. When emitting DTOs (line 114-129), **skip** nodes whose `TypeFullName` is in the root types set
3. After DTOs, emit container for each root type:
   ```csharp
   foreach (var rootType in model.RootTypes)
   {
       var rootNode = allNodes.FirstOrDefault(n => n.TypeFullName == rootType.FullyQualifiedName);
       if (rootNode == null || rootNode.Properties.Length == 0)
           continue;
       var nonRootNodes = allNodes.Where(n => n.TypeFullName != rootType.FullyQualifiedName).ToList();
       var containerSource = ContainerEmitter.Emit(rootNode, nonRootNodes, model.JsonNamingPolicy);
       // Use similar hint name pattern
       sources.Add(new GeneratorSourceEntry(containerHintName, containerSource));
   }
   ```
4. Pass `allNodes` and root type info to `NormalizerEmitter.Emit()` and `DenormalizerEmitter.Emit()` — they need to know which nodes are root types to skip helpers/collections for them

**Step 4: Run tests**

Run: `dotnet test tests/DataNormalizer.Generators.Tests/ -v minimal`
Expected: All PASS (E2E + emitter tests).

**Step 5: Run CSharpier and commit**

```bash
dotnet csharpier format .
git add -u && git commit -m "feat: wire ContainerEmitter into generator pipeline, skip root DTOs"
```

---

### Task 5: Delete NormalizedResult and clean up runtime

**Files:**
- Delete: `src/DataNormalizer/Runtime/NormalizedResult.cs`
- Delete: `tests/DataNormalizer.Tests/Runtime/NormalizedResultTests.cs`
- Modify: `src/DataNormalizer/Runtime/NormalizationContext.cs` — remove `CollectionNames` property (no longer needed in public API, only used by NormalizedResult), optionally remove `GetCollection<T>()` overload that uses `typeof(T).Name`

**Step 1: Delete files**

```bash
rm src/DataNormalizer/Runtime/NormalizedResult.cs
rm tests/DataNormalizer.Tests/Runtime/NormalizedResultTests.cs
```

**Step 2: Verify runtime library builds**

Run: `dotnet build src/DataNormalizer/`
Expected: PASS (no references to NormalizedResult from runtime code).

**Step 3: Verify generator tests still pass**

Run: `dotnet test tests/DataNormalizer.Generators.Tests/ -v minimal`
Expected: PASS (generator tests don't reference runtime NormalizedResult directly — they test generated source strings).

**Step 4: Simplify NormalizationContext**

The `CastingReadOnlyList<T>` inner class and `GetCollection<T>()` methods are still needed by the generated normalizer code (it calls `context.GetCollection<T>(typeKey)` to populate the container). Keep them. Remove only `CollectionNames` if desired.

**Step 5: Run CSharpier and commit**

```bash
dotnet csharpier format .
git add -u && git commit -m "refactor: remove NormalizedResult<TRoot> and related tests"
```

---

### Task 6: Update integration tests

**Files:**
- Modify: `tests/DataNormalizer.Integration.Tests/SimpleNormalizationTests.cs`
- Modify: `tests/DataNormalizer.Integration.Tests/BasicRoundtripTests.cs`
- Modify: `tests/DataNormalizer.Integration.Tests/CircularReferenceTests.cs`
- Modify: `tests/DataNormalizer.Integration.Tests/ConfigFeatureTests.cs`
- Modify: `tests/DataNormalizer.Integration.Tests/DeepNestingTests.cs`
- Modify: `tests/DataNormalizer.Integration.Tests/PerformanceTests.cs`
- Modify: `tests/DataNormalizer.Integration.Tests/SmokeTests.cs`

Integration tests call the generated `Normalize()` method and inspect the result. All need updating from `NormalizedResult<T>` patterns to container patterns.

**Step 1: Read all integration test files to understand current patterns**

Use the explore agent to read every integration test file and catalog the patterns used.

**Step 2: Update each test file**

Common pattern changes:

Before:
```csharp
var result = TestConfig.Normalize(source);
Assert.That(result.Root.NameIndex, Is.EqualTo(0));
var addresses = result.GetCollection<NormalizedAddress>("Address");
Assert.That(addresses.Count, Is.EqualTo(1));
```

After:
```csharp
var result = TestConfig.Normalize(source);
// Root properties are on the container directly
Assert.That(result.NameIndex, Is.EqualTo(0));
// Entity lists are typed array properties
Assert.That(result.AddressList.Length, Is.EqualTo(1));
```

For roundtrip tests:
```csharp
// Before:
var denormalized = TestConfig.Denormalize(result);
// After (unchanged — Denormalize still takes the container and returns the original type):
var denormalized = TestConfig.Denormalize(result);
```

**Important:** The integration test configs may need updating too. If the config has `NormalizeGraph<Person>()` where Person is both root and entity, the container will be `NormalizedPerson` (same name as the per-type DTO would have been). Ensure no name collisions. If the root type IS the entity type (e.g., `NormalizeGraph<Person>()` and Person has nested Address), then Person is both root and entity — the container replaces the per-type DTO.

**Step 3: Run integration tests**

Run: `dotnet test tests/DataNormalizer.Integration.Tests/ -v minimal`
Expected: All PASS.

**Step 4: Run CSharpier and commit**

```bash
dotnet csharpier format .
git add -u && git commit -m "test: update integration tests for container DTO output"
```

---

### Task 7: Update samples

**Files:**
- Modify: `samples/DataNormalizer.Samples/` (read first to understand current usage)

**Step 1: Read current sample code**

**Step 2: Update to use container pattern**

Show the new usage:
```csharp
var container = SampleConfig.Normalize(source);
Console.WriteLine($"People: {container.PersonList.Length}");
Console.WriteLine($"Addresses: {container.AddressList.Length}");

// Serialize to JSON
var json = System.Text.Json.JsonSerializer.Serialize(container,
    new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
Console.WriteLine(json);

// Roundtrip
var original = SampleConfig.Denormalize(container);
```

**Step 3: Run sample**

Run: `dotnet run --project samples/DataNormalizer.Samples/`
Expected: Outputs normalized JSON showing the container structure.

**Step 4: Run CSharpier and commit**

```bash
dotnet csharpier format .
git add -u && git commit -m "docs: update samples for container DTO output"
```

---

### Task 8: Full build, format, and verify

**Step 1: Full solution build**

Run: `dotnet build DataNormalizer.sln`
Expected: 0 errors, 0 warnings (or only DN0001 circular ref warnings from test configs).

**Step 2: Run all tests**

Run: `dotnet test DataNormalizer.sln -v minimal`
Expected: All PASS.

**Step 3: Run CSharpier check**

Run: `dotnet csharpier check .`
Expected: No formatting issues.

**Step 4: Final commit if any remaining changes**

```bash
git add -u && git commit -m "chore: final cleanup after container DTO migration"
```
