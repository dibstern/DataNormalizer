# Naming Policy System Implementation Plan (Phase 1)

> **For Agent:** REQUIRED SUB-SKILL: Use executing-plans to implement this plan task-by-task.

**Goal:** Add a configurable naming policy system so generated DTO type names, container names, list property names, and JSON property names can be customized.

**Architecture:** New `NamingBuilder` config class in the runtime library (stub -- parsed by the source generator). New `NamingModel` equatable record in the generator's Models layer. `ConfigurationParser` extended to parse `UseNaming()` lambdas (including a new `ProcessAssignment` code path for property assignments). All four emitters updated to consult the naming model instead of hardcoded conventions.

**Tech Stack:** C# 12, .NET source generators (Roslyn), NUnit 4, System.Text.Json

**Scope:** Phase 1 covers naming policy only. Phase 2 (separate plan) will cover JSON contract customization: root property redesign, `UseJsonContract`, `Reference().JsonName()`, `[NormalizeJsonName]` attribute.

**Key design decisions:**
- Graph-level `UseNaming` MERGES with global defaults (only overrides explicitly set properties)
- When `DtoSuffix` is empty, list property names fall back to `{TypeName}List` convention
- `EmitJsonPropertyNames = true` by default (always emit `[JsonPropertyName]` with camelCase)

---

### Task 1: Add NamingModel to generator Models

**Files:**
- Create: `src/DataNormalizer.Generators/Models/NamingModel.cs`
- Modify: `src/DataNormalizer.Generators/Models/NormalizationModel.cs`
- Test: `tests/DataNormalizer.Generators.Tests/Models/NamingModelTests.cs`

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

**Step 2: Add NamingModel to NormalizationModel**

In `src/DataNormalizer.Generators/Models/NormalizationModel.cs`, add:

```csharp
public NamingModel Naming { get; init; } = NamingModel.Default;
```

> **Note:** Keep the existing `JsonNamingPolicy` field. It will be removed in Task 10. Until then, emitters continue using `JsonNamingPolicy` until explicitly switched in Tasks 4-7.

**Step 3: Write unit tests for NamingModel equality**

Test file: `tests/DataNormalizer.Generators.Tests/Models/NamingModelTests.cs`

Tests:
- Two identical instances → `Equals` returns `true`, `GetHashCode` matches
- Differing only in `DtoSuffix` → `Equals` returns `false`
- Differing only in `DtoPrefix` → `Equals` returns `false`
- Differing only in `ContainerSuffix` → `Equals` returns `false`
- Differing only in `EmitJsonPropertyNames` → `Equals` returns `false`
- `NamingModel.Default` equality with `new NamingModel()` → `true`
- Compared to `null` → `false`

**Step 4: Run all tests**

Run: `dotnet test tests/DataNormalizer.Generators.Tests/ --no-restore -v q`
Expected: All existing tests pass, new equality tests pass.

**Step 5: Commit**

```
feat: add NamingModel to generator models
```

---

### Task 2: Add NamingBuilder runtime class and UseNaming() methods

**Files:**
- Create: `src/DataNormalizer/Configuration/NamingBuilder.cs`
- Modify: `src/DataNormalizer/Configuration/NormalizeBuilder.cs`
- Modify: `src/DataNormalizer/Configuration/GraphBuilder.cs`

**Step 1: Create NamingBuilder**

```csharp
// src/DataNormalizer/Configuration/NamingBuilder.cs
namespace DataNormalizer.Configuration;

/// <summary>
/// Configures naming conventions for generated DTOs and containers.
/// </summary>
public sealed class NamingBuilder
{
    public string DtoPrefix { get; set; } = "";
    public string DtoSuffix { get; set; } = "Dto";
    public string ContainerSuffix { get; set; } = "Dto";
    public bool EmitJsonPropertyNames { get; set; } = true;
}
```

**Step 2: Add UseNaming to NormalizeBuilder**

```csharp
public NormalizeBuilder UseNaming(Action<NamingBuilder> configure)
{
    configure(new NamingBuilder());
    return this;
}
```

**Step 3: Add UseNaming to GraphBuilder**

```csharp
public GraphBuilder<T> UseNaming(Action<NamingBuilder> configure)
{
    configure(new NamingBuilder());
    return this;
}
```

