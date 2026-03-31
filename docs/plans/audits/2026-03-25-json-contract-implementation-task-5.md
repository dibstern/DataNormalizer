# Audit: Task 5 — TypeGraphAnalyzer [NormalizeJsonName] and rootTypeNeedsList

**Summary:** Task 5 has several critical bugs in the plan's code snippets and significant test coverage gaps. The most serious issues are: (1) `rootTypeNeedsList` uses `p.TypeFullName == rootFqn` for Collection properties, but `p.TypeFullName` is the full collection type (e.g., `"System.Collections.Generic.List<TestApp.Person>"`), not the element type — the check will always fail for collection references; (2) the plan says "Set `rootNode.NeedsList = rootNeedsList`" after graph construction, but `TypeGraphNode` uses `init` setters which cannot be assigned post-construction; (3) the plan references `model.PropertyJsonNameOverrides` and `NormalizeFqn` but `TypeGraphAnalyzer.Analyze()` doesn't accept a `NormalizationModel` and `NormalizeFqn` is a private method in `ConfigurationParser`; (4) the `n != rootNode` check in rootTypeNeedsList excludes self-referencing types from the check, silently returning false for types like `TreeNode` that reference themselves.

## Findings

### Finding 1: `p.TypeFullName == rootFqn` is WRONG for Collection properties
**Category:** Incorrect Code
**Action:** Amend Plan
**Detail:** The `rootTypeNeedsList` computation in Step 4 checks `p.TypeFullName == rootFqn`. For a `PropertyKind.Collection` property like `List<Person>`, `p.TypeFullName` is set to `propTypeFullName` at `TypeGraphAnalyzer.cs:179` which is `GetFullyQualifiedName(propType, fqnCache)` at line 89 — this returns the full collection type, e.g., `"System.Collections.Generic.List<TestApp.Person>"`. The root type's `TypeFullName` is `"TestApp.Person"`. These will never match.

For `PropertyKind.Collection`, the correct field to compare is `p.CollectionElementTypeFullName`, which stores the element type's FQN (set at `TypeGraphAnalyzer.cs:183`).

For `PropertyKind.Normalized`, `p.TypeFullName` is `propTypeFullName` (line 284), which IS the full property type — but for Normalized properties this is the complex type itself (not a wrapper), so the comparison is correct.

**Recommendation:** Amend the `rootTypeNeedsList` computation to:
```csharp
var rootNeedsList = allNodes.Any(n => n != rootNode &&
    n.Properties.Any(p =>
        (p.Kind == PropertyKind.Normalized && p.TypeFullName == rootFqn) ||
        (p.Kind == PropertyKind.Collection && p.CollectionElementTypeFullName == rootFqn)));
```

---

### Finding 2: `TypeGraphNode` uses `init` setters — cannot assign `NeedsList` post-construction
**Category:** Incorrect Code
**Action:** Amend Plan
**Detail:** The plan's Step 4 says "Set `rootNode.NeedsList = rootNeedsList` and `rootNode.IsRootType = true`." But `TypeGraphNode` (`TypeGraphNode.cs:5-11`) is a `sealed class` with `init` setters on all properties. After object initialization, `init` properties cannot be reassigned — this would produce a compile error: `CS8852: Init-only property 'TypeGraphNode.NeedsList' can only be assigned in an object initializer`.

The `rootTypeNeedsList` computation (Step 4) requires iterating `allNodes` — which means all nodes must already be created. But you can't set `NeedsList` on an already-created node.

**Recommendation:** The plan must restructure the approach. Two options:
1. **Two-pass approach:** First pass builds all nodes (as today), then compute `NeedsList` for each node, then create NEW `TypeGraphNode` instances with the computed values (wasteful but simple).
2. **Compute before node creation:** Compute `NeedsList` during the DFS traversal. For non-root nodes, it's always `true`. For the root node, track whether any other node has already been found to reference it. Since the root is added last (DFS post-order), all child nodes are already in `results` — you can check them before creating the root node.

Option 2 is more elegant and avoids recreating nodes. The root node is always the LAST entry in `results` (added at line 340-348), so you can check `results` for back-references before building the root node. Amend Step 4 to compute `NeedsList` inside `AnalyzeType` before the `results.Add` call on line 340.

---

