# JSON Contract Customization Implementation Plan (Phase 2)

> **For Agent:** REQUIRED SUB-SKILL: Use executing-plans to implement this plan task-by-task.

**Goal:** Add root property redesign, `UseJsonContract`, `Reference().JsonName()`, and `[NormalizeJsonName]` attribute so generated containers match existing API wire formats.

**Architecture:** Builds on Phase 1's NamingModel and naming-aware emitters. Adds `JsonContractModel` for per-graph contract config, `AnalyzedProperty.JsonNameOverride` for per-property JSON name overrides, and a `rootTypeNeedsList` compile-time check in the type graph. Container emitter gains a `Result` property and conditional list emission. Parser gains cross-type chain handling for `Reference().JsonName()`.

**Tech Stack:** C# 12, .NET source generators (Roslyn), NUnit 4, System.Text.Json

**Prerequisite:** Phase 1 (naming policy) must be complete. This plan assumes `NamingModel`, `ProcessAssignment`, and naming-aware emitters are already in place.

**Key design decisions:**
- Every container always has a named `Result` property (default JSON name `"result"`)
- Root type has no list array unless it is referenced by other types in the graph (compile-time check)
- Config `Reference().JsonName()` overrides `[NormalizeJsonName]` attribute when both exist
- `UseJsonContract` provides per-graph collection JSON name overrides and root property name customization

---

### Task 1: Add JsonContractModel and JsonNameOverride to generator Models

**Files:**
- Create: `src/DataNormalizer.Generators/Models/JsonContractModel.cs`
- Modify: `src/DataNormalizer.Generators/Models/NormalizationModel.cs`
- Modify: `src/DataNormalizer.Generators/Models/AnalyzedProperty.cs`
- Test: `tests/DataNormalizer.Generators.Tests/Models/JsonContractModelTests.cs`

**Step 1: Create JsonContractModel**

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

**Step 2: Add to NormalizationModel**

```csharp
public JsonContractModel JsonContract { get; init; } = JsonContractModel.Default;
public ImmutableDictionary<string, string> PropertyJsonNameOverrides { get; init; } =
    ImmutableDictionary<string, string>.Empty;
```

**Step 3: Add JsonNameOverride to AnalyzedProperty**

```csharp
public string? JsonNameOverride { get; init; }
```

**Step 4: Write unit tests for JsonContractModel equality**

Tests:
- Two identical instances → `Equals` true, `GetHashCode` matches
- Different `RootPropertyName` → `Equals` false
- Same count, different keys → `Equals` false, `GetHashCode` differs
- Same count, different values → `Equals` false
- Empty vs non-empty `CollectionJsonNames` → `Equals` false
- `Default` equality with `new()` → true

**Step 5: Run tests, commit**

```
feat: add JsonContractModel, PropertyJsonNameOverrides, and JsonNameOverride
```

---

### Task 2: Add runtime configuration classes

**Files:**
- Create: `src/DataNormalizer/Configuration/JsonContractBuilder.cs`
- Create: `src/DataNormalizer/Configuration/ReferenceBuilder.cs`
- Create: `src/DataNormalizer/Attributes/NormalizeJsonNameAttribute.cs`
- Modify: `src/DataNormalizer/Configuration/GraphBuilder.cs`
- Modify: `src/DataNormalizer/Configuration/TypeBuilder.cs`

**Step 1: Create JsonContractBuilder**

```csharp
namespace DataNormalizer.Configuration;

public sealed class JsonContractBuilder
{
    public string? RootPropertyName { get; set; }
    public void Collection<T>(string jsonName) { }
}
```

**Step 2: Create ReferenceBuilder**

```csharp
namespace DataNormalizer.Configuration;

public sealed class ReferenceBuilder
{
    public ReferenceBuilder JsonName(string jsonName) => this;
}
```

**Step 3: Create NormalizeJsonNameAttribute**

```csharp
namespace DataNormalizer.Attributes;

[AttributeUsage(AttributeTargets.Property, Inherited = false)]
public sealed class NormalizeJsonNameAttribute : Attribute
{
    public string Name { get; }
    public NormalizeJsonNameAttribute(string name) => Name = name;
}
```

