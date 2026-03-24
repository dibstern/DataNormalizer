# Naming Policy System Implementation Plan

> **For Agent:** REQUIRED SUB-SKILL: Use executing-plans to implement this plan task-by-task.

**Goal:** Add a configurable naming policy system so generated DTOs, containers, and JSON property names can be customized, with new defaults (Dto suffix, always-on JsonPropertyName).

**Architecture:** New `NamingBuilder` and `JsonContractBuilder` config classes in the runtime library (stubs -- parsed by the source generator). New `NamingModel`/`JsonContractModel` equatable records in the generator's Models layer. `ConfigurationParser` extended to parse the new syntax (including a new `ProcessAssignment` code path for property assignments). All four emitters updated to consult the naming model instead of hardcoded conventions.

**Tech Stack:** C# 12, .NET source generators (Roslyn), NUnit 4, System.Text.Json

**Key design decisions:**
- Graph-level `UseNaming` MERGES with global defaults (only overrides explicitly set properties)
- When `DtoSuffix` is empty, list property names fall back to `{TypeName}List` convention
- CLR names stay semantic (`LineIndex`, `TransitImagesIndices`); JSON names are controlled via `[JsonPropertyName]`

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
        foreach (var kvp in CollectionJsonNames)
        {
            hash = (hash * 397) ^ kvp.Key.GetHashCode();
            hash = (hash * 397) ^ kvp.Value.GetHashCode();
        }
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

> **Note:** Keep the existing `JsonNamingPolicy` field. It will be removed in Task 14. Until then, emitters should continue using `JsonNamingPolicy` for the old code path until explicitly switched in Tasks 6-9.

**Step 4: Add JsonNameOverride to AnalyzedProperty**

In `src/DataNormalizer.Generators/Models/AnalyzedProperty.cs`, add:

```csharp
public string? JsonNameOverride { get; init; }
```

**Step 5: Run tests to confirm no regressions**

Run: `dotnet test tests/DataNormalizer.Generators.Tests/ --no-restore -v q`
Expected: All existing tests still pass. The new fields have defaults for the new naming convention, but existing tests pass because emitters are not yet wired to these models.

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
    public string DtoPrefix { get; set; } = "";
    public string DtoSuffix { get; set; } = "Dto";
    public string ContainerSuffix { get; set; } = "Dto";
    public bool EmitJsonPropertyNames { get; set; } = true;
}
```

**Step 2: Create JsonContractBuilder**

```csharp
// src/DataNormalizer/Configuration/JsonContractBuilder.cs
namespace DataNormalizer.Configuration;

public sealed class JsonContractBuilder
{
    public string? RootPropertyName { get; set; }
    public void Collection<T>(string jsonName) { }
}
```

**Step 3: Create ReferenceBuilder**

```csharp
// src/DataNormalizer/Configuration/ReferenceBuilder.cs
namespace DataNormalizer.Configuration;

public sealed class ReferenceBuilder
{
    public ReferenceBuilder JsonName(string jsonName) => this;
}
```

**Step 4: Create NormalizeJsonNameAttribute**

```csharp
// src/DataNormalizer/Attributes/NormalizeJsonNameAttribute.cs
namespace DataNormalizer.Attributes;

