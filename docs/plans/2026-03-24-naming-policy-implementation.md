# Naming Policy System Implementation Plan

> **For Agent:** REQUIRED SUB-SKILL: Use executing-plans to implement this plan task-by-task.

**Goal:** Add a configurable naming policy system so generated DTOs, containers, and JSON property names can be customized, with new defaults (Dto suffix, always-on JsonPropertyName).

**Architecture:** New `NamingBuilder` and `JsonContractBuilder` config classes in the runtime library (stubs -- parsed by the source generator). New `NamingModel`/`JsonContractModel` equatable records in the generator's Models layer. `ConfigurationParser` extended to parse the new syntax. All four emitters updated to consult the naming model instead of hardcoded conventions.

**Tech Stack:** C# 12, .NET source generators (Roslyn), NUnit 4, System.Text.Json

---

### Task 1: Add NamingModel and JsonContractModel to generator Models

**Files:**
- Create: `src/DataNormalizer.Generators/Models/NamingModel.cs`
- Create: `src/DataNormalizer.Generators/Models/JsonContractModel.cs`
- Modify: `src/DataNormalizer.Generators/Models/NormalizationModel.cs`
- Modify: `src/DataNormalizer.Generators/Models/AnalyzedProperty.cs`

**Step 1: Create NamingModel**

```csharp
// src/DataNormalizer.Generators/Models/NamingModel.cs
using System;

namespace DataNormalizer.Generators.Models;

internal sealed class NamingModel : IEquatable<NamingModel>
{
    public string DtoPrefix { get; init; } = "";
    public string DtoSuffix { get; init; } = "Dto";
    public string ContainerSuffix { get; init; } = "Dto";
    public bool EmitJsonPropertyNames { get; init; } = true;

    public bool Equals(NamingModel? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return DtoPrefix == other.DtoPrefix
            && DtoSuffix == other.DtoSuffix
            && ContainerSuffix == other.ContainerSuffix
            && EmitJsonPropertyNames == other.EmitJsonPropertyNames;
    }

    public override bool Equals(object? obj) => obj is NamingModel other && Equals(other);

    public override int GetHashCode()
    {
        var hash = 17;
        hash = (hash * 397) ^ (DtoPrefix?.GetHashCode() ?? 0);
        hash = (hash * 397) ^ (DtoSuffix?.GetHashCode() ?? 0);
        hash = (hash * 397) ^ (ContainerSuffix?.GetHashCode() ?? 0);
        hash = (hash * 397) ^ EmitJsonPropertyNames.GetHashCode();
        return hash;
    }

    public static NamingModel Default { get; } = new();
}
```

**Step 2: Create JsonContractModel**

```csharp
// src/DataNormalizer.Generators/Models/JsonContractModel.cs
using System;
using System.Collections.Immutable;

namespace DataNormalizer.Generators.Models;

internal sealed class JsonContractModel : IEquatable<JsonContractModel>
{
    public string? RootPropertyName { get; init; }
    public ImmutableDictionary<string, string> CollectionJsonNames { get; init; } =
        ImmutableDictionary<string, string>.Empty;

    public bool Equals(JsonContractModel? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        if (RootPropertyName != other.RootPropertyName) return false;
        if (CollectionJsonNames.Count != other.CollectionJsonNames.Count) return false;
        foreach (var kvp in CollectionJsonNames)
        {
            if (!other.CollectionJsonNames.TryGetValue(kvp.Key, out var otherVal) || kvp.Value != otherVal)
                return false;
        }
        return true;
    }

    public override bool Equals(object? obj) => obj is JsonContractModel other && Equals(other);

    public override int GetHashCode()
    {
        var hash = 17;
        hash = (hash * 397) ^ (RootPropertyName?.GetHashCode() ?? 0);
        hash = (hash * 397) ^ CollectionJsonNames.Count;
        return hash;
    }

    public static JsonContractModel Default { get; } = new();
}
```