**Step 4: Add UseJsonContract to GraphBuilder**

```csharp
public GraphBuilder<T> UseJsonContract(Action<JsonContractBuilder> configure)
{
    configure(new JsonContractBuilder());
    return this;
}
```

**Step 5: Add Reference and ReferenceCollection to TypeBuilder**

```csharp
public ReferenceBuilder Reference(Expression<Func<T, object?>> selector) => new();
public ReferenceBuilder ReferenceCollection(Expression<Func<T, object?>> selector) => new();
```

**Step 6: Run tests, commit**

```
feat: add JsonContractBuilder, ReferenceBuilder, NormalizeJsonNameAttribute, and builder methods
```

---

### Task 3: Extend ConfigurationParser for UseJsonContract

**Files:**
- Modify: `src/DataNormalizer.Generators/Analysis/ConfigurationParser.cs`
- Test: `tests/DataNormalizer.Generators.Tests/Analysis/ConfigurationParserTests.cs`

**Step 1: Write failing tests**

Tests:
- `graph.UseJsonContract(c => { c.RootPropertyName = "result"; })` → model has `JsonContract.RootPropertyName == "result"`
- `graph.UseJsonContract(c => { c.Collection<SearchLine>("lines"); })` → `JsonContract.CollectionJsonNames` has entry
- Multiple `Collection<T>()` calls → all stored
- `RootPropertyName` with empty string → stored as empty
- No `UseJsonContract` → `JsonContractModel.Default`
- `UseJsonContract` with both `RootPropertyName` and `Collection<T>()` calls in same lambda

**Step 2: Run tests to verify they fail**

**Step 3: Add JsonContractBuilder ReceiverKind**

```csharp
private enum ReceiverKind
{
    NormalizeBuilder,
    GraphBuilder,
    TypeBuilder,
    NamingBuilder,
    JsonContractBuilder,  // NEW
    ReferenceBuilder,     // NEW (for Task 4)
}
```

**Step 4: Add JsonContract fields to ParseContext**

```csharp
public string? RootPropertyName { get; set; }
public Dictionary<string, string> CollectionJsonNames { get; } = new();
```

**Step 5: Add UseJsonContract handler in AnalyzeInvocation**

```csharp
case "UseJsonContract" when receiverKind == ReceiverKind.GraphBuilder:
    ProcessJsonContractLambda(invocation, context);
    return ReceiverKind.GraphBuilder;
```

`ProcessJsonContractLambda`: extract lambda param, register as `ReceiverKind.JsonContractBuilder`, call `ProcessStatements` on body.

**Step 6: Handle Collection<T>() method call on JsonContractBuilder**

```csharp
case "Collection" when receiverKind == ReceiverKind.JsonContractBuilder:
    // Extract type argument FQN and string argument
    var typeFqn = GetTypeArgumentSymbol(memberAccess, context.SemanticModel);
    var jsonName = ExtractStringArgument(invocation);
    if (typeFqn != null && jsonName != null)
        context.CollectionJsonNames[NormalizeFqn(typeFqn.ToDisplayString(...))] = jsonName;
    return ReceiverKind.JsonContractBuilder;
```

**Step 7: Handle RootPropertyName assignment on JsonContractBuilder**

Phase 1 already added `ProcessAssignment`. Extend it:

```csharp
case ReceiverKind.JsonContractBuilder:
    if (propertyName == "RootPropertyName" && assignment.Right is LiteralExpressionSyntax rootLit)
        context.RootPropertyName = rootLit.Token.ValueText;
    break;
```

**Step 8: Wire into returned NormalizationModel**

```csharp
JsonContract = new JsonContractModel
{
    RootPropertyName = context.RootPropertyName,
    CollectionJsonNames = context.CollectionJsonNames.ToImmutableDictionary(),
},
```

**Step 9: Run tests, commit**

```
feat: extend ConfigurationParser to parse UseJsonContract
```

---

### Task 4: Extend ConfigurationParser for Reference().JsonName()

This requires handling **cross-type method chaining** where `x.Reference(p => p.Line).JsonName("line")` transitions from TypeBuilder to ReferenceBuilder.

