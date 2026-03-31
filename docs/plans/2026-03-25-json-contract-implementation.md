# JSON Contract Customization Implementation Plan (Phase 2)

> **For Agent:** REQUIRED SUB-SKILL: Use executing-plans to implement this plan task-by-task.

**Goal:** Add root property redesign, `UseJsonContract`, `Reference().JsonName()`, and `[NormalizeJsonName]` attribute so generated containers match existing API wire formats.

**Architecture:** Builds on Phase 1's NamingModel and naming-aware emitters. Adds `JsonContractModel` for per-graph contract config, `AnalyzedProperty.JsonNameOverride` for per-property JSON name overrides, and a `rootTypeNeedsList` compile-time check in the type graph. Container emitter gains a `Result` property and conditional list emission. Parser gains cross-type chain handling for `Reference().JsonName()`.

**Tech Stack:** C# 12, .NET source generators (Roslyn), NUnit 4, System.Text.Json

**Prerequisite:** Phase 1 (naming policy) must be complete. This plan assumes `NamingModel`, `ProcessAssignment`, and naming-aware emitters are already in place.

**Key design decisions:**
- Every container always has a named `Result` property (default JSON name `"result"`)
- If `RootPropertyName` is set to empty string `""`, fall back to `"result"` (treat empty as unset)
- If `[NormalizeJsonName("")]` is set to empty string, treat as null (no override)
- Root type has no list array unless it is referenced by other types in the graph (compile-time check)
- Config `Reference().JsonName()` overrides `[NormalizeJsonName]` attribute when both exist
- `UseJsonContract` provides per-graph collection JSON name overrides and root property name customization
- Duplicate `Collection<T>()` calls for the same type `T` within a single `UseJsonContract` lambda produce a diagnostic error (DN1002)

**Cross-task design constraints:**
- **FQN normalization:** `NormalizeFqn` must be extracted to a shared `internal static` utility class (`FqnHelper`) accessible to both `ConfigurationParser` and `TypeGraphAnalyzer`. The key format `"{typeFqn}.{propName}"` must use `SymbolDisplayFormat.FullyQualifiedFormat` in both places.
- **Signature changes:** When a task changes a method signature, it must update ALL call sites (production code AND tests) in the same task. No temporary overloads.
- **Test helper updates:** Tasks 5-9 all add new properties to `TypeGraphNode` (`IsRootType`, `NeedsList`). The `CreateNode` / `ModelFactories` test helpers must be updated to accept these new fields. Existing test call sites must be updated to pass the correct values for their scenario.

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
        // IMPORTANT: Use XOR-based order-independent hashing for dictionary entries.
        // ImmutableDictionary iteration order is not contractually guaranteed,
        // so (hash * 397) ^ entry-by-entry would be order-dependent.
        var hash = RootPropertyName?.GetHashCode() ?? 0;
        var dictHash = 0;
        foreach (var kvp in CollectionJsonNames)
        {
            dictHash ^= kvp.Key.GetHashCode() ^ (kvp.Value.GetHashCode() * 397);
        }
        return (hash * 397) ^ dictHash;
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
- Both `RootPropertyName` null → `Equals` true
- One null, one non-null `RootPropertyName` → `Equals` false
- Same count, different keys → `Equals` false, `GetHashCode` differs
- Same count, different values → `Equals` false
- Empty vs non-empty `CollectionJsonNames` → `Equals` false
- `Default` equality with `new()` → true
- `GetHashCode` consistency: equal objects → equal hashes (test with multi-entry dictionaries)
- `GetHashCode` order-independence: dictionaries constructed in different insertion order → same hash

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
- Test: `tests/DataNormalizer.Tests/Configuration/ConfigurationTests.cs` (modify existing)
- Test: `tests/DataNormalizer.Tests/Attributes/AttributeTests.cs` (modify existing)

> **Note:** These classes are syntactic markers — the source generator reads them via Roslyn syntax analysis, never at runtime. Their bodies are intentionally empty/no-op. Tests verify the public API surface exists with correct signatures.

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

> **Note:** These return `ReferenceBuilder`, not `TypeBuilder<T>`, intentionally breaking the fluent TypeBuilder chain. This matches the source generator's receiver tracking — after `Reference()`, only `ReferenceBuilder` methods (like `.JsonName()`) are valid.

**Step 6: Write unit tests**

Tests for `JsonContractBuilder` (in ConfigurationTests.cs, matching existing builder test patterns):
- `RootPropertyName` get/set roundtrip
- `Collection<T>()` does not throw (syntactic marker)
- `Collection<T>()` with various types compiles and runs

Tests for `ReferenceBuilder`:
- `JsonName()` returns same instance (fluent)
- `JsonName()` can be chained

Tests for `NormalizeJsonNameAttribute`:
- Constructor stores name in `Name` property
- `Name` returns the constructor value
- `AttributeUsage` targets `Property` only
- `Inherited` is false

Tests for `GraphBuilder.UseJsonContract`:
- Returns same `GraphBuilder` instance (fluent)
- Lambda is invoked

Tests for `TypeBuilder.Reference` / `ReferenceCollection`:
- Returns `ReferenceBuilder` instance
- Lambda expression compiles with property selector

**Step 7: Run tests, commit**

```
feat: add JsonContractBuilder, ReferenceBuilder, NormalizeJsonNameAttribute, and builder methods
```

---

### Task 3: Extend ConfigurationParser for UseJsonContract

**Prerequisites:** Task 1 (JsonContractModel), Task 2 (runtime classes)

**Files:**
- Modify: `src/DataNormalizer.Generators/Diagnostics/DiagnosticDescriptors.cs` (add DN1002)
- Modify: `src/DataNormalizer.Generators/Analysis/ConfigurationParser.cs`
- Test: `tests/DataNormalizer.Generators.Tests/Analysis/ConfigurationParserTests.cs`

**Step 0: Define DN1002 diagnostic descriptor**

Add to the EXISTING `Diagnostics/DiagnosticDescriptors.cs`:

```csharp
public static readonly DiagnosticDescriptor DuplicateCollectionType = new(
    id: "DN1002",
    title: "Duplicate Collection<T> type",
    messageFormat: "Collection<{0}> was already configured in this UseJsonContract block. Only the last value will be used.",
    category: "DataNormalizer",
    defaultSeverity: DiagnosticSeverity.Error,
    isEnabledByDefault: true);
```