**Step 4: Run tests, commit**

```
feat: add NamingBuilder and UseNaming() builder methods
```

---

### Task 3: Extend ConfigurationParser to parse UseNaming()

This requires a new parsing capability: **property assignment parsing** for `n.DtoSuffix = "Dto"` inside lambdas.

**Files:**
- Modify: `src/DataNormalizer.Generators/Analysis/ConfigurationParser.cs`
- Test: `tests/DataNormalizer.Generators.Tests/Analysis/ConfigurationParserTests.cs`

**Step 1: Write failing tests for UseNaming parsing**

Tests:
- `builder.UseNaming(n => { n.DtoSuffix = "Dto"; n.DtoPrefix = ""; })` → model has those values
- `builder.UseNaming(n => { n.EmitJsonPropertyNames = false; })` → boolean false parsed
- `builder.UseNaming(n => { n.EmitJsonPropertyNames = true; })` → boolean true parsed
- No `UseNaming` → `NamingModel.Default` values
- Assignment with non-literal RHS (e.g., `n.DtoSuffix = variable`) → silently ignored, default preserved
- `UseNaming` called twice on builder → last values win

**Step 2: Run tests to verify they fail**

**Step 3: Add NamingBuilder ReceiverKind**

```csharp
private enum ReceiverKind
{
    NormalizeBuilder,
    GraphBuilder,
    TypeBuilder,
    NamingBuilder,
}
```

**Step 4: Add naming fields to ParseContext**

```csharp
// Global naming values (mutable during parse)
public string GlobalDtoPrefix { get; set; } = "";
public string GlobalDtoSuffix { get; set; } = "Dto";
public string GlobalContainerSuffix { get; set; } = "Dto";
public bool GlobalEmitJsonPropertyNames { get; set; } = true;

// Per-graph overrides (null = not set, use global)
public string? GraphDtoPrefix { get; set; }
public string? GraphDtoSuffix { get; set; }
public string? GraphContainerSuffix { get; set; }
public bool? GraphEmitJsonPropertyNames { get; set; }

// Track whether current NamingBuilder lambda is global or graph-level
public bool IsParsingGraphNaming { get; set; }
```

**Step 5: Add ProcessAssignment method**

Property assignments like `n.DtoSuffix = "Dto"` are `AssignmentExpressionSyntax`, NOT `InvocationExpressionSyntax`. Add a new branch in `ProcessStatements`:

```csharp
case ExpressionStatementSyntax exprStmt when exprStmt.Expression is AssignmentExpressionSyntax assignment:
    ProcessAssignment(assignment, context);
    break;
```

`ProcessAssignment` implementation:
- Extract receiver name from left-hand `MemberAccessExpressionSyntax`
- Look up receiver kind in `ReceiverMap`
- If `ReceiverKind.NamingBuilder`: extract property name and value
  - For string properties (`DtoPrefix`, `DtoSuffix`, `ContainerSuffix`): extract via `LiteralExpressionSyntax.Token.ValueText`
  - For boolean (`EmitJsonPropertyNames`): check `assignment.Right.IsKind(SyntaxKind.TrueLiteralExpression)` vs `SyntaxKind.FalseLiteralExpression`
  - If RHS is not a literal: silently ignore (defensive)
- Store in global or graph fields based on `context.IsParsingGraphNaming`

**Step 6: Add UseNaming handler in AnalyzeInvocation**

```csharp
case "UseNaming" when receiverKind == ReceiverKind.NormalizeBuilder:
    ProcessUseNamingLambda(invocation, context, isGraph: false);
    return ReceiverKind.NormalizeBuilder;

case "UseNaming" when receiverKind == ReceiverKind.GraphBuilder:
    ProcessUseNamingLambda(invocation, context, isGraph: true);
    return ReceiverKind.GraphBuilder;
```

`ProcessUseNamingLambda`:
- Extract lambda parameter name
- Register in `ReceiverMap` as `ReceiverKind.NamingBuilder`
- Set `context.IsParsingGraphNaming = isGraph`
- Call `ProcessStatements` on lambda body
- Reset `context.IsParsingGraphNaming = false`

**Step 7: Wire NamingModel merge into returned NormalizationModel**

At the end of `Parse()`:

```csharp
Naming = new NamingModel
{
    DtoPrefix = context.GraphDtoPrefix ?? context.GlobalDtoPrefix,
    DtoSuffix = context.GraphDtoSuffix ?? context.GlobalDtoSuffix,
    ContainerSuffix = context.GraphContainerSuffix ?? context.GlobalContainerSuffix,
    EmitJsonPropertyNames = context.GraphEmitJsonPropertyNames ?? context.GlobalEmitJsonPropertyNames,
},
```

**Step 8: Also bridge `UseJsonNaming(CamelCase)` to `EmitJsonPropertyNames = true`**

When parsing `UseJsonNaming`, also set `context.GlobalEmitJsonPropertyNames = true`.

**Step 9: Run tests to verify they pass**

**Step 10: Write and run tests for merge semantics**

Tests:
- Global `DtoSuffix = "Model"` + graph `ContainerSuffix = "Container"` → final has both
- Global only → all global values apply
- Graph overrides everything → global fully overridden
- No UseNaming → `NamingModel.Default`

**Step 11: Commit**

```
feat: extend ConfigurationParser to parse UseNaming() with property assignment support
```

---

### Task 4: Update EmitterHelpers with naming-aware methods

**Files:**
- Modify: `src/DataNormalizer.Generators/Emitters/EmitterHelpers.cs`
- Create: `tests/DataNormalizer.Generators.Tests/Emitters/EmitterHelpersTests.cs`

**Step 1: Write failing tests**

Tests:
- `GetDtoName("Person", default)` → `"PersonDto"`
- `GetDtoName("Person", prefix="Normalized", suffix="")` → `"NormalizedPerson"`
- `GetDtoName("Address", default)` → `"AddressDto"`
- `GetContainerName("Person", default)` → `"PersonResultDto"`
- `GetContainerName("Person", containerSuffix="")` → `"PersonResult"`
- `GetDtoFullName("TestApp.Person", "Person", default)` → `"TestApp.PersonDto"`
- `GetContainerFullName("TestApp.Person", "Person", default)` → `"TestApp.PersonResultDto"`
- `GetListPropertyName(node, allNodes, default)` → `"PersonDtos"`
- `GetListPropertyName(node, allNodes, suffix="")` → `"PersonList"` (fallback)
- `GetListPropertyName(node, allNodes, suffix="Entity")` → `"PersonEntities"` (ToPlural)
- `GetListPropertyName` with duplicate TypeNames → namespace prefix applied
- `GetListPropertyName(addressNode, allNodes, default)` → `"AddressDtos"`

**Step 2: Run to verify failure**

**Step 3: Implement**

```csharp
public static string GetDtoName(string typeName, NamingModel naming)
    => $"{naming.DtoPrefix}{typeName}{naming.DtoSuffix}";

public static string GetDtoFullName(string typeFullName, string typeName, NamingModel naming)
{
    var ns = GetNamespace(typeFullName);
    var dtoName = GetDtoName(typeName, naming);
    return string.IsNullOrEmpty(ns) ? dtoName : $"{ns}.{dtoName}";
}

public static string GetContainerName(string typeName, NamingModel naming)
    => $"{typeName}Result{naming.ContainerSuffix}";

public static string GetContainerFullName(string typeFullName, string typeName, NamingModel naming)
{
    var ns = GetNamespace(typeFullName);
    var containerName = GetContainerName(typeName, naming);
    return string.IsNullOrEmpty(ns) ? containerName : $"{ns}.{containerName}";
}

public static string GetListPropertyName(TypeGraphNode node, IReadOnlyList<TypeGraphNode> allNodes, NamingModel naming)
{
    var baseName = node.TypeName;
    var count = 0;
    for (var i = 0; i < allNodes.Count; i++)
        if (allNodes[i].TypeName == node.TypeName) count++;
    if (count > 1)
    {
        var ns = GetNamespace(node.TypeFullName);
        if (!string.IsNullOrEmpty(ns)) baseName = ns.Replace(".", "") + baseName;
    }

    if (string.IsNullOrEmpty(naming.DtoSuffix))
        return $"{baseName}List";

    return ToPlural($"{baseName}{naming.DtoSuffix}");
}
```

**Step 4: Run tests, commit**

```
feat: add naming-aware helper methods to EmitterHelpers
```

---

### Task 5: Update DtoEmitter to use NamingModel