**Files:**
- Modify: `src/DataNormalizer.Generators/Analysis/ConfigurationParser.cs`
- Test: `tests/DataNormalizer.Generators.Tests/Analysis/ConfigurationParserTests.cs`

**Step 1: Write failing tests**

Tests:
- `x.Reference(p => p.Line).JsonName("line")` → `PropertyJsonNameOverrides["TypeFqn.Line"] == "line"`
- `x.ReferenceCollection(p => p.Items).JsonName("items")` → same pattern
- Multiple `Reference().JsonName()` calls on same TypeBuilder → all stored
- `x.Reference(p => p.Line)` without `.JsonName()` → no entry, no crash
- Split-statement pattern: `var r = x.Reference(p => p.Line); r.JsonName("line");` → correctly tracked via local variable

**Step 2: Run tests to verify they fail**

**Step 3: Add PropertyJsonNames to ParseContext**

```csharp
public Dictionary<string, string> PropertyJsonNames { get; } = new();
```

**Step 4: Handle Reference/ReferenceCollection on TypeBuilder**

```csharp
case "Reference" when receiverKind == ReceiverKind.TypeBuilder:
case "ReferenceCollection" when receiverKind == ReceiverKind.TypeBuilder:
    var refPropName = ExtractPropertyNameFromLambdaArg(invocation);
    if (refPropName != null && context.TypeBuilderMap.TryGetValue(receiverName, out var refTypeFqn))
        context.CurrentReferenceKey = $"{refTypeFqn}.{refPropName}";
    return ReceiverKind.ReferenceBuilder;
```

**Step 5: Handle cross-type chaining for JsonName on ReferenceBuilder**

The existing `GetUltimateReceiverName` resolves to the root receiver (TypeBuilder), which won't work for `JsonName` on `ReferenceBuilder`. Use the return value from the inner `AnalyzeInvocation`:

```csharp
// At top of AnalyzeInvocation, when outer receiver is an invocation:
if (invocation.Expression is MemberAccessExpressionSyntax outerAccess
    && outerAccess.Expression is InvocationExpressionSyntax innerInvocation)
{
    var innerResult = AnalyzeInvocation(innerInvocation, context);
    if (innerResult is not null)
    {
        var outerMethodName = GetMethodName(outerAccess);
        if (outerMethodName == "JsonName" && innerResult == ReceiverKind.ReferenceBuilder)
        {
            // Extract string arg and store with the reference key set by Reference()
            var jsonName = ExtractStringArgument(invocation);
            if (jsonName != null && context.CurrentReferenceKey != null)
                context.PropertyJsonNames[context.CurrentReferenceKey] = jsonName;
            context.CurrentReferenceKey = null;
            return ReceiverKind.ReferenceBuilder;
        }
    }
}
```

**Step 6: Handle split-statement pattern**

When `Reference()` is assigned to a local variable, the variable is registered as `ReceiverKind.ReferenceBuilder` (via `ProcessLocalDeclaration` returning the kind from `AnalyzeInvocation`). Then `r.JsonName("line")` matches:

```csharp
case "JsonName" when receiverKind == ReceiverKind.ReferenceBuilder:
    var jsonNameArg = ExtractStringArgument(invocation);
    if (jsonNameArg != null && context.CurrentReferenceKey != null)
        context.PropertyJsonNames[context.CurrentReferenceKey] = jsonNameArg;
    context.CurrentReferenceKey = null;
    return ReceiverKind.ReferenceBuilder;
```

> **Note:** `CurrentReferenceKey` must persist across statements when the Reference() call is assigned to a variable. The key is set when `Reference()` is processed (Step 4) and consumed when `JsonName()` is processed. For the split-statement pattern, the key survives because `ProcessLocalDeclaration` calls `AnalyzeInvocation` (setting the key) before `ProcessStatements` continues to the next statement (the `JsonName` call).

**Step 7: Wire PropertyJsonNameOverrides into returned model**

```csharp
PropertyJsonNameOverrides = context.PropertyJsonNames.ToImmutableDictionary(),
```

**Step 8: Run tests, commit**