[AttributeUsage(AttributeTargets.Property, Inherited = false)]
public sealed class NormalizeJsonNameAttribute : Attribute
{
    public string Name { get; }
    public NormalizeJsonNameAttribute(string name) => Name = name;
}
```

> **Note:** `Inherited = false` matches the convention used by `NormalizeIgnoreAttribute` and `NormalizeIncludeAttribute`.

**Step 5: Add UseNaming to NormalizeBuilder**

**Step 6: Add UseNaming and UseJsonContract to GraphBuilder**

**Step 7: Add Reference and ReferenceCollection to TypeBuilder**

(Same code as before -- adding the method stubs to each builder class.)

**Step 8: Run tests, Step 9: Commit**

```
feat: add NamingBuilder, JsonContractBuilder, ReferenceBuilder, and NormalizeJsonNameAttribute
```

---

### Task 3: Extend ConfigurationParser to parse new config syntax

This is the most complex task. It requires three new parsing capabilities:
1. **Property assignment parsing** (new code path) -- for `n.DtoSuffix = "Dto"`, `c.RootPropertyName = "result"`
2. **Cross-type method chaining** -- for `x.Reference(p => p.Line).JsonName("line")`
3. **Lambda parameter registration** -- for `UseNaming`/`UseJsonContract` lambda params

**Files:**
- Modify: `src/DataNormalizer.Generators/Analysis/ConfigurationParser.cs`
- Test: `tests/DataNormalizer.Generators.Tests/Analysis/ConfigurationParserTests.cs`

**Step 1: Write failing tests for UseNaming parsing**

Add tests that verify:
- `builder.UseNaming(n => { n.DtoSuffix = "Dto"; n.DtoPrefix = ""; n.ContainerSuffix = "Dto"; n.EmitJsonPropertyNames = true; })` is parsed into the NamingModel
- `graph.UseNaming(n => { n.DtoSuffix = "Normalized"; })` merges with global (only overrides DtoSuffix)
- Default NamingModel when no UseNaming is present
- Boolean literal `true`/`false` parsed correctly for `EmitJsonPropertyNames`

**Step 2: Run tests to verify they fail**

**Step 3: Extend ReceiverKind enum**

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
// Naming config
public string GlobalDtoPrefix { get; set; } = "";
public string GlobalDtoSuffix { get; set; } = "Dto";
public string GlobalContainerSuffix { get; set; } = "Dto";
public bool GlobalEmitJsonPropertyNames { get; set; } = true;

// Per-graph overrides (null = not set, use global)
public string? GraphDtoPrefix { get; set; }
public string? GraphDtoSuffix { get; set; }
public string? GraphContainerSuffix { get; set; }
public bool? GraphEmitJsonPropertyNames { get; set; }

// JSON contract
public string? RootPropertyName { get; set; }
public Dictionary<string, string> CollectionJsonNames { get; } = new();

// Per-property JSON name overrides: key = "TypeFqn.PropName", value = JSON name
public Dictionary<string, string> PropertyJsonNames { get; } = new();
```

> **Note on merge semantics:** Graph-level `UseNaming` only overrides properties explicitly assigned in the lambda. Unset properties fall through to global defaults. This is implemented by tracking graph values as nullable and merging at the end.

**Step 5: Add new ProcessAssignment method for property assignment parsing**

This is a **new code path** in `ProcessStatements`. Property assignments like `n.DtoSuffix = "Dto"` are `AssignmentExpressionSyntax`, NOT `InvocationExpressionSyntax`. Add a third case branch:

```csharp
private static void ProcessStatements(SyntaxList<StatementSyntax> statements, ParseContext context)
{
    foreach (var statement in statements)
    {
        switch (statement)
        {
            case LocalDeclarationStatementSyntax localDecl:
                ProcessLocalDeclaration(localDecl, context);
                break;

            case ExpressionStatementSyntax exprStmt when exprStmt.Expression is InvocationExpressionSyntax inv:
                ProcessTopLevelInvocation(inv, context);
                break;

            // NEW: Handle property assignments (n.DtoSuffix = "Dto", c.RootPropertyName = "result")
            case ExpressionStatementSyntax exprStmt when exprStmt.Expression is AssignmentExpressionSyntax assignment:
                ProcessAssignment(assignment, context);
                break;
        }
    }
}
```

New `ProcessAssignment` method:

```csharp
private static void ProcessAssignment(AssignmentExpressionSyntax assignment, ParseContext context)
{
    // Left side: n.DtoSuffix (MemberAccessExpression)
    if (assignment.Left is not MemberAccessExpressionSyntax memberAccess)
        return;

    var receiverName = memberAccess.Expression switch
    {
        IdentifierNameSyntax id => id.Identifier.Text,
        _ => null,
    };
    if (receiverName is null || !context.ReceiverMap.TryGetValue(receiverName, out var receiverKind))
        return;

    var propertyName = memberAccess.Name.Identifier.Text;

    switch (receiverKind)
    {
        case ReceiverKind.NamingBuilder:
            ProcessNamingAssignment(propertyName, assignment.Right, context, isGraph: /* determined by tracking */);
            break;
        case ReceiverKind.JsonContractBuilder:
            if (propertyName == "RootPropertyName" && assignment.Right is LiteralExpressionSyntax rootLiteral)
                context.RootPropertyName = rootLiteral.Token.ValueText;
            break;
    }
}
```

