# Re-Audit: Task 5 v2 -- TypeGraphAnalyzer [NormalizeJsonName] and rootTypeNeedsList

**Summary:** The v1 critical findings (CollectionElementTypeFullName, init-only mutation, NormalizeFqn extraction, self-referencing root) are all addressed in the amended plan. However, three new issues emerged: (1) the `with` expression on `TypeGraphNode` won't compile because it's a `sealed class`, not a `record`; (2) the plan changes `Analyze`'s return type to the non-existent `TypeGraph` type; (3) the signature change breaks all ~20 existing `TypeGraphAnalyzerTests` with no migration step specified.

## Verification of v1 Finding Amendments

### v1 Finding 1: `CollectionElementTypeFullName` usage -- VERIFIED FIXED
The amended code (Step 7, line 575) correctly uses `p.CollectionElementTypeFullName == rootFqn` for `PropertyKind.Collection`. Confirmed `CollectionElementTypeFullName` exists on `AnalyzedProperty` at line 12 of `AnalyzedProperty.cs`. The property is nullable (`string?`), which is fine since the comparison `null == rootFqn` correctly yields `false` for Simple properties that don't set it.

### v1 Finding 2: init-only mutation -- VERIFIED FIXED
The plan (Step 7) now says "Construct final nodes with computed values (don't mutate init-only properties)" with a `with` expression or new instance. However, this introduces a NEW issue (see Finding 1 below).

### v1 Finding 3: NormalizationModel parameter -- VERIFIED FIXED
Step 5 adds `NormalizationModel model` parameter. Step 6 uses `model.PropertyJsonNameOverrides`. However, there's a return type issue (see Finding 2 below).

### v1 Finding 4: NormalizeFqn extraction -- VERIFIED FIXED
Step 1 extracts `FqnHelper` with `NormalizeFqn` and `BuildPropertyKey`. Step 6 uses `FqnHelper.BuildPropertyKey`. ConfigurationParser update specified.

### v1 Finding 5: Self-referencing root -- VERIFIED FIXED (with caveat)
The `n != rootNode` filter is removed. The amended code includes self-references. However, the `&& (n != rootNode || ...)` second clause is **redundant** (see Finding 4 below).

### v1 Finding 6: Variable name `typeSymbol` -- VERIFIED FIXED
Step 6 now includes the note "Use the correct parameter name (check existing code -- may be `type` not `typeSymbol`)". The code uses `type.ToDisplayString(...)` which is correct (`type` is the parameter name at `TypeGraphAnalyzer.cs:48`).

## NeedsList Logic Walkthrough

The amended code:

```csharp
var rootNeedsList = allNodes.Any(n =>
    n.Properties.Any(p =>
    {
        if (p.Kind == PropertyKind.Normalized)
            return p.TypeFullName == rootFqn;
        if (p.Kind == PropertyKind.Collection)
            return p.CollectionElementTypeFullName == rootFqn;
        return false;
    })
    && (n != rootNode || n.Properties.Any(p =>
        (p.Kind == PropertyKind.Normalized && p.TypeFullName == rootFqn) ||
        (p.Kind == PropertyKind.Collection && p.CollectionElementTypeFullName == rootFqn)))
);
```

### Scenario 1: Non-root node A has Normalized property referencing root
- Condition A (`n.Properties.Any(...)`) evaluates: `p.Kind == Normalized && p.TypeFullName == rootFqn` -> `true`
- Condition B (`n != rootNode || ...`): `n != rootNode` -> `true` (short-circuits)
- **Result: `true`** -- CORRECT

### Scenario 2: Non-root node A has Collection property with element type = root
- Condition A: `p.Kind == Collection && p.CollectionElementTypeFullName == rootFqn` -> `true`
- Condition B: `n != rootNode` -> `true` (short-circuits)
- **Result: `true`** -- CORRECT

### Scenario 3: Root node has self-reference (Normalized)
- Condition A: `p.Kind == Normalized && p.TypeFullName == rootFqn` -> `true`
- Condition B: `n != rootNode` is `false`, falls through to `n.Properties.Any(...)` which is the same check -> `true`
- **Result: `true`** -- CORRECT

### Scenario 4: Root node has self-reference (Collection of self)
- Condition A: `p.Kind == Collection && p.CollectionElementTypeFullName == rootFqn` -> `true`
- Condition B: `n != rootNode` is `false`, falls to `n.Properties.Any(...)` -> checks Collection path -> `true`
- **Result: `true`** -- CORRECT