```
feat: extend ConfigurationParser for Reference().JsonName() with cross-type chaining
```

---

### Task 5: Extend TypeGraphAnalyzer for [NormalizeJsonName] and rootTypeNeedsList

**Files:**
- Modify: `src/DataNormalizer.Generators/Analysis/TypeGraphAnalyzer.cs`
- Modify: `src/DataNormalizer.Generators/Models/TypeGraphNode.cs`
- Test: `tests/DataNormalizer.Generators.Tests/Analysis/TypeGraphAnalyzerTests.cs`

**Step 1: Write failing tests for [NormalizeJsonName]**

Tests:
- Property with `[NormalizeJsonName("line")]` → `AnalyzedProperty.JsonNameOverride == "line"`
- Property with both attribute `"x"` and config override `"y"` → `JsonNameOverride == "y"` (config wins)
- Property with attribute only → attribute value used
- Property with neither → `JsonNameOverride == null`

**Step 2: Write failing tests for rootTypeNeedsList**

Add `bool IsRootType { get; init; }` and `bool NeedsList { get; init; }` to `TypeGraphNode`.

Tests:
- Root type `Person` with `Address` reference (Address refs Person back) → root `NeedsList = true`
- Root type `Person` with `Address` reference (Address does NOT ref Person) → root `NeedsList = false`
- Non-root type `Address` → `NeedsList = true` (always, non-root types always need lists)

**Step 3: Read [NormalizeJsonName] attribute in TypeGraphAnalyzer**

When building `AnalyzedProperty`, check for the attribute:

```csharp
var jsonNameOverride = (string?)null;
var propKey = $"{typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}.{prop.Name}";
propKey = NormalizeFqn(propKey);
if (model.PropertyJsonNameOverrides.TryGetValue(propKey, out var configOverride))
    jsonNameOverride = configOverride;
else
{
    var jsonNameAttr = prop.GetAttributes().FirstOrDefault(a =>
        a.AttributeClass?.Name is "NormalizeJsonNameAttribute" or "NormalizeJsonName");
    if (jsonNameAttr?.ConstructorArguments.Length > 0)
        jsonNameOverride = jsonNameAttr.ConstructorArguments[0].Value as string;
}
```

**Step 4: Compute rootTypeNeedsList**

After the type graph is built, check if any non-root node's properties reference the root type:

```csharp
var rootFqn = rootNode.TypeFullName;
var rootNeedsList = allNodes.Any(n => n != rootNode &&
    n.Properties.Any(p =>
        (p.Kind == PropertyKind.Normalized || p.Kind == PropertyKind.Collection) &&
        p.TypeFullName == rootFqn));
```

Set `rootNode.NeedsList = rootNeedsList` and `rootNode.IsRootType = true`.

**Step 5: Run tests, commit**

```
feat: read [NormalizeJsonName] attribute and compute rootTypeNeedsList
```

---

### Task 6: Update ContainerEmitter for root property and collection JSON names

**Files:**
- Modify: `src/DataNormalizer.Generators/Emitters/ContainerEmitter.cs`
- Modify: `tests/DataNormalizer.Generators.Tests/Emitters/ContainerEmitterTests.cs`

**Step 1: Write failing tests**

Tests:
- Default container has `Result` property with `[JsonPropertyName("result")]` of root DTO type
- Root type does NOT appear in a list property (root `NeedsList = false`)
- Root type with `NeedsList = true` → BOTH `Result` property AND list property emitted
- `JsonContract.RootPropertyName = "searchResult"` → `[JsonPropertyName("searchResult")]` on Result
- `JsonContract.CollectionJsonNames["FQN"] = "routes"` → `[JsonPropertyName("routes")]` on that list
- Types without Collection override → default camelCase JSON name

**Step 2: Run tests to verify they fail**

**Step 3: Update ContainerEmitter.Emit signature**

```csharp
public static string Emit(
    TypeGraphNode rootNode,
    IReadOnlyList<TypeGraphNode> allNodes,
    NamingModel naming,
    JsonContractModel jsonContract)
```

**Step 4: Emit root property**

Always emit:

```csharp
var rootJsonName = jsonContract.RootPropertyName ?? "result";
// [JsonPropertyName("{rootJsonName}")]
// public {RootDtoFullName} Result { get; set; }
```

**Step 5: Conditional list emission**

For the root node: skip list property if `rootNode.NeedsList == false`.
For all other nodes: emit list property as before.

**Step 6: Collection JSON name overrides**

When emitting `[JsonPropertyName]` on a list property, check `jsonContract.CollectionJsonNames[node.TypeFullName]` first. If present, use it. Otherwise camelCase the CLR property name.

**Step 7: Run tests, commit**

```
feat: update ContainerEmitter with root property and collection JSON name overrides
```

---

### Task 7: Update DtoEmitter for JsonNameOverride

**Files:**
- Modify: `src/DataNormalizer.Generators/Emitters/DtoEmitter.cs`
- Modify: `tests/DataNormalizer.Generators.Tests/Emitters/DtoEmitterTests.cs`

**Step 1: Write failing tests**

Tests:
- Property with `JsonNameOverride = "line"` → `[JsonPropertyName("line")]` on `LineIndex`
- Property with `JsonNameOverride = "transitImages"` → `[JsonPropertyName("transitImages")]` on `TransitImagesIndices`
- Property with no override → default camelCase (`"lineIndex"`)
- Property with override when `EmitJsonPropertyNames = false` → still emit `[JsonPropertyName]` for that property (override is explicit)

**Step 2: Run tests to verify they fail**

**Step 3: Update DtoEmitter**

When emitting `[JsonPropertyName]`:
- If `prop.JsonNameOverride != null`: use override directly
- Else if `naming.EmitJsonPropertyNames`: camelCase the CLR property name
- Else: no attribute

**Step 4: Run tests, commit**

```
feat: update DtoEmitter to use JsonNameOverride for per-property JSON names
```

---

### Task 8: Update NormalizerEmitter for root property

**Files:**
- Modify: `src/DataNormalizer.Generators/Emitters/NormalizerEmitter.cs`
- Modify: `tests/DataNormalizer.Generators.Tests/Emitters/NormalizerEmitterTests.cs`

**Step 1: Write failing tests**

Tests:
- Generated normalizer sets `result.Result = rootArray[0]`
- Root with `NeedsList = false` → normalizer does NOT set a list property for root on container
- Root with `NeedsList = true` → normalizer sets BOTH `result.Result` AND `result.{rootListProp}`
- All other types → list property set as before

**Step 2: Run tests to verify they fail**

**Step 3: Update NormalizerEmitter**

In `EmitPublicNormalizeMethod`:
- Always emit `result.Result = __{rootCamel}Arr[0];`
- If `rootNode.NeedsList`: also emit `result.{rootListProp} = __{rootCamel}Arr;`
- If `!rootNode.NeedsList`: skip the root's list property assignment

**Step 4: Run tests, commit**

```
feat: update NormalizerEmitter to set Result property and conditional root list
```

---

### Task 9: Update DenormalizerEmitter for root property

**Files:**
- Modify: `src/DataNormalizer.Generators/Emitters/DenormalizerEmitter.cs`
- Modify: `tests/DataNormalizer.Generators.Tests/Emitters/DenormalizerEmitterTests.cs`

**Step 1: Write failing tests**

Tests:
- Denormalizer reads root from `normalized.Result` (not from `list[0]`)
- Root with `NeedsList = false` → no list read for root, create local `var roots = new[] { normalized.Result }` for reference resolution
- Root with `NeedsList = true` → read from `normalized.{rootListProp}` for reference resolution (Result is still the entry point)
- Denormalize returns the reconstructed root object

**Step 2: Run tests to verify they fail**

**Step 3: Update DenormalizerEmitter**

In `EmitDenormalizeMethod`:
- Read root: `var rootDto = normalized.Result;`
- If `!rootNode.NeedsList`: `var {rootPlural} = new[] { rootDto };` (local array for ref resolution)
- If `rootNode.NeedsList`: `var {rootPlural} = normalized.{rootListProp};` (read from list)
- Root resolution: use `rootDto` instead of `{rootPlural}[0]`
- All other types: unchanged

**Step 4: Run tests, commit**