For string literals: extract via `LiteralExpressionSyntax.Token.ValueText`.
For boolean literals: check `assignment.Right.IsKind(SyntaxKind.TrueLiteralExpression)` or `SyntaxKind.FalseLiteralExpression`.

> **Important:** Track whether the NamingBuilder lambda is for global or graph-level via a flag on ParseContext (e.g., `IsParsingGraphNaming`), or use separate receiver kind values (`NamingBuilderGlobal` vs `NamingBuilderGraph`).

**Step 6: Add case handlers in AnalyzeInvocation for method calls**

Handle:
- `"UseNaming"` on `NormalizeBuilder` → extract lambda param, register as `NamingBuilder`, process lambda body
- `"UseNaming"` on `GraphBuilder` → same but sets graph-level overrides
- `"UseJsonContract"` on `GraphBuilder` → extract lambda param, register as `JsonContractBuilder`, process lambda body
- `"Collection"` on `JsonContractBuilder` → extract type arg FQN + string arg → store in `CollectionJsonNames`

**Step 7: Handle Reference().JsonName() cross-type chaining**

The existing `GetUltimateReceiverName` always resolves to the root receiver (`x` = TypeBuilder), which cannot work for cross-type chains like `x.Reference(p => p.Line).JsonName("line")`.

Solution: Use the return value from the inner `AnalyzeInvocation` call. The inner call processes `Reference(p => p.Line)`, extracts the property name, stores it in `context.CurrentReferencePropertyKey`, and returns `ReceiverKind.ReferenceBuilder`. Then in `AnalyzeInvocation`, when the outer call's immediate receiver is an invocation (line 104-110), use the returned `ReceiverKind` instead of looking up `GetUltimateReceiverName`:

```csharp
// At the top of AnalyzeInvocation, before the existing receiverName lookup:
if (invocation.Expression is MemberAccessExpressionSyntax outerAccess
    && outerAccess.Expression is InvocationExpressionSyntax innerInvocation)
{
    var innerResult = AnalyzeInvocation(innerInvocation, context);
    if (innerResult is not null)
    {
        // The inner call returned a receiver kind -- use it for the outer method
        var outerMethodName = GetMethodName(outerAccess);
        if (outerMethodName == "JsonName" && innerResult == ReceiverKind.ReferenceBuilder)
        {
            ProcessJsonNameOnReference(invocation, context);
            return ReceiverKind.ReferenceBuilder;
        }
    }
}
```

Where `ProcessJsonNameOnReference` extracts the string argument and stores it with the key from `context.CurrentReferencePropertyKey`.

**Step 8: Wire PropertyJsonNames to NormalizationModel**

Add to `NormalizationModel`:

```csharp
public ImmutableDictionary<string, string> PropertyJsonNameOverrides { get; init; } =
    ImmutableDictionary<string, string>.Empty;
```

In `Parse()`, transfer from context:

```csharp
PropertyJsonNameOverrides = context.PropertyJsonNames.ToImmutableDictionary(),
```

Task 4 (TypeGraphAnalyzer) will merge these into `AnalyzedProperty.JsonNameOverride`.

**Step 9: Wire NamingModel merge into returned NormalizationModel**

At the end of `Parse()`, merge global + graph naming:

```csharp
Naming = new NamingModel
{
    DtoPrefix = context.GraphDtoPrefix ?? context.GlobalDtoPrefix,
    DtoSuffix = context.GraphDtoSuffix ?? context.GlobalDtoSuffix,
    ContainerSuffix = context.GraphContainerSuffix ?? context.GlobalContainerSuffix,
    EmitJsonPropertyNames = context.GraphEmitJsonPropertyNames ?? context.GlobalEmitJsonPropertyNames,
},
JsonContract = new JsonContractModel
{
    RootPropertyName = context.RootPropertyName,
    CollectionJsonNames = context.CollectionJsonNames.ToImmutableDictionary(),
},
PropertyJsonNameOverrides = context.PropertyJsonNames.ToImmutableDictionary(),
```

**Step 10: Run tests, write tests for UseJsonContract and Reference().JsonName(), run and verify**