**Step 3: Add NamingModel and JsonContractModel to NormalizationModel**

In `src/DataNormalizer.Generators/Models/NormalizationModel.cs`, add:

```csharp
public NamingModel Naming { get; init; } = NamingModel.Default;
public JsonContractModel JsonContract { get; init; } = JsonContractModel.Default;
```

**Step 4: Add JsonNameOverride to AnalyzedProperty**

In `src/DataNormalizer.Generators/Models/AnalyzedProperty.cs`, add:

```csharp
public string? JsonNameOverride { get; init; }
```

**Step 5: Run tests to confirm no regressions**

Run: `dotnet test tests/DataNormalizer.Generators.Tests/ --no-restore -v q`
Expected: All existing tests still pass (new fields have defaults matching old behavior).

**Step 6: Commit**

```
feat: add NamingModel, JsonContractModel, and JsonNameOverride to generator models
```

---

### Task 2: Add runtime configuration classes (NamingBuilder, JsonContractBuilder, ReferenceBuilder, NormalizeJsonNameAttribute)

**Files:**
- Create: `src/DataNormalizer/Configuration/NamingBuilder.cs`
- Create: `src/DataNormalizer/Configuration/JsonContractBuilder.cs`
- Create: `src/DataNormalizer/Configuration/ReferenceBuilder.cs`
- Create: `src/DataNormalizer/Attributes/NormalizeJsonNameAttribute.cs`
- Modify: `src/DataNormalizer/Configuration/NormalizeBuilder.cs`
- Modify: `src/DataNormalizer/Configuration/GraphBuilder.cs`
- Modify: `src/DataNormalizer/Configuration/TypeBuilder.cs`

**Step 1: Create NamingBuilder**

```csharp
// src/DataNormalizer/Configuration/NamingBuilder.cs
namespace DataNormalizer.Configuration;

/// <summary>
/// Configures naming conventions for generated DTOs and containers.
/// </summary>
public sealed class NamingBuilder
{
    /// <summary>
    /// Prefix for generated DTO type names. Default: "" (no prefix).
    /// Example: "" + "Person" + "Dto" = "PersonDto"
    /// </summary>
    public string DtoPrefix { get; set; } = "";

    /// <summary>
    /// Suffix for generated DTO type names. Default: "Dto".
    /// Example: "" + "Person" + "Dto" = "PersonDto"
    /// </summary>
    public string DtoSuffix { get; set; } = "Dto";

    /// <summary>
    /// Suffix for the generated container type name. Default: "Dto".
    /// Example: "PersonResult" + "Dto" = "PersonResultDto"
    /// </summary>
    public string ContainerSuffix { get; set; } = "Dto";

    /// <summary>
    /// Whether to emit [JsonPropertyName] attributes on all generated properties.
    /// Default: true.
    /// </summary>
    public bool EmitJsonPropertyNames { get; set; } = true;
}
```

**Step 2: Create JsonContractBuilder**

```csharp
// src/DataNormalizer/Configuration/JsonContractBuilder.cs
namespace DataNormalizer.Configuration;

/// <summary>
/// Configures JSON contract names for a normalized graph's container properties.
/// </summary>
public sealed class JsonContractBuilder
{
    /// <summary>
    /// Sets the JSON property name for the root object in the container.
    /// </summary>
    public string? RootPropertyName { get; set; }

    /// <summary>
    /// Sets the JSON property name for a type's collection in the container.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="jsonName">The JSON property name for the collection.</param>
    public void Collection<T>(string jsonName) { }
}
```

**Step 3: Create ReferenceBuilder**

```csharp
// src/DataNormalizer/Configuration/ReferenceBuilder.cs
namespace DataNormalizer.Configuration;

/// <summary>
/// Configures a reference property's JSON serialization name.
/// </summary>
public sealed class ReferenceBuilder
{
    /// <summary>
    /// Sets the JSON property name for this reference in the generated DTO.
    /// </summary>
    /// <param name="jsonName">The JSON property name.</param>
    /// <returns>This builder instance for chaining.</returns>
    public ReferenceBuilder JsonName(string jsonName) => this;
}
```