### Finding 3: `TypeGraphAnalyzer.Analyze()` does not accept `NormalizationModel` or `PropertyJsonNameOverrides`
**Category:** Implicit Assumptions
**Action:** Amend Plan
**Detail:** The plan's Step 3 code snippet references `model.PropertyJsonNameOverrides.TryGetValue(propKey, out var configOverride)`, but `TypeGraphAnalyzer.Analyze()` (`TypeGraphAnalyzer.cs:10-17`) has this signature:
```csharp
public static IReadOnlyList<TypeGraphNode> Analyze(
    INamedTypeSymbol rootType,
    ImmutableHashSet<string> inlinedTypes,
    ImmutableHashSet<string> explicitTypes,
    ImmutableDictionary<string, TypeConfiguration> typeConfigurations,
    bool autoDiscover,
    bool copySourceAttributes = false)
```
There is no `model` parameter and no `PropertyJsonNameOverrides` parameter. The plan doesn't specify adding a parameter.

Additionally, the call site at `NormalizeGenerator.cs:78-85` passes individual fields from `NormalizationModel`, not the whole model. This call site would also need updating.

**Recommendation:** Amend the plan to explicitly:
1. Add `ImmutableDictionary<string, string> propertyJsonNameOverrides` as a new parameter to `TypeGraphAnalyzer.Analyze()` (and propagate to `AnalyzeType`), OR refactor to accept the full `NormalizationModel`.
2. Update the call site in `NormalizeGenerator.cs:78-85` to pass the new parameter.
3. Show the updated signatures in the plan.

---

### Finding 4: `NormalizeFqn` is private to `ConfigurationParser` — not accessible from `TypeGraphAnalyzer`
**Category:** Implicit Assumptions
**Action:** Amend Plan
**Detail:** The plan's Step 3 code uses `propKey = NormalizeFqn(propKey);` but `NormalizeFqn` is a `private static` method in `ConfigurationParser` (`ConfigurationParser.cs:576-579`). It is not accessible from `TypeGraphAnalyzer`.

However, `TypeGraphAnalyzer` already has its own equivalent: `GetFullyQualifiedName` at line 630-639, which does exactly the same thing — calls `ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)` and strips the `"global::"` prefix. So the FQN format IS consistent between the two classes, but the plan references the wrong function name.

**Recommendation:** Replace `NormalizeFqn(propKey)` in the plan's Step 3 code with inline logic that matches `GetFullyQualifiedName`'s stripping behavior:
```csharp
var typeFqn = GetFullyQualifiedName(typeSymbol, fqnCache);
var propKey = $"{typeFqn}.{prop.Name}";
```
Since `GetFullyQualifiedName` already strips `"global::"`, using its output for the type FQN portion of the key will produce the correct format. No separate `NormalizeFqn` call is needed.

---

### Finding 5: `n != rootNode` excludes self-referencing types from `NeedsList` check
**Category:** Incorrect Code
**Action:** Amend Plan
**Detail:** The `rootTypeNeedsList` computation filters with `n != rootNode`. For a self-referencing root type (e.g., `TreeNode` with a `TreeNode? Parent` property), the only node is the root itself. Since `n != rootNode` excludes it, `rootNeedsList` evaluates to `false`.

But a self-referencing root type DOES need a list — if `TreeNode.Parent` is a `TreeNode` reference, then when normalizing a `TreeNode`, the denormalizer needs a list of `TreeNode` objects to resolve the `ParentIndex`.

**Recommendation:** The `n != rootNode` filter should be removed, or the self-reference case needs special handling. The correct check should be: "does ANY node (including the root itself) have a property that references the root type?" Change to:
```csharp
var rootNeedsList = allNodes.Any(n =>
    n.Properties.Any(p =>
        (p.Kind == PropertyKind.Normalized && p.TypeFullName == rootFqn) ||
        (p.Kind == PropertyKind.Collection && p.CollectionElementTypeFullName == rootFqn)));
```
Note: This also includes Finding 1's fix for Collection types.

---

### Finding 6: `typeSymbol` variable name doesn't exist in the plan's code context
**Category:** Incorrect Code
**Action:** Amend Plan
**Detail:** The plan's Step 3 code uses `typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)`, but in `TypeGraphAnalyzer.AnalyzeType()`, the parameter is named `type` (line 48), not `typeSymbol`. Using `typeSymbol` would be a compile error.

**Recommendation:** Change `typeSymbol.ToDisplayString(...)` to `type.ToDisplayString(...)` in the plan's Step 3 code, or (better, per Finding 4) use `typeFullName` which is already computed at line 64 and contains the normalized FQN.