**Files:**
- Modify: `src/DataNormalizer.Generators/Emitters/DtoEmitter.cs`
- Modify: `tests/DataNormalizer.Generators.Tests/Emitters/DtoEmitterTests.cs`

**Step 1: Update existing tests for new defaults**

- Change `NormalizedPerson` → `PersonDto` in all assertions
- Update `Emit()` calls to pass `NamingModel`
- Tests asserting `Does.Not.Contain("JsonPropertyName")` with `jsonNamingPolicy: null` → pass `new NamingModel { EmitJsonPropertyNames = false }` or reverse assertion

**Step 2: Run tests to verify they fail**

**Step 3: Change DtoEmitter.Emit signature**

```csharp
public static string Emit(TypeGraphNode node, bool copySourceAttributes, NamingModel naming)
```

- Replace `$"Normalized{node.TypeName}"` with `EmitterHelpers.GetDtoName(node.TypeName, naming)`
- Replace `jsonNamingPolicy` check with `naming.EmitJsonPropertyNames`
- When `EmitJsonPropertyNames` is true: always emit `[JsonPropertyName]` with camelCase

**Step 4: Run tests to verify they pass**

**Step 5: Write new tests**

- `EmitJsonPropertyNames = true` → all properties have `[JsonPropertyName("camelCase")]`
- `EmitJsonPropertyNames = false` → no `[JsonPropertyName]` attributes
- Custom prefix/suffix: `DtoPrefix = "My", DtoSuffix = ""` → class named `MyPerson`

**Step 6: Commit**

```
feat: update DtoEmitter to use NamingModel
```

---

### Task 6: Update ContainerEmitter to use NamingModel

**Files:**
- Modify: `src/DataNormalizer.Generators/Emitters/ContainerEmitter.cs`
- Modify: `tests/DataNormalizer.Generators.Tests/Emitters/ContainerEmitterTests.cs`

**Step 1: Update existing tests for new defaults**

- `NormalizedPersonResult` → `PersonResultDto`
- `PersonList` → `PersonDtos`
- Update tests that assert no `[JsonPropertyName]` → use `new NamingModel { EmitJsonPropertyNames = false }`

**Step 2: Run tests to verify they fail**

**Step 3: Change ContainerEmitter.Emit signature**

```csharp
public static string Emit(TypeGraphNode rootNode, IReadOnlyList<TypeGraphNode> allNodes, NamingModel naming)
```

- Container class name: `EmitterHelpers.GetContainerName(rootNode.TypeName, naming)`
- DTO full names: `EmitterHelpers.GetDtoFullName(node.TypeFullName, node.TypeName, naming)`
- List property names: `EmitterHelpers.GetListPropertyName(node, allNodes, naming)`
- JSON names: if `naming.EmitJsonPropertyNames`, camelCase the CLR list property name

**Step 4: Run tests, write additional tests, commit**

```
feat: update ContainerEmitter to use NamingModel
```

---

### Task 7: Update NormalizerEmitter to use NamingModel

**Files:**
- Modify: `src/DataNormalizer.Generators/Emitters/NormalizerEmitter.cs`
- Modify: `tests/DataNormalizer.Generators.Tests/Emitters/NormalizerEmitterTests.cs`

**Step 1: Update tests** (`NormalizedPersonResult` → `PersonResultDto`, `PersonList` → `PersonDtos`)

**Step 2: Run tests to verify they fail**

**Step 3: Update NormalizerEmitter**

Access naming via `model.Naming`:
- Line 75: `GetContainerFullName` → pass `model.Naming`
- Line 87: `GetDtoFullName` → pass `model.Naming`
- Line 95: `result.{node.TypeName}List` → `result.{GetListPropertyName(node, allNodes, model.Naming)}`
- Line 106: `GetDtoFullName` → pass `model.Naming`

> Do NOT change `Normalize{typeName}` helper method names -- these are internal method names.

**Step 4: Run tests, commit**

```
feat: update NormalizerEmitter to use NamingModel
```

---

### Task 8: Update DenormalizerEmitter to use NamingModel

**Files:**
- Modify: `src/DataNormalizer.Generators/Emitters/DenormalizerEmitter.cs`
- Modify: `tests/DataNormalizer.Generators.Tests/Emitters/DenormalizerEmitterTests.cs`