**Step 4: Create NormalizeJsonNameAttribute**

```csharp
// src/DataNormalizer/Attributes/NormalizeJsonNameAttribute.cs
namespace DataNormalizer.Attributes;

/// <summary>
/// Specifies a custom JSON property name for this property in the generated DTO.
/// When applied, the generated DTO property will have a [JsonPropertyName] attribute
/// with the specified name, overriding default naming conventions.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class NormalizeJsonNameAttribute : Attribute
{
    /// <summary>
    /// The JSON property name to use.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Creates a new NormalizeJsonNameAttribute with the specified JSON property name.
    /// </summary>
    /// <param name="name">The JSON property name.</param>
    public NormalizeJsonNameAttribute(string name) => Name = name;
}
```

**Step 5: Add UseNaming to NormalizeBuilder**

In `src/DataNormalizer/Configuration/NormalizeBuilder.cs`, add:

```csharp
/// <summary>
/// Configures global naming conventions for all generated types.
/// </summary>
/// <param name="configure">An action to configure naming options.</param>
/// <returns>This builder instance for chaining.</returns>
public NormalizeBuilder UseNaming(Action<NamingBuilder> configure)
{
    configure(new NamingBuilder());
    return this;
}
```

**Step 6: Add UseNaming and UseJsonContract to GraphBuilder**

In `src/DataNormalizer/Configuration/GraphBuilder.cs`, add:

```csharp
/// <summary>
/// Configures naming conventions for this graph, overriding global defaults.
/// </summary>
/// <param name="configure">An action to configure naming options.</param>
/// <returns>This builder instance for chaining.</returns>
public GraphBuilder<T> UseNaming(Action<NamingBuilder> configure)
{
    configure(new NamingBuilder());
    return this;
}

/// <summary>
/// Configures JSON contract names for the container's collection properties.
/// </summary>
/// <param name="configure">An action to configure JSON contract options.</param>
/// <returns>This builder instance for chaining.</returns>
public GraphBuilder<T> UseJsonContract(Action<JsonContractBuilder> configure)
{
    configure(new JsonContractBuilder());
    return this;
}
```

**Step 7: Add Reference and ReferenceCollection to TypeBuilder**

In `src/DataNormalizer/Configuration/TypeBuilder.cs`, add:

```csharp
/// <summary>
/// Configures a scalar reference property's JSON name.
/// </summary>
/// <param name="selector">Expression selecting the reference property.</param>
/// <returns>A <see cref="ReferenceBuilder"/> for further configuration.</returns>
public ReferenceBuilder Reference(Expression<Func<T, object?>> selector) => new();

/// <summary>
/// Configures a collection reference property's JSON name.
/// </summary>
/// <param name="selector">Expression selecting the collection property.</param>
/// <returns>A <see cref="ReferenceBuilder"/> for further configuration.</returns>
public ReferenceBuilder ReferenceCollection(Expression<Func<T, object?>> selector) => new();
```

**Step 8: Run tests to confirm no regressions**

Run: `dotnet test --no-restore -v q`
Expected: All existing tests still pass.

**Step 9: Commit**

```
feat: add NamingBuilder, JsonContractBuilder, ReferenceBuilder, and NormalizeJsonNameAttribute
```

---

### Task 3: Extend ConfigurationParser to parse new config syntax

**Files:**
- Modify: `src/DataNormalizer.Generators/Analysis/ConfigurationParser.cs`
- Test: `tests/DataNormalizer.Generators.Tests/Analysis/ConfigurationParserTests.cs`

**Step 1: Write failing tests for UseNaming parsing**