---

### Finding 7: Missing test — Root type referenced via Collection property (`List<Root>`)
**Category:** Insufficient Test Coverage
**Action:** Amend Plan
**Detail:** The plan's Step 2 tests say "Root type Person with Address reference (Address refs Person back) -> root NeedsList = true". But this doesn't specify whether the back-reference is via a Normalized property (`Person Ceo`) or a Collection property (`List<Person> Employees`). The Collection case is critical because of Finding 1 — it exercises the `CollectionElementTypeFullName` comparison path. Without this test, Finding 1's bug could ship undetected.

**Recommendation:** Add explicit tests:
- Address has `Person Manager` (Normalized back-ref) -> root `NeedsList = true`
- Address has `List<Person> Contacts` (Collection back-ref) -> root `NeedsList = true`

---

### Finding 8: Missing test — Self-referencing root type
**Category:** Insufficient Test Coverage
**Action:** Amend Plan
**Detail:** The plan's Step 2 tests don't include a self-referencing root type (e.g., `TreeNode` has `TreeNode? Parent`). This case is critical because of Finding 5 — the `n != rootNode` filter would make this return `false`. Without a test, Finding 5's bug could ship undetected.

**Recommendation:** Add test: "Root type `TreeNode` with `TreeNode? Parent` property -> root `NeedsList = true`"

---

### Finding 9: Test for `[NormalizeJsonName]` requires attribute to exist — it's created in Task 2
**Category:** Implicit Assumptions
**Action:** Accept
**Detail:** The Step 1 tests need `[NormalizeJsonName("line")]` on test source code. The attribute is created in Task 2. If Task 2 is incomplete, the tests in Task 5 can't reference the real attribute. However, the Roslyn test compilation includes a source string, so the test source code could define a minimal attribute inline as part of the test source string. The existing test helper `CompileAndGetType` only compiles one source string.