**Step 11: Also parse `UseJsonNaming(CamelCase)` as `EmitJsonPropertyNames = true`**

When the parser encounters `UseJsonNaming`, in addition to setting `context.JsonNamingPolicy = "CamelCase"`, also set `context.GlobalEmitJsonPropertyNames = true`. This ensures a seamless transition for any code using the old API.

**Step 12: Commit**

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

Look for `NormalizeJsonNameAttribute` on the property symbol. If found, extract the string constructor argument and set `JsonNameOverride`.

Also check `model.PropertyJsonNameOverrides` (from the config parser). Config overrides take precedence over attributes:

```csharp
var jsonNameOverride = (string?)null;
// Check config-based override first (higher priority)
var propKey = $"{typeSymbol.ToDisplayString()}.{propertySymbol.Name}";
if (model.PropertyJsonNameOverrides.TryGetValue(propKey, out var configOverride))
    jsonNameOverride = configOverride;
// Fall back to attribute
else if (/* property has NormalizeJsonNameAttribute */)
    jsonNameOverride = /* extracted value */;
```

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
- `GetListPropertyName(node, allNodes, new NamingModel { DtoSuffix = "" })` returns `"PersonList"` (fallback)
- `GetListPropertyName(node, allNodes, new NamingModel { DtoSuffix = "Entity" })` returns `"PersonEntities"` (uses `ToPlural`)

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

    // When DtoSuffix is empty, fall back to "{TypeName}List" convention
    if (string.IsNullOrEmpty(naming.DtoSuffix))
        return $"{baseName}List";

    // Use ToPlural to handle suffixes correctly (e.g., "Entity" -> "Entities")
    return ToPlural($"{baseName}{naming.DtoSuffix}");
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

Change all assertions from `NormalizedPerson` to `PersonDto`. Update `DtoEmitter.Emit()` calls to pass a `NamingModel`. Tests that assert `Does.Not.Contain("JsonPropertyName")` when passing `jsonNamingPolicy: null` must be updated to pass `new NamingModel { EmitJsonPropertyNames = false }` instead (or reverse the assertion, since default is now `true`).

**Step 2: Run tests to verify they fail**

**Step 3: Change DtoEmitter.Emit signature to accept NamingModel**

```csharp
public static string Emit(TypeGraphNode node, bool copySourceAttributes, NamingModel naming)
```

Replace:
- `$"Normalized{node.TypeName}"` with `EmitterHelpers.GetDtoName(node.TypeName, naming)`

**Step 4: Update EmitJsonNamingAttribute to use NamingModel**

Replace the `jsonNamingPolicy` string parameter approach:
- If `naming.EmitJsonPropertyNames` → always emit `[JsonPropertyName]`
- Check `prop.JsonNameOverride` first; if set, use it directly
- Otherwise camelCase the CLR property name

**Step 5: Run tests to verify they pass**

**Step 6: Write new tests for JsonNameOverride**

**Step 7: Commit**

```
feat: update DtoEmitter to use NamingModel for type and property names
```

---

### Task 7: Update ContainerEmitter to use NamingModel and JsonContractModel

**Files:**
- Modify: `src/DataNormalizer.Generators/Emitters/ContainerEmitter.cs`
- Modify: `tests/DataNormalizer.Generators.Tests/Emitters/ContainerEmitterTests.cs`

**Step 1: Update existing ContainerEmitter tests for new defaults**

Change assertions from `NormalizedPersonResult` to `PersonResultDto`, from `PersonList` to `PersonDtos`. Update tests that assert no `[JsonPropertyName]` when `jsonNamingPolicy: null` to use `new NamingModel { EmitJsonPropertyNames = false }`.

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
- Container class name: use `EmitterHelpers.GetContainerName(rootNode.TypeName, naming)`
- DTO full names: use `EmitterHelpers.GetDtoFullName(node.TypeFullName, node.TypeName, naming)`
- List property names (CLR): use `EmitterHelpers.GetListPropertyName(node, allNodes, naming)`
- JSON property names on lists: check `jsonContract.CollectionJsonNames[node.TypeFullName]` first, then camelCase the CLR property name
- Root property: if `jsonContract.RootPropertyName` is set, emit a root DTO property with that JSON name