Add tests to `ConfigurationParserTests.cs` that verify:
- `builder.UseNaming(n => { n.DtoSuffix = "Dto"; n.DtoPrefix = ""; n.ContainerSuffix = "Dto"; n.EmitJsonPropertyNames = true; })` is parsed into the NamingModel
- `graph.UseNaming(n => { n.DtoSuffix = "Normalized"; })` is parsed as graph-level override
- Default NamingModel when no UseNaming is present

**Step 2: Run tests to verify they fail**

Run: `dotnet test tests/DataNormalizer.Generators.Tests/ --filter "ClassName~ConfigurationParser" -v q`
Expected: FAIL

**Step 3: Add ReceiverKind entries for NamingBuilder, JsonContractBuilder, and ReferenceBuilder**

In `ConfigurationParser.cs`, extend `ReceiverKind` enum:

```csharp
private enum ReceiverKind
{
    NormalizeBuilder,
    GraphBuilder,
    TypeBuilder,
    NamingBuilder,
    JsonContractBuilder,
    ReferenceBuilder,
}
```

**Step 4: Add naming fields to ParseContext**

```csharp
public NamingModel GlobalNaming { get; set; } = NamingModel.Default;
public NamingModel? GraphNaming { get; set; }
public string? RootPropertyName { get; set; }
public Dictionary<string, string> CollectionJsonNames { get; } = new();
public Dictionary<string, string> PropertyJsonNames { get; } = new(); // key: "TypeFqn.PropName"
public string? CurrentReferenceBuilderKey { get; set; } // tracks which property Reference()/ReferenceCollection() was called on
```

**Step 5: Add case handlers in AnalyzeInvocation for the new methods**

Handle these new cases in the switch:
- `"UseNaming"` on `NormalizeBuilder` → process lambda, set `GlobalNaming`
- `"UseNaming"` on `GraphBuilder` → process lambda, set `GraphNaming`
- `"UseJsonContract"` on `GraphBuilder` → process lambda, set contract config
- `"Collection"` on `JsonContractBuilder` → extract type arg FQN + string arg → store in `CollectionJsonNames`
- `"Reference"` on `TypeBuilder` → extract property name, set `CurrentReferenceBuilderKey`, return `ReferenceBuilder`
- `"ReferenceCollection"` on `TypeBuilder` → same as Reference
- `"JsonName"` on `ReferenceBuilder` → extract string arg, store in `PropertyJsonNames`
- Property assignments on `NamingBuilder` (e.g., `n.DtoSuffix = "Dto"`) → parse assignment statements

**Step 6: Parse NamingBuilder property assignments**

NamingBuilder lambdas contain assignment statements like `n.DtoSuffix = "Dto"`. Add a handler that recognizes `ExpressionStatementSyntax` with `AssignmentExpressionSyntax` where the left side is a `MemberAccessExpression` on a known `NamingBuilder` receiver.

```csharp
case "DtoPrefix" when receiverKind == ReceiverKind.NamingBuilder:
case "DtoSuffix" when receiverKind == ReceiverKind.NamingBuilder:
case "ContainerSuffix" when receiverKind == ReceiverKind.NamingBuilder:
case "EmitJsonPropertyNames" when receiverKind == ReceiverKind.NamingBuilder:
```

Parse these by extracting the right-hand side literal value and updating the appropriate naming field on context.

**Step 7: Wire NamingModel and JsonContractModel into the returned NormalizationModel**

At the end of `Parse()`, merge global + graph naming into the final model:

```csharp
Naming = context.GraphNaming ?? context.GlobalNaming,
JsonContract = new JsonContractModel
{
    RootPropertyName = context.RootPropertyName,
    CollectionJsonNames = context.CollectionJsonNames.ToImmutableDictionary(),
},
```

**Step 8: Run tests to verify they pass**

Run: `dotnet test tests/DataNormalizer.Generators.Tests/ --filter "ClassName~ConfigurationParser" -v q`
Expected: PASS

**Step 9: Write failing tests for UseJsonContract parsing**

Tests that verify `Collection<SearchLine>("lines")` produces the correct `CollectionJsonNames` entry.

**Step 10: Run and verify those pass too**