```
feat: update DenormalizerEmitter to read from Result property
```

---

### Task 10: Wire JsonContractModel through NormalizeGenerator

**Files:**
- Modify: `src/DataNormalizer.Generators/NormalizeGenerator.cs`

**Step 1: Update ContainerEmitter call**

```csharp
ContainerEmitter.Emit(rootNode, nodes, model.Naming, model.JsonContract)
```

**Step 2: Ensure NormalizerEmitter and DenormalizerEmitter have access to rootNode.NeedsList and rootNode.IsRootType**

These are set by the TypeGraphAnalyzer (Task 5) and flow through the existing `allNodes` parameter.

**Step 3: Run all generator tests, commit**

```
feat: wire JsonContractModel through NormalizeGenerator
```

---

### Task 11: Update all existing tests for root property change

**Files (ALL need updating):**
- `tests/DataNormalizer.Integration.Tests/SimpleNormalizationTests.cs`
- `tests/DataNormalizer.Integration.Tests/BasicRoundtripTests.cs`
- `tests/DataNormalizer.Integration.Tests/ConfigFeatureTests.cs`
- `tests/DataNormalizer.Integration.Tests/CircularReferenceTests.cs`
- `tests/DataNormalizer.Integration.Tests/DeepNestingTests.cs`
- `tests/DataNormalizer.Integration.Tests/PerformanceTests.cs`
- `tests/DataNormalizer.Integration.Tests/SmokeTests.cs`
- `tests/DataNormalizer.Generators.Tests/GeneratorEndToEndTests.cs`
- `tests/DataNormalizer.Generators.Tests/Emitters/ContainerEmitterTests.cs`
- `tests/DataNormalizer.Generators.Tests/Emitters/NormalizerEmitterTests.cs`
- `tests/DataNormalizer.Generators.Tests/Emitters/DenormalizerEmitterTests.cs`
- `samples/DataNormalizer.Samples/Program.cs`

**Step 1: Integration tests**

All tests that access `container.{RootType}Dtos[0]` → change to `container.Result`.
All tests that check container has a root list → remove that assertion (or keep if root is referenced).

**Step 2: E2E tests**

Update generated code assertions to expect `Result` property.

**Step 3: Samples**

Update `Program.cs` to use `container.Result` instead of `container.{RootType}Dtos[0]`.

**Step 4: Run all tests, commit**

```
test: update all tests and samples for root property container shape
```

---

### Task 12: Add integration tests for full search-response use case

**Files:**
- Create: `tests/DataNormalizer.Integration.Tests/TestTypes/Search/` (types mirroring search-response.json)
- Create: `tests/DataNormalizer.Integration.Tests/SearchNormalizationConfig.cs`
- Create: `tests/DataNormalizer.Integration.Tests/SearchContractTests.cs`

**Step 1: Create test types**

In `TestTypes/Search/` namespace:
- `SearchResponse` (root): `List<SearchRoute> Routes`, `List<SearchPlace> Places`, `SearchPlace OriginPlace`, `SearchPlace DestinationPlace`
- `SearchRoute`: `List<SearchSegment> Segments`, `List<SearchPlace> Places`
- `SearchSegment`: `List<SearchOption> Options`
- `SearchOption`: `List<SearchHop> Hops`
- `SearchHop`: `SearchLine Line`, `SearchCarrier? MarketingCarrier`, `SearchVehicle Vehicle`, `List<SearchImage> TransitImages`
- `SearchLine`: `List<SearchPlace> Places`, `string? Path`
- `SearchPlace`: `string ShortName`, `string? Kind`, `double Lat`, `double Lng`
- `SearchCarrier`: `string Name`, `string Code`
- `SearchVehicle`: `string Name`, `string Kind`
- `SearchImage`: `string Title`, `string ThumbnailUrl`

**Step 2: Create SearchNormalizationConfig**