**Step 4-7: Run tests, write new tests for JsonContractModel, commit**

```
feat: update ContainerEmitter to use NamingModel and JsonContractModel
```

---

### Task 8: Update NormalizerEmitter to use NamingModel and JsonContractModel

**Files:**
- Modify: `src/DataNormalizer.Generators/Emitters/NormalizerEmitter.cs`
- Modify: `tests/DataNormalizer.Generators.Tests/Emitters/NormalizerEmitterTests.cs`

**Step 1: Update existing NormalizerEmitter tests for new defaults**

Change assertions from `NormalizedPersonResult` to `PersonResultDto`, `PersonList` to `PersonDtos`.

**Step 2: Run tests to verify they fail**

**Step 3: Update NormalizerEmitter to use NamingModel**

Specific changes (the `model` parameter already provides `model.Naming`):
- Line 75: `EmitterHelpers.GetContainerFullName(rootType.FullyQualifiedName, rootNode.TypeName)` → pass `model.Naming`
- Line 87: `EmitterHelpers.GetDtoFullName(node.TypeFullName, node.TypeName)` → pass `model.Naming`
- Line 95: `result.{node.TypeName}List` → use `EmitterHelpers.GetListPropertyName(node, allNodes, model.Naming)`
- Line 106: `EmitterHelpers.GetDtoFullName(node.TypeFullName, typeName)` → pass `model.Naming`

> **Important:** Do NOT change `Normalize{typeName}` helper method names (lines 80, 109, etc.). These are internal method names, not type names.

**Step 4: Add root property population**

If `model.JsonContract.RootPropertyName` is set, after populating all list properties, emit:

```csharp
result.Root = __{rootCamel}Arr[0];
```

This requires NormalizerEmitter to also consult `model.JsonContract`.

**Step 5: Run tests, commit**

```
feat: update NormalizerEmitter to use NamingModel and JsonContractModel
```

---

### Task 9: Update DenormalizerEmitter to use NamingModel

**Files:**
- Modify: `src/DataNormalizer.Generators/Emitters/DenormalizerEmitter.cs`
- Modify: `tests/DataNormalizer.Generators.Tests/Emitters/DenormalizerEmitterTests.cs`

**Step 1: Update existing tests for new defaults**

**Step 2: Run tests to verify they fail**

**Step 3: Update DenormalizerEmitter to use NamingModel**

Two specific changes:
- Line 63: `EmitterHelpers.GetContainerFullName(rootType.FullyQualifiedName, rootNode.TypeName)` → pass `model.Naming`
- Line 91: `normalized.{node.TypeName}List` → use `EmitterHelpers.GetListPropertyName(node, allNodes, model.Naming)`

> **Note:** DenormalizerEmitter reconstructs source types, not DTO types, so DTO naming changes do not affect most of its output.

Thread `NamingModel` (from `model.Naming`) through the private method chain: `Emit` → `EmitDenormalizeMethod` → `EmitGetCollections`. `EmitGetCollections` already receives `allNodes` but needs `naming` added.

**Step 4: Run tests, commit**

```
feat: update DenormalizerEmitter to use NamingModel
```

---

### Task 10: Wire everything through NormalizeGenerator

**Files:**
- Modify: `src/DataNormalizer.Generators/NormalizeGenerator.cs`

**Step 1: Update emitter calls**

- Line 124: `DtoEmitter.Emit(node, model.CopySourceAttributes, model.Naming)` (remove old `model.JsonNamingPolicy` parameter)
- `ContainerEmitter.Emit(rootNode, nodes, model.Naming, model.JsonContract)`
- NormalizerEmitter and DenormalizerEmitter already receive `model`, so they access `model.Naming` and `model.JsonContract` internally.

**Step 2: Update hint name generation**

Lines 129-130 and 158-159 hardcode `Normalized{TypeName}` in hint names. Update to:
- `$"{EmitterHelpers.GetDtoName(node.TypeName, model.Naming)}.g.cs"` for DTO hint names
- `$"{EmitterHelpers.GetContainerName(rootNode.TypeName, model.Naming)}.g.cs"` for container hint names

**Step 3: Run all generator tests, commit**

```
feat: wire NamingModel and JsonContractModel through NormalizeGenerator to all emitters
```