**Step 11: Write failing tests for Reference().JsonName() parsing**

Tests that verify `x.Reference(p => p.Line).JsonName("line")` produces a `PropertyJsonNames` entry.

**Step 12: Run and verify**

**Step 13: Commit**

```
feat: extend ConfigurationParser to parse UseNaming, UseJsonContract, and Reference().JsonName()
```

---

### Task 4: Extend TypeGraphAnalyzer to read [NormalizeJsonName] attributes

**Files:**
- Modify: `src/DataNormalizer.Generators/Analysis/TypeGraphAnalyzer.cs`
- Test: `tests/DataNormalizer.Generators.Tests/Analysis/TypeGraphAnalyzerTests.cs`

**Step 1: Write failing test**

Add a test that creates a source type with `[NormalizeJsonName("line")]` on a property and verifies the analyzer sets `JsonNameOverride` on the corresponding `AnalyzedProperty`.

**Step 2: Run test to verify it fails**

**Step 3: In TypeGraphAnalyzer, when building AnalyzedProperty, check for the NormalizeJsonName attribute**

Look for an attribute named `NormalizeJsonNameAttribute` or `NormalizeJsonName` on the property symbol. If found, extract the string constructor argument and set `JsonNameOverride`.

Also merge with `PropertyJsonNames` from the parser (config overrides take precedence over attributes, or vice versa -- config wins).

**Step 4: Run tests to verify they pass**

**Step 5: Commit**

```
feat: read [NormalizeJsonName] attribute in TypeGraphAnalyzer
```

---

### Task 5: Update EmitterHelpers with naming-aware methods

**Files:**
- Modify: `src/DataNormalizer.Generators/Emitters/EmitterHelpers.cs`
- Test: `tests/DataNormalizer.Generators.Tests/Emitters/` (new tests in existing files or new file)

**Step 1: Write failing tests for new naming methods**

Test that:
- `GetDtoName("Person", namingModel)` returns `"PersonDto"` with defaults
- `GetDtoName("Person", new NamingModel { DtoPrefix = "Normalized", DtoSuffix = "" })` returns `"NormalizedPerson"`
- `GetContainerName("Person", namingModel)` returns `"PersonResultDto"` with defaults
- `GetListPropertyName(node, allNodes, namingModel)` returns `"PersonDtos"` with defaults

**Step 2: Run to verify failure**

**Step 3: Implement new naming methods**

```csharp
public static string GetDtoName(string typeName, NamingModel naming)
{
    return $"{naming.DtoPrefix}{typeName}{naming.DtoSuffix}";
}

public static string GetDtoFullName(string typeFullName, string typeName, NamingModel naming)
{
    var ns = GetNamespace(typeFullName);
    var dtoName = GetDtoName(typeName, naming);
    return string.IsNullOrEmpty(ns) ? dtoName : $"{ns}.{dtoName}";
}

public static string GetContainerName(string typeName, NamingModel naming)
{
    return $"{typeName}Result{naming.ContainerSuffix}";
}

public static string GetContainerFullName(string typeFullName, string typeName, NamingModel naming)
{
    var ns = GetNamespace(typeFullName);
    var containerName = GetContainerName(typeName, naming);
    return string.IsNullOrEmpty(ns) ? containerName : $"{ns}.{containerName}";
}

public static string GetListPropertyName(TypeGraphNode node, IReadOnlyList<TypeGraphNode> allNodes, NamingModel naming)
{
    var count = 0;
    for (var i = 0; i < allNodes.Count; i++)
    {
        if (allNodes[i].TypeName == node.TypeName)
            count++;
    }

    var baseName = node.TypeName;
    if (count > 1)
    {
        var ns = GetNamespace(node.TypeFullName);
        if (!string.IsNullOrEmpty(ns))
            baseName = ns.Replace(".", "") + baseName;
    }

    return $"{baseName}{naming.DtoSuffix}s";
}
```

**Step 4: Run tests to verify they pass**