### Scenario 5: No references to root from anywhere
- Condition A: `false` for all nodes (no property references root)
- Short-circuits, all `n` evaluate to `false`
- **Result: `false`** -- CORRECT

### Scenario 6: Root has property referencing non-root
- Condition A: root's properties reference non-root type, not root itself -> `false`
- **Result: `false`** -- CORRECT

**All scenarios pass. The logic is correct.**

## New Findings

### Finding 1: `with` expression won't compile on `TypeGraphNode` (sealed class, not record)

**Category:** Incorrect Code
**Action:** Amend Plan
**Detail:** Step 7 says:
```csharp
var finalRootNode = rootNode with { IsRootType = true, NeedsList = rootNeedsList };
// OR if TypeGraphNode is a class: create a new instance copying all properties
```

`TypeGraphNode` (`TypeGraphNode.cs:5`) is `internal sealed class`, not a `record`. The `with` expression is only available on `record` types (and anonymous types) in C#. Using `with` on a plain `class` produces compile error `CS8858: The receiver type 'TypeGraphNode' is not a valid record type and is not a valid struct type.`

The plan includes the fallback comment "OR if TypeGraphNode is a class: create a new instance" but doesn't show the actual code. Since `TypeGraphNode` uses `required` properties (`TypeFullName`, `TypeName`, `Properties`), the implementer must copy ALL required fields. The plan should provide the concrete code:

```csharp
var finalRootNode = new TypeGraphNode
{
    TypeFullName = rootNode.TypeFullName,
    TypeName = rootNode.TypeName,
    Properties = rootNode.Properties,
    HasCircularReference = rootNode.HasCircularReference,
    IsRootType = true,
    NeedsList = rootNeedsList,
};
```

Alternatively, the plan could convert `TypeGraphNode` to a `record` first, but this may have broader implications for the codebase. Recommend providing the explicit `new TypeGraphNode { ... }` code.

---

### Finding 2: `Analyze` return type `TypeGraph` does not exist

**Category:** Incorrect Code
**Action:** Amend Plan
**Detail:** Step 5 specifies:
```csharp
public static TypeGraph Analyze(
    INamedTypeSymbol rootType,
    NormalizationModel model,
    ...)
```

There is no `TypeGraph` type anywhere in the codebase. The current return type is `IReadOnlyList<TypeGraphNode>` (`TypeGraphAnalyzer.cs:10`). Using `TypeGraph` would be an undefined type error.

**Recommendation:** Change the signature in the plan to:
```csharp
public static IReadOnlyList<TypeGraphNode> Analyze(
    INamedTypeSymbol rootType,
    NormalizationModel model,
    ...)
```

Or if the plan intends to introduce a new `TypeGraph` wrapper type (to hold both the node list and `rootNeedsList`), it must explicitly define that type and add it to the Files list. But this seems unnecessary since `NeedsList` is being added directly to `TypeGraphNode`.

---

### Finding 3: Signature change breaks ~20 existing TypeGraphAnalyzerTests

**Category:** Missing Wiring
**Action:** Amend Plan
**Detail:** The plan adds `NormalizationModel model` as the second parameter to `TypeGraphAnalyzer.Analyze()`. There are 20 existing test methods in `TypeGraphAnalyzerTests.cs` (lines 13-817) that all call:
```csharp
TypeGraphAnalyzer.Analyze(
    rootType,
    ImmutableHashSet<string>.Empty,
    ImmutableHashSet<string>.Empty,
    ImmutableDictionary<string, TypeConfiguration>.Empty,
    autoDiscover: true
);
```

After the signature change, these will all fail with a compile error because the second argument (`ImmutableHashSet<string>`) doesn't match `NormalizationModel`.

The plan does not mention updating these 20 existing tests or adding a backward-compatible overload. Compare to Task 6 which explicitly adds a backward-compatible `ContainerEmitter.Emit` overload for the same reason.