Also add a `List<GeneratorDiagnosticInfo> Diagnostics` field to `ParseContext` if not already present, using the existing `GeneratorDiagnosticInfo` type (check the codebase for this type's constructor signature — it likely takes `(string Id, string TypeName)` or similar). For DN1002, store:

```csharp
context.Diagnostics.Add(new GeneratorDiagnosticInfo("DN1002", normalizedFqn));
```

> **IMPORTANT:** Do NOT use `invocation.GetLocation()` or `Diagnostic.Create()` in ParseContext. Store only serializable data. Diagnostics are surfaced in `NormalizeGenerator`'s `RegisterSourceOutput` callback.

**Step 1: Write failing tests**

Tests:
- `graph.UseJsonContract(c => { c.RootPropertyName = "result"; })` → model has `JsonContract.RootPropertyName == "result"`
- `graph.UseJsonContract(c => { c.Collection<SearchLine>("lines"); })` → `JsonContract.CollectionJsonNames` has entry
- Multiple `Collection<T>()` calls with different types → all stored
- `RootPropertyName` with empty string → stored as empty string (emitter handles fallback)
- No `UseJsonContract` → `JsonContractModel.Default`
- `UseJsonContract` with both `RootPropertyName` and `Collection<T>()` calls in same lambda
- Empty lambda body `UseJsonContract(c => { })` → no crash, `JsonContractModel.Default`
- `UseJsonContract` called twice on same graph → last-wins (matching `UseNaming` pattern)
- `Collection<T>(someVariable)` with non-literal string arg → silently skipped (no crash)
- `Collection<T>()` where T is a type from a different namespace → FQN stored correctly
- Duplicate `Collection<T>()` for same type T → diagnostic error DN1002

**Step 2: Run tests to verify they fail**

**Step 3: Create `ExtractStringArgument` helper**

This helper is used by both Task 3 and Task 4. Add it to ConfigurationParser:

```csharp
private static string? ExtractStringArgument(InvocationExpressionSyntax invocation)
{
    var args = invocation.ArgumentList.Arguments;
    if (args.Count > 0 && args[0].Expression is LiteralExpressionSyntax literal
        && literal.IsKind(SyntaxKind.StringLiteralExpression))
        return literal.Token.ValueText;
    return null;
}
```

**Step 4: Add JsonContractBuilder ReceiverKind**

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

**Step 5: Add JsonContract fields to ParseContext**

```csharp
public string? RootPropertyName { get; set; }
public Dictionary<string, string> CollectionJsonNames { get; } = new();
public HashSet<string> SeenCollectionTypes { get; } = new(); // for duplicate detection
```

**Step 6: Add UseJsonContract handler in AnalyzeInvocation**

```csharp
case "UseJsonContract" when receiverKind == ReceiverKind.GraphBuilder:
    ProcessJsonContractLambda(invocation, context);
    return ReceiverKind.GraphBuilder;
```

`ProcessJsonContractLambda`: extract lambda param, register as `ReceiverKind.JsonContractBuilder`, call `ProcessStatements` on body.

**Step 7: Handle Collection<T>() method call on JsonContractBuilder**

```csharp
case "Collection" when receiverKind == ReceiverKind.JsonContractBuilder:
    // Extract type argument FQN and string argument
    var typeFqn = GetTypeArgumentSymbol(memberAccess, context.SemanticModel);
    var jsonName = ExtractStringArgument(invocation);
    if (typeFqn != null && jsonName != null)
    {
        var normalizedFqn = NormalizeFqn(typeFqn.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
        if (!context.SeenCollectionTypes.Add(normalizedFqn))
        {
            // Duplicate Collection<T> for same T — store serializable diagnostic info
            context.Diagnostics.Add(new GeneratorDiagnosticInfo("DN1002", normalizedFqn));
        }
        context.CollectionJsonNames[normalizedFqn] = jsonName;
    }
    return ReceiverKind.JsonContractBuilder;
```

**Step 8: Handle RootPropertyName assignment on JsonContractBuilder**

Phase 1 already added `ProcessAssignment`. **The existing guard `if (receiverKind != ReceiverKind.NamingBuilder) return;` must be restructured** to use a switch or multi-condition check that also accepts `ReceiverKind.JsonContractBuilder`:

```csharp
switch (receiverKind)
{
    case ReceiverKind.NamingBuilder:
        // existing NamingBuilder assignment handling...
        break;
    case ReceiverKind.JsonContractBuilder:
        if (propertyName == "RootPropertyName" && assignment.Right is LiteralExpressionSyntax rootLit)
            context.RootPropertyName = rootLit.Token.ValueText;
        break;
    default:
        return; // unknown receiver, skip
}
```

**Step 9: Wire into returned NormalizationModel**

```csharp
JsonContract = new JsonContractModel
{
    RootPropertyName = context.RootPropertyName,
    CollectionJsonNames = context.CollectionJsonNames.ToImmutableDictionary(),
},
```

**Step 10: Run tests, commit**

```
feat: extend ConfigurationParser to parse UseJsonContract
```

---

### Task 4: Extend ConfigurationParser for Reference().JsonName()

This requires handling **cross-type method chaining** where `x.Reference(p => p.Line).JsonName("line")` transitions from TypeBuilder to ReferenceBuilder.

**Prerequisites:** Task 3 (ExtractStringArgument, ReceiverKind.ReferenceBuilder)

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
- Consecutive `Reference()` calls without `JsonName()`: `x.Reference(p => p.A); x.Reference(p => p.B).JsonName("b")` → only B stored, A silently dropped (key overwritten)
- `Reference()` then `Reference().JsonName()`: `x.Reference(p => p.A); x.Reference(p => p.B).JsonName("b")` → stores only B
- `JsonName("")` with empty string → stored as empty string (emitter treats as null per design decision)
- `JsonName(someVariable)` with non-literal arg → silently skipped
- Two separate `ForType` blocks with `Reference().JsonName()` → isolated, both stored correctly

**Step 2: Run tests to verify they fail**

**Step 3: Add to ParseContext**

```csharp
public Dictionary<string, string> PropertyJsonNames { get; } = new();
public string? CurrentReferenceKey { get; set; }
```

> **Limitation:** `CurrentReferenceKey` is a single mutable field. The pattern `var r1 = x.Reference(A); var r2 = x.Reference(B); r1.JsonName("a");` would store "a" against B's key, not A's. This is a known limitation of the split-statement pattern — the last `Reference()` call wins. Document this in generated API docs. The fluent chaining pattern `x.Reference(A).JsonName("a")` is the recommended usage.

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

**IMPORTANT:** This must be integrated into the existing invocation-as-receiver handling. The current code uses `GetUltimateReceiverName` which walks through invocations to the ultimate identifier — this would bypass our cross-type chain detection because it would resolve `x.Reference(p => p.Line).JsonName("line")` to `x` (the TypeBuilder), not to the `Reference()` call.

The fix: **check for cross-type chaining BEFORE `GetUltimateReceiverName`** is called. Insert this block at the top of `AnalyzeInvocation`, before the existing receiver resolution logic:

```csharp
// CROSS-TYPE CHAIN DETECTION: must run before GetUltimateReceiverName
// which would walk through the inner invocation and lose the cross-type transition.
if (invocation.Expression is MemberAccessExpressionSyntax outerAccess
    && outerAccess.Expression is InvocationExpressionSyntax innerInvocation)
{
    var outerMethodName = GetMethodName(outerAccess);

    // Check if inner call returns a different receiver type than the outer expects
    if (outerMethodName == "JsonName")
    {
        // Process the inner invocation first to set CurrentReferenceKey
        var innerResult = AnalyzeInvocation(innerInvocation, context);
        if (innerResult == ReceiverKind.ReferenceBuilder)
        {
            var jsonName = ExtractStringArgument(invocation);
            if (jsonName != null && context.CurrentReferenceKey != null)
                context.PropertyJsonNames[context.CurrentReferenceKey] = jsonName;
            context.CurrentReferenceKey = null;
            return ReceiverKind.ReferenceBuilder;
        }
        // If inner wasn't ReferenceBuilder, fall through to normal handling
    }
}
// ... existing GetUltimateReceiverName logic continues below ...
```

> **Why before GetUltimateReceiverName:** The existing method resolves `a.B().C()` to the ultimate identifier `a`, losing the fact that `B()` returned a different type than `a`. By intercepting the `JsonName` case first, we can correctly detect the `Reference() → JsonName()` transition without disrupting existing same-type chains.

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

**Prerequisites:** Task 1 (JsonNameOverride on AnalyzedProperty, PropertyJsonNameOverrides on NormalizationModel)

**Files:**
- Create: `src/DataNormalizer.Generators/Helpers/FqnHelper.cs`
- Modify: `src/DataNormalizer.Generators/Analysis/TypeGraphAnalyzer.cs`
- Modify: `src/DataNormalizer.Generators/Analysis/ConfigurationParser.cs` (extract NormalizeFqn)
- Modify: `src/DataNormalizer.Generators/Models/TypeGraphNode.cs`
- Modify: Test helper `CreateNode` / `ModelFactories` to accept `isRootType` and `needsList` with defaults
- Test: `tests/DataNormalizer.Generators.Tests/Analysis/TypeGraphAnalyzerTests.cs`

**Step 1: Extract FqnHelper**

Move `NormalizeFqn` from `ConfigurationParser` to a shared helper:

```csharp
// src/DataNormalizer.Generators/Helpers/FqnHelper.cs
namespace DataNormalizer.Generators.Helpers;

internal static class FqnHelper
{
    public static string NormalizeFqn(string fqn) { /* existing implementation */ }

    public static string BuildPropertyKey(string typeFqn, string propertyName)
        => NormalizeFqn($"{typeFqn}.{propertyName}");
}
```

Update `ConfigurationParser` to call `FqnHelper.NormalizeFqn` and `FqnHelper.BuildPropertyKey` instead of its own private method.

**Step 2: Write failing tests for [NormalizeJsonName]**

Tests:
- Property with `[NormalizeJsonName("line")]` → `AnalyzedProperty.JsonNameOverride == "line"`
- Property with both attribute `"x"` and config override `"y"` → `JsonNameOverride == "y"` (config wins)
- Property with attribute only → attribute value used
- Property with neither → `JsonNameOverride == null`
- Property with `[NormalizeJsonName("")]` → `JsonNameOverride == null` (empty string treated as unset)
- `[NormalizeJsonName]` on a Normalized property (reference) → override applies
- `[NormalizeJsonName]` on a Collection property (collection reference) → override applies
- FQN key consistency: verify `FqnHelper.BuildPropertyKey` output matches ConfigurationParser's PropertyJsonNameOverrides keys (integration-style test with a known type)

> **Note:** The test source must include an inline attribute definition for `[NormalizeJsonName]` since the source generator test compilation doesn't reference the runtime library.

**Step 3: Write failing tests for rootTypeNeedsList**

Add `bool IsRootType { get; init; }` and `bool NeedsList { get; init; }` to `TypeGraphNode`.

> **IMPORTANT:** `TypeGraphNode` is a `sealed class`, NOT a `record`. The `with` expression is not available. You must construct new `TypeGraphNode` instances explicitly, copying all existing properties and setting the new ones. See Step 7 for the construction pattern.

Tests:
- Root type `Person` with `Address` reference where Address has `Person` back-reference → root `NeedsList = true`
- Root type `Person` with `Address` reference where Address does NOT ref Person → root `NeedsList = false`
- Non-root type `Address` → `NeedsList = true` (always, non-root types always need lists)
- Root type referenced via a **Collection** property (`Address` has `List<Person> Residents`) → root `NeedsList = true` (must check `CollectionElementTypeFullName`, not `TypeFullName`)
- **Self-referencing root type** (`TreeNode` has `TreeNode? Parent`) → root `NeedsList = true` (self-references count)
- Multiple non-root types referencing root → root `NeedsList = true`

**Step 4: Update test helpers**

Update `CreateNode` / `ModelFactories` to accept `isRootType` and `needsList` parameters:

```csharp
public static TypeGraphNode CreateNode(
    ...,
    bool isRootType = false,
    bool needsList = true)
```

Update ALL existing test call sites in `TypeGraphAnalyzerTests` to pass correct values for their scenario.

**Step 5: Change TypeGraphAnalyzer.Analyze signature**

Change the `Analyze` signature directly — no overloads. Update ALL call sites in this task:

```csharp
public static IReadOnlyList<TypeGraphNode> Analyze(
    INamedTypeSymbol rootType,
    NormalizationModel model,  // NEW — needed for PropertyJsonNameOverrides
    ...)
```

Update the call site in `NormalizeGenerator.cs` to pass `model`:
```csharp
var typeGraph = TypeGraphAnalyzer.Analyze(rootTypeSymbol, model, ...);
```

Update ALL existing `TypeGraphAnalyzerTests` to pass `NormalizationModel.Default` (or a model with `PropertyJsonNameOverrides` for the new override tests).

> **Note:** Check existing code for the actual return type — it may be a custom wrapper class. Use whatever the existing method returns.

**Step 6: Read [NormalizeJsonName] attribute in TypeGraphAnalyzer**

When building `AnalyzedProperty`, check for the attribute. Use the correct parameter name (check existing code — may be `type` not `typeSymbol`):

```csharp
var jsonNameOverride = (string?)null;
var propKey = FqnHelper.BuildPropertyKey(
    type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), prop.Name);
if (model.PropertyJsonNameOverrides.TryGetValue(propKey, out var configOverride))
    jsonNameOverride = configOverride;
else
{
    var jsonNameAttr = prop.GetAttributes().FirstOrDefault(a =>
        a.AttributeClass?.Name == "NormalizeJsonNameAttribute");
    if (jsonNameAttr?.ConstructorArguments.Length > 0)
    {
        var attrValue = jsonNameAttr.ConstructorArguments[0].Value as string;
        jsonNameOverride = string.IsNullOrEmpty(attrValue) ? null : attrValue;
    }
}
```

> **Note:** Match attribute by `"NormalizeJsonNameAttribute"` only (not `"NormalizeJsonName"` — Roslyn `AttributeClass.Name` always includes the full suffix).

Add `JsonNameOverride = jsonNameOverride` to ALL `new AnalyzedProperty(...)` construction sites in the analyzer (there are ~9 of them). Search for `new AnalyzedProperty` and ensure every one includes the new property.

**Step 7: Compute rootTypeNeedsList**

After the type graph is built, check if ANY node's properties reference the root type. **Do not exclude the root node itself** (self-references count):

```csharp
var rootFqn = rootNode.TypeFullName;
var rootNeedsList = allNodes.Any(n =>
    n.Properties.Any(p =>
    {
        if (p.Kind == PropertyKind.Normalized)
            return p.TypeFullName == rootFqn;
        if (p.Kind == PropertyKind.Collection)
            return p.CollectionElementTypeFullName == rootFqn;  // NOT p.TypeFullName (that's List<T>)
        return false;
    })
    && (n != rootNode || n.Properties.Any(p =>  // self-ref: root refs itself
        (p.Kind == PropertyKind.Normalized && p.TypeFullName == rootFqn) ||
        (p.Kind == PropertyKind.Collection && p.CollectionElementTypeFullName == rootFqn)))
);
```

> **IMPORTANT:** For `PropertyKind.Collection`, `p.TypeFullName` is the full list type (e.g., `System.Collections.Generic.List<Person>`), NOT the element type. You must use `p.CollectionElementTypeFullName` (or the equivalent property name in the codebase) to get the element type for comparison.

Construct final nodes with computed values. `TypeGraphNode` is a `sealed class` — create new instances explicitly:

```csharp
// TypeGraphNode is a sealed class, NOT a record — cannot use `with` expression.
// Create new root node copying all properties and setting IsRootType + NeedsList:
var finalRootNode = new TypeGraphNode
{
    TypeFullName = rootNode.TypeFullName,
    TypeShortName = rootNode.TypeShortName,
    Properties = rootNode.Properties,
    // ... copy all other existing properties ...
    IsRootType = true,
    NeedsList = rootNeedsList,
};
// Replace rootNode in the returned list with finalRootNode.
// For non-root nodes, set IsRootType = false, NeedsList = true (always need lists).
```

**Step 8: Run tests, commit**

```
feat: read [NormalizeJsonName] attribute, extract FqnHelper, and compute rootTypeNeedsList
```

---

### Task 6: Update ContainerEmitter for root property and collection JSON names

**Prerequisites:** Task 1 (JsonContractModel), Task 5 (IsRootType, NeedsList on TypeGraphNode)

**Files:**
- Modify: `src/DataNormalizer.Generators/Emitters/ContainerEmitter.cs`
- Modify: `tests/DataNormalizer.Generators.Tests/Emitters/ContainerEmitterTests.cs`

**Step 1: Write failing tests**

Tests:
- Default container has `Result` property with `[JsonPropertyName("result")]` of the correct root DTO type (verify the full type name)
- Root type with `NeedsList = false` → NO list property for root type in container
- Root type with `NeedsList = true` → BOTH `Result` property AND list property emitted
- `JsonContract.RootPropertyName = "searchResult"` → `[JsonPropertyName("searchResult")]` on Result
- `JsonContract.RootPropertyName = ""` → `[JsonPropertyName("result")]` (empty string falls back to default)
- `JsonContract.CollectionJsonNames["FQN"] = "routes"` → `[JsonPropertyName("routes")]` on that list
- `CollectionJsonNames` key that doesn't match any node → no crash, no effect
- Types without Collection override → default camelCase JSON name
- Result `[JsonPropertyName("result")]` is ALWAYS emitted even when `EmitJsonPropertyNames = false` (Result is structural, not user-controlled)
- Multiple non-root types with various `CollectionJsonNames` → all applied correctly
- Result property type is `{RootDtoFullName}` with `{ get; set; }` and initialized to `default!`

**Step 2: Run tests to verify they fail**

**Step 3: Change ContainerEmitter.Emit signature**

Change the signature directly — no overloads:

```csharp
public static string Emit(
    TypeGraphNode rootNode,
    IReadOnlyList<TypeGraphNode> allNodes,
    NamingModel naming,
    JsonContractModel jsonContract)
```

Update the call site in `NormalizeGenerator.cs`:
```csharp
ContainerEmitter.Emit(rootNode, allNodes, model.Naming, model.JsonContract)
```
> **IMPORTANT:** Check the actual variable names in `NormalizeGenerator.cs` at the call site. Use whatever the existing code uses.

Update ALL existing `ContainerEmitterTests` call sites (there are ~16) to pass `JsonContractModel.Default` as the 4th argument.

**Step 4: Update test helpers and existing tests**

Update test helper `CreateNode` calls to include `isRootType` / `needsList` where needed. Existing tests that assert root list behavior must pass `needsList: true` on the root node.

**Step 5: Emit root property**

Always emit:

```csharp
var rootJsonName = string.IsNullOrEmpty(jsonContract.RootPropertyName)
    ? "result"
    : jsonContract.RootPropertyName;
// [JsonPropertyName("{rootJsonName}")]
// public {RootDtoFullName} Result { get; set; } = default!;
```

**Step 6: Conditional list emission**

For the root node: skip list property if `rootNode.NeedsList == false`.
For all other nodes: emit list property as before.

**Step 7: Collection JSON name overrides**

When emitting `[JsonPropertyName]` on a list property, check `jsonContract.CollectionJsonNames[node.TypeFullName]` first. If present, use it. Otherwise camelCase the CLR property name. Ensure the key format matches what `ConfigurationParser` produces (via `FqnHelper.NormalizeFqn`).

**Step 8: Run tests, commit**

```
feat: update ContainerEmitter with root property and collection JSON name overrides
```

---

### Task 7: Update DtoEmitter for JsonNameOverride

**Prerequisites:** Task 1 (JsonNameOverride on AnalyzedProperty)

**Files:**
- Modify: `src/DataNormalizer.Generators/Emitters/DtoEmitter.cs`
- Modify: `tests/DataNormalizer.Generators.Tests/Emitters/DtoEmitterTests.cs`

**Step 1: Write failing tests**

Tests (specify PropertyKind for each):
- `PropertyKind.Normalized` property with `JsonNameOverride = "line"` → `[JsonPropertyName("line")]` on the index property
- `PropertyKind.Collection` property with `JsonNameOverride = "transitImages"` → `[JsonPropertyName("transitImages")]` on the indices array property
- `PropertyKind.Simple` property with `JsonNameOverride = "name"` → `[JsonPropertyName("name")]`
- `PropertyKind.Inlined` property with `JsonNameOverride = "details"` → `[JsonPropertyName("details")]`
- Property with no override → default camelCase (`"lineIndex"`)
- Property with override when `EmitJsonPropertyNames = false` → still emit `[JsonPropertyName]` for that property (override is explicit)
- Mixed DTO: some properties with overrides, some without, in one emission → correct attributes on each
- `copySourceAttributes = true` with existing `[JsonPropertyName("original")]` in SourceAttributes AND `JsonNameOverride = "override"` → emit ONLY `[JsonPropertyName("override")]`, NOT both (skip the source attribute to avoid duplicate)
- `JsonNameOverride = ""` → treated as null, no override (default behavior)
- Negative assertion: `JsonNameOverride` does NOT change the C# property name — only the `[JsonPropertyName]` attribute value

**Step 2: Run tests to verify they fail**

**Step 3: Update DtoEmitter**

When emitting `[JsonPropertyName]`:
- If `prop.JsonNameOverride` is not null and not empty: use override directly. **Also suppress** any existing `[JsonPropertyName]` from `SourceAttributes` to avoid emitting duplicate attributes.
- Else if `naming.EmitJsonPropertyNames`: camelCase the CLR property name (check for existing source `[JsonPropertyName]` first — existing `HasJsonPropertyNameAttribute` guard)
- Else: no attribute

**Step 4: Run tests, commit**

```
feat: update DtoEmitter to use JsonNameOverride for per-property JSON names
```

---

### Task 8: Update NormalizerEmitter for root property

**Prerequisites:** Task 5 (IsRootType, NeedsList on TypeGraphNode), Task 6 (container has Result property)

**Files:**
- Modify: `src/DataNormalizer.Generators/Emitters/NormalizerEmitter.cs`
- Modify: `tests/DataNormalizer.Generators.Tests/Emitters/NormalizerEmitterTests.cs`

**Step 1: Write failing tests**

Tests:
- Generated normalizer sets `result.Result = __{rootCamel}Arr[0]` (always, after the existing for-loop)
- Root with `NeedsList = false` → normalizer does NOT set a list property for root on container (skip root inside the list-property-setting loop)
- Root with `NeedsList = true` → normalizer sets BOTH `result.Result` AND `result.{rootListProp}`
- All other types → list property set as before
- Circular self-referencing root with `NeedsList = true` → both Result and list set correctly

**Step 2: Run tests to verify they fail**

**Step 3: Update test helpers**

Update `CreateNode` calls in existing NormalizerEmitter tests to pass `isRootType` and `needsList` as needed. Enumerate which existing tests need updating — any test that currently asserts the root list property is set must be updated:
- Tests asserting `result.{rootPluralDtos} = __...Arr` must pass `needsList: true` on the root node and keep those assertions
- Tests where the root is not referenced by other types must pass `needsList: false` and update assertions to remove root list expectations

**Step 4: Update NormalizerEmitter**

In `EmitPublicNormalizeMethod`:
- **Root identification:** Compare `node.IsRootType` (not object equality) to identify the root node in the loop
- **Root array variable:** The dedup array `var __{rootCamel}Arr = __{rootCamel}Dict.Values.ToArray();` is ALWAYS created (it's needed for `result.Result`). This already happens in the existing code for all nodes. Do NOT skip creating this variable — only skip setting the list property on the container.
- **Inside the existing list-property loop:** When iterating `allNodes`, skip setting the list property for the root node if `rootNode.NeedsList == false`. The line `result.{rootListProp} = __{rootCamel}Arr;` is the ONLY thing that's conditionally skipped.
- **After the loop:** Always emit `result.Result = __{rootCamel}Arr[0];` where `rootCamel` is computed from the root node's type name using the same camelCase helper. This uses the same array variable that was already created above.
- If `rootNode.NeedsList`: the root's list property is ALSO set inside the loop as before (no skip)

**Step 5: Run tests, commit**

```
feat: update NormalizerEmitter to set Result property and conditional root list
```

---

### Task 9: Update DenormalizerEmitter for root property

**Prerequisites:** Task 5 (IsRootType, NeedsList), Task 6 (container has Result)

**Files:**
- Modify: `src/DataNormalizer.Generators/Emitters/DenormalizerEmitter.cs`
- Modify: `tests/DataNormalizer.Generators.Tests/Emitters/DenormalizerEmitterTests.cs`

**Step 1: Write failing tests**

Tests:
- Denormalizer reads root from `normalized.Result` (not from `list[0]`)
- Root with `NeedsList = false` → in `EmitGetCollections`, wrap Result in a single-element DTO array: `var {camel}Dtos = new[] { normalized.Result };`. Also assert NEGATIVE: generated code does NOT contain `normalized.{rootListProp}` (the list property isn't read)
- Root with `NeedsList = true` → in `EmitGetCollections`, read from `normalized.{rootListProp}` for reference resolution. Result is still the entry point for the final return.
- Denormalize returns the reconstructed root object
- Existing tests updated with `isRootType`/`needsList` on test nodes

**Step 2: Run tests to verify they fail**

**Step 3: Update test helpers**

Same as Task 8: update `CreateNode` calls with `isRootType`/`needsList`. List which existing tests break and need updating.

**Step 4: Update DenormalizerEmitter**

The change is localized to `EmitGetCollections` (which reads collections from the container):

- **Add `rootNode` parameter** to `EmitGetCollections` (or pass through existing parameters)
- **Root identification in loop:** Use `node.IsRootType` flag (not object equality or TypeFullName comparison) to identify the root node
- For the root node, emit:
  - If `!rootNode.NeedsList`: `var {camel}Dtos = new[] { normalized.Result };`
  - If `rootNode.NeedsList`: `var {camel}Dtos = normalized.{rootListProp};` (existing pattern)
- For non-root nodes: unchanged
- **Do NOT change `EmitPass1`, `EmitPass2`, or `EmitRootResolution`** — these work on the DTO arrays produced by `EmitGetCollections`, which are now correct for both NeedsList scenarios. The existing `return {plural}[0]` return pattern in root resolution remains correct (index 0 of the DTO array).

> **Key insight:** The type mismatch concern is unfounded because `EmitGetCollections` produces DTO arrays (not source object arrays). The `new[] { normalized.Result }` creates a `T[]` where `T` is the root DTO type, matching the expected array type for pass1/pass2 processing.

**Step 5: Run tests, commit**

```
feat: update DenormalizerEmitter to read from Result property
```

---

### Task 10: Verify end-to-end wiring through NormalizeGenerator

**Prerequisites:** Tasks 1, 5, 6, 7, 8, 9 (all model and emitter changes complete)

**Files:**
- Verify: `src/DataNormalizer.Generators/NormalizeGenerator.cs` (call sites already updated in Tasks 5 and 6)

**Step 1: Verify TypeGraphAnalyzer call site**

Task 5 already updated the `NormalizeGenerator` call to pass `model`. Verify:

```csharp
var typeGraph = TypeGraphAnalyzer.Analyze(rootTypeSymbol, model, ...);
```

**Step 2: Verify ContainerEmitter call site**

Task 6 already updated the call to the 4-parameter signature. Verify:

```csharp
ContainerEmitter.Emit(rootNode, allNodes, model.Naming, model.JsonContract)
```

**Step 3: Verify NormalizerEmitter and DenormalizerEmitter access**

These emitters receive `allNodes` which now includes `NeedsList` and `IsRootType` from Task 5. No signature changes needed — the data flows through `TypeGraphNode` properties. Confirm this by checking the existing call sites.

**Step 4: Run all generator tests**

> **Note:** Existing integration tests may still fail because they reference old container shapes — that's expected and fixed in Task 11.

**Step 5: Commit**

```
feat: verify end-to-end JsonContractModel wiring through NormalizeGenerator
```

---

### Task 11: Update all existing tests for root property change

**Prerequisites:** Task 10 (all wiring complete)

**Files (ALL need updating — verify this list with `grep -r "Dtos\[" tests/ samples/`):**
- `tests/DataNormalizer.Integration.Tests/SimpleNormalizationTests.cs`
- `tests/DataNormalizer.Integration.Tests/BasicRoundtripTests.cs`
- `tests/DataNormalizer.Integration.Tests/ConfigFeatureTests.cs`
- `tests/DataNormalizer.Integration.Tests/CircularReferenceTests.cs`
- `tests/DataNormalizer.Integration.Tests/DeepNestingTests.cs`
- `tests/DataNormalizer.Integration.Tests/PerformanceTests.cs`
- `tests/DataNormalizer.Integration.Tests/SmokeTests.cs`
- `tests/DataNormalizer.Integration.Tests/JsonNamingTests.cs` ← **also needs updating**
- `tests/DataNormalizer.Generators.Tests/GeneratorEndToEndTests.cs`
- `tests/DataNormalizer.Generators.Tests/Emitters/ContainerEmitterTests.cs`
- `tests/DataNormalizer.Generators.Tests/Emitters/NormalizerEmitterTests.cs`
- `tests/DataNormalizer.Generators.Tests/Emitters/DenormalizerEmitterTests.cs`
- `tests/DataNormalizer.Generators.Tests/Emitters/EmitterHelpersTests.cs` ← **also needs updating if it tests list property names**
- `samples/DataNormalizer.Samples/Program.cs`

**Step 0: Build NeedsList reference table**

Before making changes, determine which root type in each test scenario has `NeedsList = true` vs `false`:

| Test file | Root type | Referenced by others? | NeedsList |
|---|---|---|---|
| SimpleNormalizationTests | Person | Check if Address/etc refs Person | Determine |
| BasicRoundtripTests | (varies) | Check per test | Determine |
| CircularReferenceTests | (varies) | Yes (circular) | `true` |
| DeepNestingTests | (varies) | Check | Determine |
| ... | ... | ... | ... |

> **IMPORTANT:** Run the tests first to see which ones fail and how. Use the failure messages to guide changes rather than blind find-replace.

**Step 1: Integration tests — 5 access patterns to update**

Search for ALL of these patterns (not just `[0]`):

1. `container.{RootType}Dtos[0]` → `container.Result`
2. `container.{RootType}Dtos.Length` or `.Count` → remove or change assertion (no root list when `NeedsList = false`)
3. `Has.Length.EqualTo(...)` on root list → remove or adjust
4. `var roots = container.{RootType}Dtos` (variable assignment) → `var root = container.Result` (single object, not array)
5. Inline property access on root list → adjust for single Result object

For tests where root `NeedsList = true` (circular references, etc.), the root list still exists — keep list-based assertions but also add `Result` assertions.

**Step 2: JSON serialization assertions**

Any test that serializes a container to JSON and checks for `"{rootType}Dtos"` in the output must be updated to check for `"result"` instead.

**Step 3: E2E tests**

Update generated code assertions to expect `Result` property.

**Step 4: Samples**

Update `Program.cs` to use `container.Result` instead of `container.{RootType}Dtos[0]`. Note there are two configs: `SampleNormalization` (Order root) and `CorporateNormalization` (Corporation root). Both need updating.

**Step 5: Run all tests, commit**

```
test: update all tests and samples for root property container shape
```

---

### Task 12: Add integration tests for full search-response use case

**Prerequisites:** Task 11 (all existing tests passing)

**Files:**
- Create: `tests/DataNormalizer.Integration.Tests/TestTypes/Search/` (types mirroring search-response.json)
- Create: `tests/DataNormalizer.Integration.Tests/TestTypes/Transport/` (types for NeedsList=true scenario)
- Create: `tests/DataNormalizer.Integration.Tests/SearchNormalizationConfig.cs`
- Create: `tests/DataNormalizer.Integration.Tests/TransportNormalizationConfig.cs`
- Create: `tests/DataNormalizer.Integration.Tests/SearchContractTests.cs`

**Step 1: Create search test types**

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

**Step 2: Create transport test types for NeedsList=true scenario**

In `TestTypes/Transport/` namespace — a small graph where the root IS referenced by other types, with realistic unique data fields for meaningful comparison:

- `TransportNetwork` (root): `string NetworkName`, `string Region`, `List<TransportLine> Lines`, `List<TransportStation> Stations`
- `TransportLine`: `string LineName`, `string Color`, `string Mode`, `List<TransportStation> Stops`, `TransportNetwork Network` ← **back-reference to root!**
- `TransportStation`: `string StationName`, `double Latitude`, `double Longitude`, `string Zone`, `List<TransportLine> ServingLines`

> `TransportLine.Network` references the root type `TransportNetwork`, so `TransportNetwork.NeedsList = true`. The container should have both `Result` and a `TransportNetworkDtos` list.

**Step 3: Create SearchNormalizationConfig**

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

**Step 4: Create TransportNormalizationConfig**

```csharp
[NormalizeConfiguration]
public partial class TransportNormalizationConfig : NormalizationConfig
{
    protected override void Configure(NormalizeBuilder builder)
    {
        builder.NormalizeGraph<TransportNetwork>(graph =>
        {
            graph.UseJsonContract(c =>
            {
                c.Collection<TransportLine>("lines");
                c.Collection<TransportStation>("stations");
            });
        });
    }
}
```

**Step 5: Write search contract tests**

- Normalize a SearchResponse graph with routes, hops, shared places, shared carriers
- Serialize to JSON
- Verify JSON has `"result"` (not `"searchResponseDtos"`)
- Verify container type does NOT have a `SearchResponseDtos` property (reflection check: `typeof(SearchNormalizationContainer).GetProperty("SearchResponseDtos")` is null)
- Verify JSON has `"routes"`, `"lines"`, `"places"`, `"carriers"` (custom names)
- Verify JSON has `"hops"` with `"line"`, `"marketingCarrier"`, `"vehicle"` (per-property overrides)
- Verify `"transitImages"` on hops (collection reference override)
- Verify no `"searchResponseDtos"` array in JSON
- Verify shared `SearchPlace` instances have same index values in normalized output (dedup assertions)
- Verify nullable reference: `MarketingCarrier?` with JsonName override works when null and when populated
- **Full roundtrip:** Normalize → Serialize to JSON → Deserialize from JSON → Denormalize → verify data matches original
- Denormalize and verify roundtrip: same data back

**Step 6: Write test for [NormalizeJsonName] attribute**

Add `[NormalizeJsonName("origin")]` on `SearchResponse.OriginPlace`. Verify JSON shows `"origin"` not `"originPlaceIndex"`. Also verify the default `"originPlaceIndex"` is absent.

**Step 7: Write test for attribute + config priority**

Add both `[NormalizeJsonName("orig")]` on property AND `Reference().JsonName("origin")` in config. Verify config wins → `"origin"`. Verify `"orig"` is absent.

**Step 8: Write test for default Result name**

Create a config without `RootPropertyName` set. Verify JSON has `"result"` as the default name.

**Step 9: Write transport network tests (NeedsList=true)**

- Normalize a TransportNetwork with Lines and Stations where Lines reference the root
- Verify container has BOTH `Result` property AND `TransportNetworkDtos` list
- Verify `Result` is the same object as `TransportNetworkDtos[0]`
- Verify roundtrip works with the back-reference

**Step 10: Run tests, commit**

```
test: add full search-response and transport-network integration tests for JSON contract customization
```

---

### Task 13: Add compilation checks to emitter unit tests

The emitter unit tests use string matching (`Does.Contain`) to verify generated code. This catches content but not validity -- a test can pass while the generated code has syntax errors, missing usings, or broken type references. Fix this by compiling the emitter output in each unit test using Roslyn.

**Files:**
- Modify: `tests/DataNormalizer.Generators.Tests/DataNormalizer.Generators.Tests.csproj` (add package reference)
- Create: `tests/DataNormalizer.Generators.Tests/Emitters/EmitterCompilationHelper.cs`
- Modify: `tests/DataNormalizer.Generators.Tests/Emitters/DtoEmitterTests.cs`
- Modify: `tests/DataNormalizer.Generators.Tests/Emitters/ContainerEmitterTests.cs`
- Modify: `tests/DataNormalizer.Generators.Tests/Emitters/NormalizerEmitterTests.cs`
- Modify: `tests/DataNormalizer.Generators.Tests/Emitters/DenormalizerEmitterTests.cs`
- **NOT** `EmitterHelpersTests.cs` (these test helper logic, not generated code)

**Step 1: Add Basic.Reference.Assemblies NuGet package**

Add `Basic.Reference.Assemblies.Net90` (or appropriate version matching the test TFM) to the test project. This provides complete, reliable BCL metadata references without fragile `Assembly.Location` paths.

**Step 2: Create EmitterCompilationHelper**

```csharp
internal static class EmitterCompilationHelper
{
    private static readonly MetadataReference[] BaseReferences;

    static EmitterCompilationHelper()
    {
        var refs = new List<MetadataReference>();
        // Use Basic.Reference.Assemblies for complete BCL coverage
        refs.AddRange(Basic.ReferenceAssemblies.Net90.References.All);
        // Add DataNormalizer runtime library (for generated code that references it)
        refs.Add(MetadataReference.CreateFromFile(typeof(DataNormalizer.Runtime.SomeType).Assembly.Location));
        BaseReferences = refs.ToArray();
    }

    public static void AssertCompiles(params string[] sources)
    {
        var trees = sources.Select(s => CSharpSyntaxTree.ParseText(s)).ToArray();
        var compilation = CSharpCompilation.Create(
            "EmitterTest",
            trees,
            BaseReferences,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithNullableContextOptions(NullableContextOptions.Enable));
        var diagnostics = compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToArray();
        Assert.That(diagnostics, Is.Empty,
            $"Generated code has compilation errors:\n{string.Join("\n", diagnostics.Select(d => d.ToString()))}");
    }

    /// <summary>
    /// Compile with additional stub source code for user-defined types
    /// referenced by generated code.
    /// </summary>
    public static void AssertCompilesWithStubs(string[] generated, string[] stubs)
    {
        AssertCompiles(generated.Concat(stubs).ToArray());
    }
}
```

> **User-defined types:** Generated code references types like `TestApp.Person`, `TestApp.Address` that don't exist in the compilation. For tests that compile generated normalizer/denormalizer code, provide stub type definitions as additional source strings.

**Step 3: Add compilation assertions to key tests**

For each emitter test file, add `EmitterCompilationHelper.AssertCompiles(result)` to the primary test cases. Specifically:
- **DtoEmitterTests:** Main DTO emission test, DTO with JsonNameOverride
- **ContainerEmitterTests:** Default container, container with Result + list, container with collection overrides
- **NormalizerEmitterTests:** Full normalizer output (requires stub types)
- **DenormalizerEmitterTests:** Full denormalizer output (requires stub types)

For tests that emit multiple related files (container + DTOs + normalizer + denormalizer), pass all sources together so cross-file references resolve:

```csharp
var stubs = new[] { "namespace TestApp { public class Person { public string Name { get; set; } ... } }" };
var dtoResult = DtoEmitter.Emit(personNode, false, naming);
var containerResult = ContainerEmitter.Emit(personNode, allNodes, naming, jsonContract);
EmitterCompilationHelper.AssertCompilesWithStubs(
    new[] { dtoResult, containerResult }, stubs);
```

**Step 4: Add negative test**

Write one test with deliberately malformed output (e.g., append `}}}}` to a generated string) and verify `AssertCompiles` throws with a meaningful error. This validates the helper actually catches compilation failures.

**Step 5: Write compilation tests for new features**

- Emit a DTO with `JsonNameOverride` properties → compiles
- Emit a container with `RootPropertyName` and collection overrides → compiles
- Emit normalizer + denormalizer + container + DTOs with root property → compiles together (with stubs)

**Step 6: Run tests, commit**

```
feat: add compilation checks to emitter unit tests
```

---

### Task 14: Add unparsed config statement diagnostics

When the parser encounters a statement inside a known builder lambda that it cannot parse, it should emit a compiler error. This makes the parser's limitations visible at build time rather than silently producing wrong defaults.

**Prerequisites:** Task 3 (ParseContext structure, ReceiverKinds)

**Files:**
- Modify: `src/DataNormalizer.Generators/Diagnostics/DiagnosticDescriptors.cs` (file ALREADY EXISTS at this path)
- Modify: `src/DataNormalizer.Generators/Analysis/ConfigurationParser.cs`
- Modify: `src/DataNormalizer.Generators/Models/NormalizationModel.cs`
- Modify: `src/DataNormalizer.Generators/NormalizeGenerator.cs`
- Test: `tests/DataNormalizer.Generators.Tests/Analysis/ConfigurationParserTests.cs`

**Step 1: Define DN1001 diagnostic descriptor**

Add to the EXISTING `Diagnostics/DiagnosticDescriptors.cs` (DN1002 was already added in Task 3):

```csharp
public static readonly DiagnosticDescriptor UnparsedConfigStatement = new(
    id: "DN1001",
    title: "Unparsed configuration statement",
    messageFormat: "Configuration statement could not be parsed and will be ignored: '{0}'",
    category: "DataNormalizer",
    defaultSeverity: DiagnosticSeverity.Error,
    isEnabledByDefault: true);
```

**Step 2: Verify ParseContext diagnostics use GeneratorDiagnosticInfo**

Task 3 already added `List<GeneratorDiagnosticInfo> Diagnostics` to ParseContext. Verify this is using the existing `GeneratorDiagnosticInfo` type from the codebase (check its constructor signature — likely `(string Id, string TypeName)` or similar two-field constructor).

> **CRITICAL:** Do NOT store `Diagnostic` objects (which contain `SyntaxTree` references). The `GeneratorDiagnosticInfo` type is already serializable. For DN1001, store:
> ```csharp
> context.Diagnostics.Add(new GeneratorDiagnosticInfo("DN1001", truncatedText));
> ```

**Step 3: Track builder lambda depth with try/finally**

Add `public int BuilderLambdaDepth { get; set; }` to ParseContext. Wrap ALL increment sites with try/finally to prevent mismatched depth on exceptions:

```csharp
context.BuilderLambdaDepth++;
try
{
    ProcessStatements(lambdaBody, context);
}
finally
{
    context.BuilderLambdaDepth--;
}
```

Apply this pattern at every site that enters a builder lambda: `ProcessNamingLambda`, `ProcessJsonContractLambda`, `ProcessGraphLambda`, `ProcessForTypeLambda`.

**Step 4: Emit diagnostic for unrecognized statements**

In `ProcessStatements`, after all case branches:

```csharp
default:
    if (context.BuilderLambdaDepth > 0)
    {
        var text = statement.ToString();
        if (text.Length > 100) text = text[..100] + "...";
        // Use the same GeneratorDiagnosticInfo type used by DN1002 in Task 3
        context.Diagnostics.Add(new GeneratorDiagnosticInfo("DN1001", text));
    }
    break;
```

> **Note:** `statement.ToString()` is more concise than `ToFullString().Trim()`. Truncate to 100 chars for readability. Do NOT use `statement.GetLocation()` — Location contains SyntaxTree references that break incrementality.

**Step 5: Surface diagnostics through the pipeline**

Add diagnostics to `NormalizationModel` using `GeneratorDiagnosticInfo` (the same type already used in ParseContext):

```csharp
public ImmutableArray<GeneratorDiagnosticInfo> ParserDiagnostics { get; init; } =
    ImmutableArray<GeneratorDiagnosticInfo>.Empty;
```

In `NormalizeGenerator`'s `RegisterSourceOutput` callback (NOT a non-existent `Execute` method — this is an `IIncrementalGenerator`), map each `GeneratorDiagnosticInfo` to the correct `DiagnosticDescriptor` and report:

```csharp
foreach (var diag in model.ParserDiagnostics)
{
    // Map diagnostic ID to the correct descriptor
    var descriptor = diag.Id switch
    {
        "DN1001" => DiagnosticDescriptors.UnparsedConfigStatement,
        "DN1002" => DiagnosticDescriptors.DuplicateCollectionType,
        _ => DiagnosticDescriptors.UnparsedConfigStatement, // fallback
    };
    spc.ReportDiagnostic(Diagnostic.Create(
        descriptor,
        Location.None,
        diag.TypeName)); // TypeName field carries the message argument
}
```

> **Note:** Check the existing diagnostic reporting pattern in `NormalizeGenerator` — there may already be a loop that processes `GeneratorDiagnosticInfo` entries. If so, add the new IDs to that existing loop rather than creating a parallel one. The existing pattern likely uses a `TransformConfig` pipeline step to convert parser output into `GeneratorOutput` — extend that conversion to include parser diagnostics.

**Step 6: Write failing tests**

Tests:
- `if (true) { n.DtoSuffix = "Dto"; }` (conditional statement) → error DN1001 on the `if` statement
- `Console.WriteLine("debug")` inside builder lambda → error DN1001
- `foreach (var x in items) { }` inside builder lambda → error DN1001
- Valid statements (`n.DtoSuffix = "Dto"`, `graph.Inline<T>()`, `x.Reference(p => p.Line).JsonName("line")`) → no diagnostic
- Statements outside builder lambdas (in Configure method body, not inside any lambda) → no diagnostic
- Deeply nested lambda (ForType inside NormalizeGraph) → DN1001 still fires for unrecognized statements
- Multiple unparsed statements → multiple DN1001 diagnostics (verify count)
- Duplicate `Collection<T>()` for same T → error DN1002

> **Note:** `n.DtoSuffix = GetSuffix()` (non-literal RHS) does NOT produce DN1001 — it matches the existing `AssignmentExpressionSyntax` branch and silently returns when RHS isn't a literal. This is correct behavior — the assignment is "recognized" even though the value can't be extracted. Do NOT use this as a test case for DN1001.

**Step 7: Run tests, commit**

```
feat: emit DN1001 error for unparsed configuration statements and DN1002 for duplicate Collection<T>
```

---

### Task 15: Final build, format, and verification check

**Step 1:** `dotnet restore`
**Step 2:** `dotnet tool restore`
**Step 3:** `dotnet build` → 0 errors, 0 warnings (TreatWarningsAsErrors is on)
**Step 4:** `dotnet csharpier .` (format all files)
**Step 5:** `dotnet csharpier check .` → all formatted (verify no diff)
**Step 6:** `dotnet test --no-build` → all pass (tests the same build artifacts)
**Step 7:** `dotnet pack --no-build` → NuGet package builds correctly
**Step 8:** `dotnet test --no-build --collect:"XPlat Code Coverage"` → collect coverage, review for new code coverage gaps
**Step 9:** Commit any formatting fixes

```
style: format code with CSharpier
```