**Step 5: Commit**

```
feat: add naming-aware helper methods to EmitterHelpers
```

---

### Task 6: Update DtoEmitter to use NamingModel

**Files:**
- Modify: `src/DataNormalizer.Generators/Emitters/DtoEmitter.cs`
- Modify: `tests/DataNormalizer.Generators.Tests/Emitters/DtoEmitterTests.cs`

**Step 1: Update all existing DtoEmitter tests for new defaults**

Change all assertions from `NormalizedPerson` to `PersonDto`, etc. Update `DtoEmitter.Emit()` calls to pass a `NamingModel`.

**Step 2: Run tests to verify they fail**

**Step 3: Change DtoEmitter.Emit signature to accept NamingModel**

```csharp
public static string Emit(TypeGraphNode node, bool copySourceAttributes, NamingModel naming)
```

Replace:
- `$"Normalized{node.TypeName}"` with `EmitterHelpers.GetDtoName(node.TypeName, naming)`
- JSON naming: if `naming.EmitJsonPropertyNames` is true, always emit `[JsonPropertyName]` with camelCase. If a property has `JsonNameOverride`, use that instead.
- Keep the old overload `Emit(TypeGraphNode node)` that calls `Emit(node, false, NamingModel.Default)` for backward compat in tests during transition.

**Step 4: Update EmitJsonNamingAttribute to use NamingModel**

Replace the `jsonNamingPolicy` string parameter approach with `NamingModel`:
- If `naming.EmitJsonPropertyNames` → always emit `[JsonPropertyName]`
- Check `prop.JsonNameOverride` first; if set, use it directly
- Otherwise camelCase the CLR property name

**Step 5: Run tests to verify they pass**

**Step 6: Write new tests for JsonNameOverride**

Test that when `AnalyzedProperty.JsonNameOverride = "line"`, the generated output contains `[JsonPropertyName("line")]` on the `LineIndex` property.

**Step 7: Run and verify**

**Step 8: Commit**

```
feat: update DtoEmitter to use NamingModel for type and property names
```

---

### Task 7: Update ContainerEmitter to use NamingModel and JsonContractModel

**Files:**
- Modify: `src/DataNormalizer.Generators/Emitters/ContainerEmitter.cs`
- Modify: `tests/DataNormalizer.Generators.Tests/Emitters/ContainerEmitterTests.cs`

**Step 1: Update existing ContainerEmitter tests for new defaults**

Change assertions from `NormalizedPersonResult` to `PersonResultDto`, from `PersonList` to `PersonDtos`, etc.

**Step 2: Run tests to verify they fail**

**Step 3: Change ContainerEmitter.Emit signature**

```csharp
public static string Emit(
    TypeGraphNode rootNode,
    IReadOnlyList<TypeGraphNode> allNodes,
    NamingModel naming,
    JsonContractModel jsonContract)
```

Replace:
- Container class name: use `EmitterHelpers.GetContainerName()`
- DTO full names: use `EmitterHelpers.GetDtoFullName()` with naming
- List property names: use `EmitterHelpers.GetListPropertyName()` with naming
- JSON property names on lists: check `jsonContract.CollectionJsonNames` first, then camelCase the CLR name
- Root property: if `jsonContract.RootPropertyName` is set, emit a root property holding the root DTO at index 0

**Step 4: Run tests to verify they pass**

**Step 5: Write new tests for JsonContractModel**

Test that `Collection<SearchLine>("lines")` causes the container to emit `[JsonPropertyName("lines")]` on the `SearchLineDtos` property.

Test that `RootPropertyName = "result"` causes a root property in the container.

**Step 6: Run and verify**

**Step 7: Commit**

```
feat: update ContainerEmitter to use NamingModel and JsonContractModel
```

---

### Task 8: Update NormalizerEmitter to use NamingModel

**Files:**
- Modify: `src/DataNormalizer.Generators/Emitters/NormalizerEmitter.cs`
- Modify: `tests/DataNormalizer.Generators.Tests/Emitters/NormalizerEmitterTests.cs`