```csharp
[NormalizeConfiguration]
public partial class SearchNormalizationConfig : NormalizationConfig
{
    protected override void Configure(NormalizeBuilder builder)
    {
        builder.NormalizeGraph<SearchResponse>(graph =>
        {
            graph.UseJsonContract(c =>
            {
                c.RootPropertyName = "result";
                c.Collection<SearchRoute>("routes");
                c.Collection<SearchSegment>("segments");
                c.Collection<SearchOption>("options");
                c.Collection<SearchHop>("hops");
                c.Collection<SearchLine>("lines");
                c.Collection<SearchPlace>("places");
                c.Collection<SearchCarrier>("carriers");
                c.Collection<SearchVehicle>("vehicles");
                c.Collection<SearchImage>("transitImages");
            });
        });

        builder.ForType<SearchHop>(x =>
        {
            x.Reference(p => p.Line).JsonName("line");
            x.Reference(p => p.MarketingCarrier).JsonName("marketingCarrier");
            x.Reference(p => p.Vehicle).JsonName("vehicle");
            x.ReferenceCollection(p => p.TransitImages).JsonName("transitImages");
        });
    }
}
```

**Step 3: Write tests**

- Normalize a SearchResponse graph with routes, hops, shared places, shared carriers
- Serialize to JSON
- Verify JSON has `"result"` (not `"searchResponseDtos"`)
- Verify JSON has `"routes"`, `"lines"`, `"places"`, `"carriers"` (custom names)
- Verify JSON has `"hops"` with `"line"`, `"marketingCarrier"`, `"vehicle"` (per-property overrides)
- Verify `"transitImages"` on hops (collection reference override)
- Verify no `"searchResponseDtos"` array in JSON (root excluded from list)
- Denormalize and verify roundtrip: same data back

**Step 4: Write test for [NormalizeJsonName] attribute**

Add `[NormalizeJsonName("origin")]` on `SearchResponse.OriginPlace`. Verify JSON shows `"origin"` not `"originPlaceIndex"`.

**Step 5: Write test for attribute + config priority**

Add both `[NormalizeJsonName("orig")]` on property AND `Reference().JsonName("origin")` in config. Verify config wins → `"origin"`.

**Step 6: Write test for root type referenced by other types**

Create a scenario where the root type IS referenced (e.g., `SearchPlace` as root, with `SearchLine.Places` referencing it). Verify container has both `Result` and `PlaceDtos` list.

**Step 7: Run tests, commit**

```
test: add full search-response integration tests for JSON contract customization
```

---

### Task 13: Add compilation checks to emitter unit tests

The emitter unit tests use string matching (`Does.Contain`) to verify generated code. This catches content but not validity -- a test can pass while the generated code has syntax errors, missing usings, or broken type references. Fix this by compiling the emitter output in each unit test using Roslyn.

**Files:**
- Create: `tests/DataNormalizer.Generators.Tests/Emitters/EmitterCompilationHelper.cs`
- Modify: `tests/DataNormalizer.Generators.Tests/Emitters/DtoEmitterTests.cs`
- Modify: `tests/DataNormalizer.Generators.Tests/Emitters/ContainerEmitterTests.cs`
- Modify: `tests/DataNormalizer.Generators.Tests/Emitters/NormalizerEmitterTests.cs`
- Modify: `tests/DataNormalizer.Generators.Tests/Emitters/DenormalizerEmitterTests.cs`

**Step 1: Create EmitterCompilationHelper**

Shared helper that compiles one or more generated source strings and asserts zero errors:

```csharp
internal static class EmitterCompilationHelper
{
    private static readonly MetadataReference[] References = new[]
    {
        MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
        MetadataReference.CreateFromFile(typeof(System.Text.Json.Serialization.JsonPropertyNameAttribute).Assembly.Location),
        // Add other required references (System.Runtime, System.Collections, etc.)
    };

    public static void AssertCompiles(params string[] sources)
    {
        var trees = sources.Select(s => CSharpSyntaxTree.ParseText(s)).ToArray();
        var compilation = CSharpCompilation.Create(
            "EmitterTest",
            trees,
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var diagnostics = compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToArray();
        Assert.That(diagnostics, Is.Empty,
            $"Generated code has compilation errors:\n{string.Join("\n", diagnostics.Select(d => d.ToString()))}");
    }
}
```

**Step 2: Add compilation assertions to existing tests**