---

### Task 11: Update integration tests and E2E tests for new defaults

**Files to update (ALL of these):**
- `tests/DataNormalizer.Integration.Tests/SimpleNormalizationTests.cs`
- `tests/DataNormalizer.Integration.Tests/BasicRoundtripTests.cs`
- `tests/DataNormalizer.Integration.Tests/ConfigFeatureTests.cs`
- `tests/DataNormalizer.Integration.Tests/CircularReferenceTests.cs`
- `tests/DataNormalizer.Integration.Tests/DeepNestingTests.cs`
- `tests/DataNormalizer.Integration.Tests/PerformanceTests.cs`
- `tests/DataNormalizer.Integration.Tests/SmokeTests.cs`
- `tests/DataNormalizer.Generators.Tests/GeneratorEndToEndTests.cs`

**Step 1: Systematic renames across all files**

Type name renames:
- `NormalizedPerson` → `PersonDto`
- `NormalizedAddress` → `AddressDto`
- `NormalizedPhoneNumber` → `PhoneNumberDto`
- `NormalizedOrder` → `OrderDto`
- `NormalizedPersonResult` → `PersonResultDto`
- `NormalizedOrderResult` → `OrderResultDto`
- (and all other `Normalized{X}` → `{X}Dto`)

List property renames:
- `PersonList` → `PersonDtos`
- `AddressList` → `AddressDtos`
- `PhoneNumberList` → `PhoneNumberDtos`
- `OrderList` → `OrderDtos`
- `EmployeeList` → `EmployeeDtos`
- `TreeNodeList` → `TreeNodeDtos`
- `NodeAList` → `NodeADtos`
- `UniverseList` → `UniverseDtos`
- `GalaxyList` → `GalaxyDtos`
- `SolarSystemList` → `SolarSystemDtos`
- `PlanetList` → `PlanetDtos`
- `ContinentList` → `ContinentDtos`
- `CountryList` → `CountryDtos`
- `CityList` → `CityDtos`
- (and all other `{X}List` → `{X}Dtos`)

**Step 2: Update E2E hint name searches**

In `GeneratorEndToEndTests.cs`:
- `s.hintName.Contains("NormalizedPersonResult")` → `"PersonResultDto"`
- `s.hintName.Contains("NormalizedOrderResult")` → `"OrderResultDto"`
- `s.hintName.Contains("NormalizedAddress")` → `"AddressDto"`
- Update all string-matched assertions in `E2E_MultipleRoots_ContainersOnlyHaveReachableEntityLists`

**Step 3: Verify Tasks 8-9 updated hardcoded names**

Before running tests, confirm:
- `NormalizerEmitter.EmitPublicNormalizeMethod()` (line 95) uses naming-aware list property name
- `DenormalizerEmitter.EmitGetCollections()` (line 91) uses naming-aware list property name

**Step 4: Run all tests, commit**

```
test: update all integration and E2E tests for new naming defaults
```

---

### Task 12: Update samples for new defaults

**Files:**
- Modify: `samples/DataNormalizer.Samples/Program.cs`
- Modify: `samples/DataNormalizer.Samples/SampleNormalization.cs`
- Modify: `samples/DataNormalizer.Samples/CorporateNormalization.cs`

**Step 1: Rename all generated types and list properties**

Type renames: `NormalizedOrder` → `OrderDto`, `NormalizedOrderResult` → `OrderResultDto`, etc.

List property renames (18 references in `Program.cs`):
- `OrderList` → `OrderDtos`
- `CustomerList` → `CustomerDtos`
- `AddressList` → `AddressDtos`
- `OrderLineList` → `OrderLineDtos`
- `ProductList` → `ProductDtos`
- `CorporationList` → `CorporationDtos`
- `DivisionList` → `DivisionDtos`
- `DepartmentList` → `DepartmentDtos`
- `TeamList` → `TeamDtos`
- `EmployeeList` → `EmployeeDtos`
- `CertificationList` → `CertificationDtos`
- `SkillList` → `SkillDtos`

**Step 2: Build and verify, commit**

```
chore: update samples for new naming defaults
```

---

### Task 13: Add integration tests with custom naming, JsonContract, and default JSON roundtrip