**Step 1: Update existing NormalizerEmitter tests for new defaults**

Change assertions from `NormalizedPerson` to `PersonDto`, `NormalizedPersonResult` to `PersonResultDto`, `PersonList` to `PersonDtos`.

**Step 2: Run tests to verify they fail**

**Step 3: Update NormalizerEmitter to accept and use NamingModel**

Every reference to:
- `EmitterHelpers.GetContainerFullName(...)` → pass naming
- `EmitterHelpers.GetDtoFullName(...)` → pass naming
- `result.{node.TypeName}List` → use `EmitterHelpers.GetListPropertyName(node, allNodes, naming)`
- `$"Normalized{typeName}"` → use `EmitterHelpers.GetDtoName(typeName, naming)`

**Step 4: Run tests to verify they pass**

**Step 5: Commit**

```
feat: update NormalizerEmitter to use NamingModel
```

---

### Task 9: Update DenormalizerEmitter to use NamingModel

**Files:**
- Modify: `src/DataNormalizer.Generators/Emitters/DenormalizerEmitter.cs`
- Modify: `tests/DataNormalizer.Generators.Tests/Emitters/DenormalizerEmitterTests.cs`

**Step 1: Update existing DenormalizerEmitter tests for new defaults**

Change assertions from `NormalizedPersonResult` to `PersonResultDto`, `PersonList` to `PersonDtos`.

**Step 2: Run tests to verify they fail**

**Step 3: Update DenormalizerEmitter to accept and use NamingModel**

Every reference to:
- Container type name → use naming-aware method
- `normalized.{node.TypeName}List` → use naming-aware list property name
- DTO type names → use naming-aware method

**Step 4: Run tests to verify they pass**

**Step 5: Commit**

```
feat: update DenormalizerEmitter to use NamingModel
```

---

### Task 10: Wire everything through NormalizeGenerator

**Files:**
- Modify: `src/DataNormalizer.Generators/NormalizeGenerator.cs`

**Step 1: Update the generator to pass NamingModel and JsonContractModel from the parsed NormalizationModel to all emitters**

In the `Execute` method where emitters are called:
- `DtoEmitter.Emit(node, model.CopySourceAttributes, model.Naming)` instead of passing `model.JsonNamingPolicy`
- `ContainerEmitter.Emit(rootNode, nodes, model.Naming, model.JsonContract)` instead of passing `jsonNamingPolicy`
- `NormalizerEmitter.Emit(model, nodes)` → ensure it can access naming from model
- `DenormalizerEmitter.Emit(model, nodes)` → ensure it can access naming from model

**Step 2: Run all generator tests**

Run: `dotnet test tests/DataNormalizer.Generators.Tests/ --no-restore -v q`
Expected: PASS

**Step 3: Commit**

```
feat: wire NamingModel and JsonContractModel through NormalizeGenerator to all emitters
```

---

### Task 11: Update integration tests and E2E tests for new defaults

**Files:**
- Modify: `tests/DataNormalizer.Integration.Tests/` (all test files)
- Modify: `tests/DataNormalizer.Generators.Tests/GeneratorEndToEndTests.cs`

**Step 1: Update integration test configs and assertions**

All integration tests currently expect `NormalizedPerson`, `PersonList`, etc. Update to expect `PersonDto`, `PersonDtos`, etc.

**Step 2: Run all tests**

Run: `dotnet test --no-restore -v q`
Expected: PASS

**Step 3: Commit**

```
test: update all integration and E2E tests for new naming defaults
```

---

### Task 12: Update samples for new defaults

**Files:**
- Modify: `samples/DataNormalizer.Samples/Program.cs`
- Modify: `samples/DataNormalizer.Samples/SampleNormalization.cs`
- Modify: `samples/DataNormalizer.Samples/CorporateNormalization.cs`

**Step 1: Update sample code to use new type names**