**Recommendation:** The plan should note that test source strings must include a definition of `NormalizeJsonNameAttribute` (since the real one is in a separate project that isn't referenced in test compilations). Something like:
```csharp
var source = """
    namespace DataNormalizer.Attributes;
    [System.AttributeUsage(System.AttributeTargets.Property)]
    public sealed class NormalizeJsonNameAttribute : System.Attribute
    {
        public NormalizeJsonNameAttribute(string name) => Name = name;
        public string Name { get; }
    }
    
    namespace TestApp;
    public class SearchHop
    {
        [DataNormalizer.Attributes.NormalizeJsonName("line")]
        public SearchLine Line { get; set; } = new();
    }
    // ...
    """;
```

---

### Finding 10: Missing test — `[NormalizeJsonName("")]` empty string
**Category:** Insufficient Test Coverage
**Action:** Ask User
**Detail:** The plan tests four cases: attribute with value, config override wins, attribute only, and neither. But it doesn't test `[NormalizeJsonName("")]` (empty string). Should an empty string override be treated as a valid JSON name or as null/no-override?

**Recommendation:** Decision needed: Should `[NormalizeJsonName("")]` produce `JsonNameOverride == ""` (valid, emits `[JsonPropertyName("")]`) or should it be treated as no override (`JsonNameOverride == null`)? Add a test for whichever behavior is chosen.

---

### Finding 11: Missing test — FQN format consistency between ConfigurationParser keys and TypeGraphAnalyzer keys
**Category:** Insufficient Test Coverage
**Action:** Amend Plan
**Detail:** The `PropertyJsonNameOverrides` dictionary is keyed by `"{NormalizeFqn(typeFqn)}.{propName}"` in `ConfigurationParser`, and looked up by `"{GetFullyQualifiedName(type)}.{prop.Name}"` in `TypeGraphAnalyzer`. Both strip `"global::"`, so they should match. But this is a critical cross-component assumption with no integration test.

**Recommendation:** Add a test that creates a scenario with BOTH a config override and an attribute, passes `PropertyJsonNameOverrides` to `TypeGraphAnalyzer.Analyze`, and verifies the config override wins. This tests the key format consistency end-to-end. (Note: this test requires the signature change from Finding 3.)

---

### Finding 12: `TypeGraphNode` equality — new fields don't affect caching
**Category:** Implicit Assumptions
**Action:** Accept
**Detail:** `TypeGraphNode` is a plain `sealed class` with no `IEquatable`, no custom `Equals`, no custom `GetHashCode`. Adding `IsRootType` and `NeedsList` won't affect any equality-based behavior. The incremental generator pipeline operates on `GeneratorOutput` (a `record struct` with string contents), not on `TypeGraphNode` directly. So adding new fields to `TypeGraphNode` has no impact on caching.

**Recommendation:** No change needed. If the pipeline ever restructures to cache at the `TypeGraphNode` level, equality would need to be addressed at that time.

---

### Finding 13: Attribute matching uses `"NormalizeJsonName"` — Roslyn does strip the suffix in some contexts
**Category:** Incorrect Code
**Action:** Accept
**Detail:** The plan checks `a.AttributeClass?.Name is "NormalizeJsonNameAttribute" or "NormalizeJsonName"`. In Roslyn, `INamedTypeSymbol.Name` for an attribute class returns the actual class name — always `"NormalizeJsonNameAttribute"` (the full name), never the shortened form. The C# language allows you to write `[NormalizeJsonName]` in source, but `AttributeClass.Name` still resolves to the full class name `"NormalizeJsonNameAttribute"`.

However, including both forms is harmless (the `"NormalizeJsonName"` branch simply never matches). It's a minor inaccuracy but not a bug.

**Recommendation:** No change strictly needed. Could simplify to just `a.AttributeClass?.Name == "NormalizeJsonNameAttribute"` for clarity, but the existing check is functionally correct.

---

### Finding 14: `propTypeFullName` for Normalized properties may include nullable annotation
**Category:** Implicit Assumptions
**Action:** Accept
**Detail:** For a nullable reference property like `Address? WorkAddress`, `propTypeFullName = GetFullyQualifiedName(propType, fqnCache)` at line 89 gets the type of `propType` which is `Address?`. `SymbolDisplayFormat.FullyQualifiedFormat` does NOT include the `?` suffix (nullable annotations are not part of the display format by default). So `propTypeFullName` will still be `"TestApp.Address"`, and the `rootTypeNeedsList` check would correctly match. No issue here.

**Recommendation:** None needed.

---

### Finding 15: Missing test — `[NormalizeJsonName]` on different PropertyKinds
**Category:** Insufficient Test Coverage
**Action:** Amend Plan
**Detail:** The plan only tests `[NormalizeJsonName]` in isolation (Step 1 doesn't specify which PropertyKind the attributed property has). The `JsonNameOverride` is set on ALL property kinds (Simple, Normalized, Collection, Inlined) within the TypeGraphAnalyzer. The DtoEmitter (Task 7) uses it differently per kind. While TypeGraphAnalyzer should set it uniformly regardless of kind, a test should verify this for at least Normalized and Collection kinds (the two most common targets for JSON name overrides).

**Recommendation:** Add at minimum two test variants:
- `[NormalizeJsonName("line")]` on a Normalized property -> `JsonNameOverride == "line"`
- `[NormalizeJsonName("images")]` on a Collection property's parent (i.e., `List<Image> TransitImages` with `[NormalizeJsonName("images")]`) -> `JsonNameOverride == "images"`

---

### Finding 16: Plan's code uses `typeSymbol` but `AnalyzeType` iterates with `prop` — attribute reading context
**Category:** Incorrect Code
**Action:** Amend Plan
**Detail:** The plan's Step 3 code reads `prop.GetAttributes()` which is correct (reading attributes from the `IPropertySymbol`). But the code is shown as a standalone snippet, not integrated into the existing `foreach (var prop in properties)` loop at `TypeGraphAnalyzer.cs:86`. The plan must specify where exactly this code goes — it should be inside that loop, before the `AnalyzedProperty` construction calls at lines 106, 131, 175, 197, 221, 244, 280, 298, 317.

Since there are 9 different `new AnalyzedProperty { ... }` construction sites, the `JsonNameOverride = jsonNameOverride` must be added to ALL of them. The plan doesn't mention this — it only shows the attribute reading logic, not the wiring into every construction site.

**Recommendation:** Amend the plan to specify: "Add `JsonNameOverride = jsonNameOverride` to ALL `new AnalyzedProperty { ... }` initializations within the `foreach (var prop in properties)` loop. There are currently 9 such sites (lines 106, 131, 175, 197, 221, 244, 280, 298, 317)."

---

## Summary Table

| # | Category | Action | Issue | File:Line | Amendment / Question |
|---|----------|--------|-------|-----------|----------------------|
| 1 | Incorrect Code | **Amend Plan** | `p.TypeFullName` for Collection props is the list type, not element type — `rootTypeNeedsList` always fails for collections | TypeGraphAnalyzer.cs:179 vs Plan Step 4 | Use `p.CollectionElementTypeFullName` for Collection check |
| 2 | Incorrect Code | **Amend Plan** | `TypeGraphNode` uses `init` setters — can't assign `NeedsList` post-construction | TypeGraphNode.cs:5-11 | Compute `NeedsList` before node creation (during DFS, root is last) |
| 3 | Implicit Assumptions | **Amend Plan** | `TypeGraphAnalyzer.Analyze()` has no `NormalizationModel` or `PropertyJsonNameOverrides` param | TypeGraphAnalyzer.cs:10-17, NormalizeGenerator.cs:78-85 | Add parameter and update call site |
| 4 | Implicit Assumptions | **Amend Plan** | `NormalizeFqn` is private to `ConfigurationParser`, not accessible in `TypeGraphAnalyzer` | ConfigurationParser.cs:576, Plan Step 3 | Use `GetFullyQualifiedName` output (already strips `global::`) instead |
| 5 | Incorrect Code | **Amend Plan** | `n != rootNode` excludes self-referencing types from `NeedsList` check | Plan Step 4 | Remove filter or handle self-reference explicitly |
| 6 | Incorrect Code | **Amend Plan** | Plan uses `typeSymbol` but the parameter is named `type` in `AnalyzeType` | TypeGraphAnalyzer.cs:48, Plan Step 3 | Change to `type` or use pre-computed `typeFullName` |
| 7 | Insufficient Test Coverage | **Amend Plan** | No test for Collection back-reference to root (`List<Root>`) | Plan Step 2 | Add test for Collection back-ref separately from Normalized back-ref |
| 8 | Insufficient Test Coverage | **Amend Plan** | No test for self-referencing root type | Plan Step 2 | Add test: `TreeNode` with `TreeNode? Parent` -> `NeedsList = true` |
| 9 | Implicit Assumptions | **Accept** | Test source must include attribute definition since real attribute is in separate project | Plan Step 1 | Test source strings need inline `NormalizeJsonNameAttribute` class |
| 10 | Insufficient Test Coverage | **Ask User** | `[NormalizeJsonName("")]` empty string behavior unspecified | Plan Step 1 | Decide: empty string = valid override or null? |
| 11 | Insufficient Test Coverage | **Amend Plan** | No integration test for FQN format consistency between parser keys and analyzer lookup | Plan Step 1 | Add cross-component key-matching test |
| 12 | Implicit Assumptions | **Accept** | `TypeGraphNode` has no equality — new fields don't affect caching | TypeGraphNode.cs:5-11 | No impact on current pipeline |
| 13 | Incorrect Code | **Accept** | `"NormalizeJsonName"` branch in attribute check never matches (harmless) | Plan Step 3 | Functionally correct, cosmetically imprecise |
| 14 | Implicit Assumptions | **Accept** | Nullable reference types don't affect FQN format | TypeGraphAnalyzer.cs:634 | Confirmed safe |
| 15 | Insufficient Test Coverage | **Amend Plan** | No test for `[NormalizeJsonName]` on different PropertyKinds | Plan Step 1 | Add tests for Normalized and Collection property kinds |
| 16 | Incorrect Code | **Amend Plan** | `JsonNameOverride` must be wired into ALL 9 `new AnalyzedProperty` sites | TypeGraphAnalyzer.cs (9 sites) | Plan must specify adding `JsonNameOverride = jsonNameOverride` to all 9 construction sites |

**No issues found in:** (none — all investigated categories had findings)

## Critical Path

The following findings must be fixed before implementation begins, in priority order:

1. **Finding 2 (init setters)** — The plan's architecture for `NeedsList` doesn't compile. Must restructure.
2. **Finding 1 (Collection TypeFullName)** — The `rootTypeNeedsList` logic is wrong for collections. Will silently produce incorrect results.
3. **Finding 5 (self-reference)** — The `n != rootNode` filter causes incorrect `NeedsList = false` for self-referencing root types.
4. **Finding 3 (missing parameter)** — The plan's code won't compile without a signature change.
5. **Finding 4 (NormalizeFqn)** — The plan references a function that's inaccessible.
6. **Finding 16 (9 construction sites)** — Without explicit guidance, an implementer may only wire `JsonNameOverride` into one or two `AnalyzedProperty` sites.