**Files:**
- Create: `tests/DataNormalizer.Integration.Tests/TestTypes/Naming/` (new test types)
- Create: `tests/DataNormalizer.Integration.Tests/CustomNamingConfig.cs`
- Create: `tests/DataNormalizer.Integration.Tests/NamingPolicyTests.cs`

**Step 1: Create test types inspired by the search-response use case**

Create types in `TestTypes/Naming/` namespace to avoid collision with existing cycle test types:
- `SearchResponse` (root) with routes, places, carriers references
- `SearchRoute` with segments
- `SearchSegment` with options
- `SearchHop` with line, carrier, vehicle references
- `SearchLine` with places
- `SearchPlace`, `SearchCarrier`, `SearchVehicle`

These mirror the structure from the ChatGPT discussion's transport search example.

**Step 2: Create CustomNamingConfig**

```csharp
[NormalizeConfiguration]
public partial class CustomNamingConfig : NormalizationConfig
{
    protected override void Configure(NormalizeBuilder builder)
    {
        builder.NormalizeGraph<SearchResponse>(graph =>
        {
            graph.UseJsonContract(c =>
            {
                c.RootPropertyName = "result";
                c.Collection<SearchRoute>("routes");
                c.Collection<SearchLine>("lines");
                c.Collection<SearchPlace>("places");
                c.Collection<SearchCarrier>("carriers");
            });
        });

        builder.ForType<SearchHop>(x =>
        {
            x.Reference(p => p.Line).JsonName("line");
            x.Reference(p => p.Carrier).JsonName("carrier");
        });
    }
}
```

**Step 3: Write roundtrip + JSON serialization tests**

- Normalize a SearchResponse graph
- Serialize to JSON with `System.Text.Json.JsonSerializer`
- Verify JSON contains expected property names (`"routes"`, `"lines"`, `"line"`, `"carrier"`)
- Denormalize and verify roundtrip

**Step 4: Write default naming JSON roundtrip test**

Using the basic Person/Address graph:
- Normalize, serialize to JSON
- Verify default JSON property names are camelCase (`"personDtos"`, `"addressDtos"`, `"homeAddressIndex"`)

**Step 5: Write test for [NormalizeJsonName] attribute**

Add `[NormalizeJsonName("addr")]` on a property in one of the test types, verify it appears in serialized JSON.

**Step 6: Run tests, commit**

```
test: add integration tests for custom naming policy, JSON contract, and default JSON roundtrip
```

---

### Task 14: Clean up old naming code paths

**Files:**
- Modify: `src/DataNormalizer.Generators/Emitters/EmitterHelpers.cs`
- Modify: `src/DataNormalizer.Generators/Emitters/DtoEmitter.cs`
- Modify: `src/DataNormalizer.Generators/Emitters/ContainerEmitter.cs`
- Modify: `src/DataNormalizer.Generators/Models/NormalizationModel.cs`
- Modify: `src/DataNormalizer.Generators/Analysis/ConfigurationParser.cs`

**Step 1: Remove old `JsonNamingPolicy` string field from NormalizationModel**

The field is superseded by `NamingModel.EmitJsonPropertyNames`. Task 3 already maps `UseJsonNaming(CamelCase)` to `EmitJsonPropertyNames = true`.

**Step 2: Remove old overloads**

- `EmitterHelpers.GetDtoFullName(string, string)` (two-arg, no NamingModel)
- `EmitterHelpers.GetContainerFullName(string, string)` (two-arg, no NamingModel)
- `EmitterHelpers.GetListPropertyName(TypeGraphNode, IReadOnlyList<TypeGraphNode>)` (two-arg, no NamingModel)
- `DtoEmitter.Emit(TypeGraphNode node)` (convenience overload)

**Step 3: Remove `JsonNamingPolicy` from ParseContext and parser output**

**Step 4: Run all tests, commit**

```
refactor: remove old naming code paths replaced by NamingModel
```

---

### Task 15: Run full build and format check

**Step 1:** `dotnet build --no-restore` -- verify 0 errors
**Step 2:** `dotnet csharpier check .` -- verify all formatted
**Step 3:** `dotnet test --no-restore` -- verify all pass
**Step 4:** Commit any formatting fixes

```
style: format code with CSharpier
```