**Step 1: Update tests**

**Step 2: Run tests to verify they fail**

**Step 3: Update DenormalizerEmitter**

Two changes via `model.Naming`:
- Line 63: `GetContainerFullName` → pass `model.Naming`
- Line 91: `normalized.{node.TypeName}List` → `normalized.{GetListPropertyName(node, allNodes, model.Naming)}`

Thread `NamingModel` through: `Emit` → `EmitDenormalizeMethod` → `EmitGetCollections`. Add `NamingModel naming` parameter to private methods that need it.

**Step 4: Run tests, commit**

```
feat: update DenormalizerEmitter to use NamingModel
```

---

### Task 9: Wire through NormalizeGenerator and update hint names

**Files:**
- Modify: `src/DataNormalizer.Generators/NormalizeGenerator.cs`

**Step 1: Update emitter calls**

- `DtoEmitter.Emit(node, model.CopySourceAttributes, model.Naming)` (was `model.JsonNamingPolicy`)
- `ContainerEmitter.Emit(rootNode, nodes, model.Naming)` (was `jsonNamingPolicy`)
- NormalizerEmitter/DenormalizerEmitter already receive `model`

**Step 2: Update hint name generation**

Lines 129-130, 158-159: replace `Normalized{TypeName}` with:
- `EmitterHelpers.GetDtoName(node.TypeName, model.Naming)` for DTO hints
- `EmitterHelpers.GetContainerName(rootNode.TypeName, model.Naming)` for container hints

**Step 3: Run all generator tests, commit**

```
feat: wire NamingModel through NormalizeGenerator
```

---

### Task 10: Clean up old naming code paths

**Files:**
- Modify: `src/DataNormalizer.Generators/Emitters/EmitterHelpers.cs`
- Modify: `src/DataNormalizer.Generators/Emitters/DtoEmitter.cs`
- Modify: `src/DataNormalizer.Generators/Emitters/ContainerEmitter.cs`
- Modify: `src/DataNormalizer.Generators/Models/NormalizationModel.cs`
- Modify: `src/DataNormalizer.Generators/Analysis/ConfigurationParser.cs`

**Step 1: Remove `JsonNamingPolicy` field from NormalizationModel** (superseded by `NamingModel.EmitJsonPropertyNames`)

**Step 2: Remove old method overloads without NamingModel parameter**

- `EmitterHelpers.GetDtoFullName(string, string)` → removed
- `EmitterHelpers.GetContainerFullName(string, string)` → removed
- `EmitterHelpers.GetListPropertyName(TypeGraphNode, IReadOnlyList<TypeGraphNode>)` → removed
- `DtoEmitter.Emit(TypeGraphNode node)` convenience overload → removed

**Step 3: Remove `JsonNamingPolicy` from ParseContext**

**Step 4: Run all tests, commit**

```
refactor: remove old naming code paths replaced by NamingModel
```

---

### Task 11: Update ALL integration tests and E2E tests

**Files (ALL need updating):**
- `tests/DataNormalizer.Integration.Tests/SimpleNormalizationTests.cs`
- `tests/DataNormalizer.Integration.Tests/BasicRoundtripTests.cs`
- `tests/DataNormalizer.Integration.Tests/ConfigFeatureTests.cs`
- `tests/DataNormalizer.Integration.Tests/CircularReferenceTests.cs`
- `tests/DataNormalizer.Integration.Tests/DeepNestingTests.cs`
- `tests/DataNormalizer.Integration.Tests/PerformanceTests.cs`
- `tests/DataNormalizer.Integration.Tests/SmokeTests.cs`
- `tests/DataNormalizer.Generators.Tests/GeneratorEndToEndTests.cs`

**Step 1: Type name renames across all files**

`Normalized{X}` → `{X}Dto` for ALL types (Person, Address, PhoneNumber, Order, Employee, TreeNode, NodeA, Universe, Galaxy, SolarSystem, Planet, Continent, Country, City, etc.)

**Step 2: List property renames across all files**

`{X}List` → `{X}Dtos` for ALL types (PersonList→PersonDtos, AddressList→AddressDtos, PhoneNumberList→PhoneNumberDtos, OrderList→OrderDtos, EmployeeList→EmployeeDtos, TreeNodeList→TreeNodeDtos, NodeAList→NodeADtos, UniverseList→UniverseDtos, GalaxyList→GalaxyDtos, SolarSystemList→SolarSystemDtos, PlanetList→PlanetDtos, ContinentList→ContinentDtos, CountryList→CountryDtos, CityList→CityDtos)