For each emitter's "main" test case, add a call to `EmitterCompilationHelper.AssertCompiles(result)` after the existing string assertions. The compilation check runs on the same output string -- no new test methods needed, just an additional assertion per test.

For tests that emit multiple related files (e.g., container + DTOs), pass all sources together so cross-file references resolve:

```csharp
var dtoResult = DtoEmitter.Emit(personNode, false, naming);
var containerResult = ContainerEmitter.Emit(personNode, allNodes, naming, jsonContract);
EmitterCompilationHelper.AssertCompiles(dtoResult, containerResult);
```

**Step 3: Write tests for compilation of generated code with JsonNameOverride**

- Emit a DTO with `JsonNameOverride` properties → compiles
- Emit a container with `RootPropertyName` and collection overrides → compiles
- Emit normalizer + denormalizer with root property → compiles together

**Step 4: Run tests, commit**

```
feat: add compilation checks to emitter unit tests
```

---

### Task 14: Add unparsed config statement diagnostics

When the parser encounters a statement inside a known builder lambda that it cannot parse, it should emit a compiler error. This makes the parser's limitations visible at build time rather than silently producing wrong defaults.

**Files:**
- Modify: `src/DataNormalizer.Generators/Analysis/ConfigurationParser.cs`
- Create: `src/DataNormalizer.Generators/DiagnosticDescriptors.cs` (or modify if exists)
- Modify: `src/DataNormalizer.Generators/Models/NormalizationModel.cs`
- Test: `tests/DataNormalizer.Generators.Tests/Analysis/ConfigurationParserTests.cs`

**Step 1: Define the diagnostic descriptor**

```csharp
internal static class DiagnosticDescriptors
{
    public static readonly DiagnosticDescriptor UnparsedConfigStatement = new(
        id: "DN1001",
        title: "Unparsed configuration statement",
        messageFormat: "Configuration statement could not be parsed and will be ignored: '{0}'",
        category: "DataNormalizer",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}
```

**Step 2: Add diagnostics collection to ParseContext**

```csharp
public List<Diagnostic> Diagnostics { get; } = new();
```

**Step 3: Track which lambdas are "known builder lambdas"**

Add a counter or flag to ParseContext: `public int BuilderLambdaDepth { get; set; }`. Increment when entering a builder lambda body (UseNaming, UseJsonContract, ForType, NormalizeGraph), decrement when leaving. Any statement inside a builder lambda that falls through to `default` in `ProcessStatements` should produce a diagnostic.

**Step 4: Emit diagnostic for unrecognized statements**

In `ProcessStatements`, after all case branches:

```csharp
default:
    if (context.BuilderLambdaDepth > 0)
    {
        context.Diagnostics.Add(Diagnostic.Create(
            DiagnosticDescriptors.UnparsedConfigStatement,
            statement.GetLocation(),
            statement.ToFullString().Trim()));
    }
    break;
```

**Step 5: Surface diagnostics in NormalizationModel and NormalizeGenerator**

Add `ImmutableArray<Diagnostic> Diagnostics` to `NormalizationModel`. In `NormalizeGenerator.Execute`, report them via `context.ReportDiagnostic()`.

**Step 6: Write failing tests**

Tests:
- `n.DtoSuffix = GetSuffix()` (non-literal RHS) → error DN1001
- `if (true) { n.DtoSuffix = "Dto"; }` (conditional) → error DN1001 on the `if` statement
- `Console.WriteLine("debug")` inside builder lambda → error DN1001
- Valid statements (`n.DtoSuffix = "Dto"`, `graph.Inline<T>()`, `x.Reference(p => p.Line).JsonName("line")`) → no diagnostic
- Statements outside builder lambdas (in Configure method body, not inside any lambda) → no diagnostic (user might have helper code)

**Step 7: Run tests, commit**

```
feat: emit DN1001 error for unparsed configuration statements
```

---

### Task 15: Final build and format check

**Step 1:** `dotnet build --no-restore` → 0 errors
**Step 2:** `dotnet csharpier check .` → all formatted
**Step 3:** `dotnet test --no-restore` → all pass
**Step 4:** Commit any formatting fixes

```
style: format code with CSharpier
```