Replace `NormalizedOrder` with `OrderDto`, `NormalizedOrderResult` with `OrderResultDto`, etc.

Optionally add a `UseNaming()` example to one of the configs.

**Step 2: Build and verify samples compile**

Run: `dotnet build samples/DataNormalizer.Samples/ --no-restore`
Expected: Build succeeded

**Step 3: Commit**

```
chore: update samples for new naming defaults
```

---

### Task 13: Add integration test with custom naming and JsonContract

**Files:**
- Create: `tests/DataNormalizer.Integration.Tests/NamingPolicyTests.cs`
- Create: `tests/DataNormalizer.Integration.Tests/CustomNamingConfig.cs`

**Step 1: Create a config that exercises all new features**

```csharp
[NormalizeConfiguration]
public sealed partial class CustomNamingConfig : NormalizationConfig
{
    protected override void Configure(NormalizeBuilder builder)
    {
        builder.UseNaming(n =>
        {
            n.DtoPrefix = "";
            n.DtoSuffix = "Dto";
            n.ContainerSuffix = "Dto";
            n.EmitJsonPropertyNames = true;
        });

        builder.NormalizeGraph<Team>(graph =>
        {
            graph.UseJsonContract(c =>
            {
                c.RootPropertyName = "team";
                c.Collection<Person>("people");
                c.Collection<Address>("addresses");
            });
        });

        builder.ForType<Person>(x =>
        {
            x.Reference(p => p.HomeAddress).JsonName("home");
        });
    }
}
```

**Step 2: Write roundtrip tests**

- Normalize a Team graph
- Verify the container type is `TeamResultDto`
- Verify the DTO types are `TeamDto`, `PersonDto`, `AddressDto`
- Serialize to JSON
- Verify JSON property names match expected contract
- Denormalize and verify roundtrip

**Step 3: Write test for [NormalizeJsonName] attribute**

Add `[NormalizeJsonName("addr")]` on a property and verify it appears in serialized JSON.

**Step 4: Run tests**

Run: `dotnet test tests/DataNormalizer.Integration.Tests/ --no-restore -v q`
Expected: PASS

**Step 5: Commit**

```
test: add integration tests for custom naming policy and JSON contract
```

---

### Task 14: Clean up old naming code paths

**Files:**
- Modify: `src/DataNormalizer.Generators/Emitters/EmitterHelpers.cs`
- Modify: `src/DataNormalizer.Generators/Emitters/DtoEmitter.cs`
- Modify: `src/DataNormalizer.Generators/Emitters/ContainerEmitter.cs`
- Modify: `src/DataNormalizer.Generators/Models/NormalizationModel.cs`

**Step 1: Remove old `JsonNamingPolicy` string field from NormalizationModel**

The `JsonNamingPolicy` field and `UseJsonNaming()` on `GraphBuilder` are superseded by `NamingModel.EmitJsonPropertyNames`. Remove the old field and update the parser to map `UseJsonNaming(CamelCase)` to `EmitJsonPropertyNames = true` for backward compat during transition.

**Step 2: Remove old overloads of GetDtoFullName, GetContainerFullName, GetListPropertyName that don't take NamingModel**

**Step 3: Remove old `Emit(TypeGraphNode node)` convenience overload on DtoEmitter**

**Step 4: Run all tests**

Run: `dotnet test --no-restore -v q`
Expected: PASS

**Step 5: Commit**

```
refactor: remove old naming code paths replaced by NamingModel
```

---

### Task 15: Run full build and format check

**Step 1: Build entire solution**

Run: `dotnet build --no-restore`
Expected: Build succeeded, 0 warnings (or only expected ones)

**Step 2: Run CSharpier format check**

Run: `dotnet csharpier check .`
Expected: All files formatted

**Step 3: Run all tests one final time**

Run: `dotnet test --no-restore`
Expected: All tests pass

**Step 4: Commit any formatting fixes**

```
style: format code with CSharpier
```