**Recommendation:** Either:
1. Add a backward-compatible overload in `TypeGraphAnalyzer` that constructs a default `NormalizationModel` from the individual parameters (similar to Task 6's approach). This preserves all existing tests. The overload delegates to the new signature by constructing a `NormalizationModel` with the passed-in values.
2. Or explicitly add a step to update all 20 existing test methods to pass a `NormalizationModel`. The model would need to be constructed with the individual fields that the tests currently pass directly (InlinedTypes, ExplicitTypes, TypeConfigurations, AutoDiscover, CopySourceAttributes).

Option 1 is cleaner and less disruptive:
```csharp
// Backward-compatible overload (existing tests use this)
public static IReadOnlyList<TypeGraphNode> Analyze(
    INamedTypeSymbol rootType,
    ImmutableHashSet<string> inlinedTypes,
    ImmutableHashSet<string> explicitTypes,
    ImmutableDictionary<string, TypeConfiguration> typeConfigurations,
    bool autoDiscover,
    bool copySourceAttributes = false)
    => Analyze(rootType, new NormalizationModel
    {
        InlinedTypes = inlinedTypes,
        ExplicitTypes = explicitTypes,
        TypeConfigurations = typeConfigurations,
        AutoDiscover = autoDiscover,
        CopySourceAttributes = copySourceAttributes,
    });
```

---

### Finding 4: NeedsList `&&` second clause is redundant (harmless)

**Category:** Fragile Code
**Action:** Accept
**Detail:** The `&& (n != rootNode || n.Properties.Any(...))` second clause is logically redundant. When Condition A (`n.Properties.Any(...)`) is true:
- If `n != rootNode`: Condition B is `true` trivially (first operand of `||` is true)
- If `n == rootNode`: Condition B's fallback `n.Properties.Any(...)` is checking the exact same thing as Condition A, which we already know is true

So Condition B is **always true** when Condition A is true. The entire expression simplifies to just Condition A. The extra clause adds complexity without changing behavior. This is not a bug, just unnecessary code.

**Recommendation:** The implementer could simplify to:
```csharp
var rootNeedsList = allNodes.Any(n =>
    n.Properties.Any(p =>
    {
        if (p.Kind == PropertyKind.Normalized)
            return p.TypeFullName == rootFqn;
        if (p.Kind == PropertyKind.Collection)
            return p.CollectionElementTypeFullName == rootFqn;
        return false;
    }));
```
This is informational only -- the complex version produces identical results.

---

### Finding 5: `FqnHelper.BuildPropertyKey` does double normalization (harmless)

**Category:** Fragile Code
**Action:** Accept
**Detail:** Step 6 code:
```csharp
var propKey = FqnHelper.BuildPropertyKey(
    type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), prop.Name);
```

`BuildPropertyKey` is defined as:
```csharp
public static string BuildPropertyKey(string typeFqn, string propertyName)
    => NormalizeFqn($"{typeFqn}.{propertyName}");
```

`type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)` returns `"global::TestApp.Person"`. Then `NormalizeFqn("global::TestApp.Person.Name")` strips `global::` to produce `"TestApp.Person.Name"`.

Meanwhile, `TypeGraphAnalyzer.GetFullyQualifiedName` (line 630-639) already strips `global::` and caches the result. The existing code at line 64 computes `typeFullName = GetFullyQualifiedName(type, fqnCache)` = `"TestApp.Person"`. Using `$"{typeFullName}.{prop.Name}"` directly would skip the redundant normalization and cache miss.

This is functionally correct but slightly inefficient. The implementer could optimize by using `typeFullName` (already computed at line 64) instead of calling `type.ToDisplayString()` again:
```csharp
var propKey = $"{typeFullName}.{prop.Name}";
```
This produces the same key and avoids redundant work. But using `FqnHelper.BuildPropertyKey` is also fine -- it's just unnecessary overhead.

---

### Finding 6: `NormalizationModel` constructor requires matching ConfigurationParser fields

**Category:** Implicit Assumptions
**Action:** Accept
**Detail:** The backward-compatible overload (if adopted per Finding 3) constructs a `NormalizationModel` from individual parameters. The `NormalizationModel` has additional fields beyond what the old `Analyze` signature exposed:
- `ConfigClassName` (default `""`)
- `ConfigNamespace` (default `""`)
- `RootTypes` (default empty)
- `PropertyJsonNameOverrides` (default empty)
- `JsonContract` (default `JsonContractModel.Default`)
- `Naming` (default `NamingModel.Default`)
- `UseReferenceTrackingForCycles` (default `false`)

All of these have sensible defaults in `NormalizationModel`. The backward-compatible overload only needs to set the 5 fields it receives. The `PropertyJsonNameOverrides` default (empty dict) means no JSON name overrides, which matches the old behavior. No issue here.

---

### Finding 7: Attribute matching correctly handles only `"NormalizeJsonNameAttribute"`

**Category:** Implicit Assumptions
**Action:** Accept
**Detail:** The amended Step 6 correctly specifies:
```csharp
var jsonNameAttr = prop.GetAttributes().FirstOrDefault(a =>
    a.AttributeClass?.Name == "NormalizeJsonNameAttribute");
```
This matches only the full suffix form. Confirmed: Roslyn's `INamedTypeSymbol.Name` always returns the full class name `"NormalizeJsonNameAttribute"`, never the shortened `"NormalizeJsonName"` form (which is a C# syntactic sugar, not a type name). Correct.

---

### Finding 8: Empty string `[NormalizeJsonName("")]` handling is correct

**Category:** Implicit Assumptions
**Action:** Accept
**Detail:** The amended Step 6 code:
```csharp
var attrValue = jsonNameAttr.ConstructorArguments[0].Value as string;
jsonNameOverride = string.IsNullOrEmpty(attrValue) ? null : attrValue;
```
This correctly treats empty string as null (per the design decision "empty `[NormalizeJsonName("")]` -> treated as null"). `string.IsNullOrEmpty("")` is `true`, so the result is `null`. Correct.

---

### Finding 9: Plan mentions all ~9 `new AnalyzedProperty` sites

**Category:** Missing Wiring
**Action:** Accept
**Detail:** The amended plan Step 6 says: "Add `JsonNameOverride = jsonNameOverride` to ALL `new AnalyzedProperty(...)` construction sites in the analyzer (there are ~9 of them). Search for `new AnalyzedProperty` and ensure every one includes the new property."

Actual count in `TypeGraphAnalyzer.cs`: lines 106, 131, 175, 197, 221, 244, 280, 298, 317 -- exactly 9 sites. The guidance is correct.

---

## Summary Table

| # | Category | Action | Issue | File:Line | Amendment / Question |
|---|----------|--------|-------|-----------|----------------------|
| 1 | Incorrect Code | **Amend Plan** | `with` expression won't compile on `TypeGraphNode` (sealed class, not record) | TypeGraphNode.cs:5 | Provide explicit `new TypeGraphNode { ... }` construction code copying all fields; or convert to record |
| 2 | Incorrect Code | **Amend Plan** | `Analyze` return type `TypeGraph` does not exist | TypeGraphAnalyzer.cs:10, Plan Step 5 | Change to `IReadOnlyList<TypeGraphNode>` (current return type) |
| 3 | Missing Wiring | **Amend Plan** | Signature change breaks ~20 existing TypeGraphAnalyzerTests | TypeGraphAnalyzerTests.cs:27,54,79,... | Add backward-compatible overload or specify updating all 20 existing tests |
| 4 | Fragile Code | Accept | NeedsList `&&` second clause is logically redundant | Plan Step 7 | Can simplify but not required -- produces correct results |
| 5 | Fragile Code | Accept | `FqnHelper.BuildPropertyKey` bypasses existing FQN cache | Plan Step 6 | Could use pre-computed `typeFullName` instead; functionally correct as-is |
| 6 | Implicit Assumptions | Accept | NormalizationModel has additional fields beyond old Analyze params | NormalizationModel.cs:6-21 | All have sensible defaults; no issue |
| 7 | Implicit Assumptions | Accept | Attribute matching uses full suffix only | Plan Step 6 | Correct -- Roslyn always returns full class name |
| 8 | Implicit Assumptions | Accept | Empty string `[NormalizeJsonName("")]` -> null | Plan Step 6 | Correct per design decision |
| 9 | Missing Wiring | Accept | All 9 AnalyzedProperty sites mentioned | TypeGraphAnalyzer.cs (9 sites) | Count is accurate |

## v1 Finding Resolution Status

| v1 # | Issue | Status | Notes |
|-------|-------|--------|-------|
| 1 | `TypeFullName` wrong for Collection | **FIXED** | Uses `CollectionElementTypeFullName` correctly |
| 2 | init-only mutation | **PARTIALLY FIXED** | Addresses mutation but `with` won't compile (Finding 1 above) |
| 3 | Missing NormalizationModel param | **FIXED** | Added to signature (but return type wrong -- Finding 2 above) |
| 4 | NormalizeFqn inaccessible | **FIXED** | Extracted to FqnHelper |
| 5 | Self-referencing root excluded | **FIXED** | Filter removed |
| 6 | Wrong variable name | **FIXED** | Note added to check parameter name |

**No issues found in:** Non-Strict Typing, State Issues

## Critical Path

1. **Finding 1 (`with` expression)** -- The plan's primary mechanism for constructing the final root node won't compile. Must provide alternative construction code.
2. **Finding 2 (return type)** -- `TypeGraph` doesn't exist. Will cause compile error immediately.
3. **Finding 3 (existing tests)** -- 20 tests break silently on signature change. Must specify migration strategy.