**Step 3: E2E hint name searches**

Update `GeneratorEndToEndTests.cs`:
- `"NormalizedPersonResult"` → `"PersonResultDto"`
- `"NormalizedOrderResult"` → `"OrderResultDto"`
- `"NormalizedAddress"` → `"AddressDto"`
- String-matched assertions in `E2E_MultipleRoots_ContainersOnlyHaveReachableEntityLists`

**Step 4: Verify Tasks 7-8 updated hardcoded names** (NormalizerEmitter line 95, DenormalizerEmitter line 91)

**Step 5: Run all tests, commit**

```
test: update all integration and E2E tests for new naming defaults
```

---

### Task 12: Update samples

**Files:**
- Modify: `samples/DataNormalizer.Samples/Program.cs`
- Modify: `samples/DataNormalizer.Samples/SampleNormalization.cs`
- Modify: `samples/DataNormalizer.Samples/CorporateNormalization.cs`

**Step 1: Type and list property renames**

Type: `NormalizedOrder`→`OrderDto`, `NormalizedOrderResult`→`OrderResultDto`, etc.

List (18 refs in Program.cs): `OrderList`→`OrderDtos`, `CustomerList`→`CustomerDtos`, `AddressList`→`AddressDtos`, `OrderLineList`→`OrderLineDtos`, `ProductList`→`ProductDtos`, `CorporationList`→`CorporationDtos`, `DivisionList`→`DivisionDtos`, `DepartmentList`→`DepartmentDtos`, `TeamList`→`TeamDtos`, `EmployeeList`→`EmployeeDtos`, `CertificationList`→`CertificationDtos`, `SkillList`→`SkillDtos`

**Step 2: Build, commit**

```
chore: update samples for new naming defaults
```

---

### Task 13: Add JSON serialization integration test

**Files:**
- Create: `tests/DataNormalizer.Integration.Tests/JsonNamingTests.cs`

**Step 1: Write test that verifies default JSON property names**

Using the existing Person/Address graph:
- Call `Normalize()` to produce the container
- Serialize to JSON with `System.Text.Json.JsonSerializer.Serialize()`
- Assert JSON contains `"personDtos"`, `"addressDtos"`, `"homeAddressIndex"` (camelCase)
- Assert JSON does NOT contain `"PersonDtos"`, `"AddressDtos"` (PascalCase)

**Step 2: Write test with `EmitJsonPropertyNames = false`**

Create a config with `UseNaming(n => { n.EmitJsonPropertyNames = false; })`.
- Normalize, serialize
- Verify property names are PascalCase (no `[JsonPropertyName]` attributes → default serializer behavior)

**Step 3: Write test with custom DtoSuffix**

Create a config with `UseNaming(n => { n.DtoSuffix = "Model"; n.DtoPrefix = ""; })`.
- Normalize, verify container type is `PersonResultDto` (ContainerSuffix unchanged)
- Verify list property names are `"personModels"` in JSON

**Step 4: Run tests, commit**

```
test: add JSON serialization integration tests for naming policy
```

---

### Task 14: Final build and format check

**Step 1:** `dotnet build --no-restore` → 0 errors
**Step 2:** `dotnet csharpier check .` → all formatted
**Step 3:** `dotnet test --no-restore` → all pass
**Step 4:** Commit any formatting fixes

```
style: format code with CSharpier
```

---

## Phase 2 (separate plan, not in scope)

The following features will be designed and planned separately:

- **Root property redesign**: Container has a named `Result` property (default JSON name `"result"`). Root type excluded from its own list unless referenced elsewhere in the type graph (compile-time check).
- **UseJsonContract**: `graph.UseJsonContract(c => { c.Collection<T>("name"); c.RootPropertyName = "result"; })`
- **Reference().JsonName()**: `x.Reference(p => p.Line).JsonName("line")` for per-property JSON name overrides
- **[NormalizeJsonName] attribute**: Domain attribute alternative for JSON name overrides
- **JsonContractModel/JsonContractBuilder/ReferenceBuilder**: Supporting models and config classes
