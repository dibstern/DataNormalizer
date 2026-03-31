# Audit: Task 8 — Update NormalizerEmitter for Root Property

**Auditor:** Claude (automated)
**Date:** 2026-03-25
**Scope:** Task 8 of the JSON Contract Implementation Plan

**Summary:** Task 8 modifies `NormalizerEmitter.EmitPublicNormalizeMethod` to emit `result.Result = __{rootCamel}Arr[0]` and conditionally emit the root type's list property based on `rootNode.NeedsList`. The primary issues are: (1) **`TypeGraphNode` does not yet have `NeedsList` or `IsRootType` properties** — Task 5 is responsible for adding them, creating a hard dependency that the plan implicitly acknowledges but Task 8's test code must construct nodes with; (2) **`result.Result = __{rootCamel}Arr[0]` will fail at runtime if the root collection is empty**, which cannot happen in normal operation but deserves consideration; (3) **several existing tests will break** because the current emitter always emits `result.{listProp}` for all nodes including root, and adding `result.Result` changes the output for every test scenario; (4) **test helper `CreateNode` must be updated** to accept `NeedsList`/`IsRootType`.

---

## Findings

### Finding 1: `TypeGraphNode` does not have `NeedsList` or `IsRootType` — hard dependency on Task 5

**Category:** Implicit Assumptions
**Action:** Amend Plan
**File:** `src/DataNormalizer.Generators/Models/TypeGraphNode.cs:5-11`

**Detail:** The current `TypeGraphNode` has only four properties:
```csharp
public required string TypeFullName { get; init; }
public required string TypeName { get; init; }
public required ImmutableArray<AnalyzedProperty> Properties { get; init; }
public bool HasCircularReference { get; init; }
```

Task 8's plan says: "If `rootNode.NeedsList`: also emit `result.{rootListProp} = __{rootCamel}Arr;`" — this requires `NeedsList` on `TypeGraphNode`, which is added in Task 5. The plan correctly sequences Task 5 before Task 8, but Task 8's description does not explicitly mention the dependency or that it requires Task 5 to be completed first.

Additionally, the NormalizerEmitter currently iterates `allNodes` and emits list properties for ALL nodes (lines 84-97 of NormalizerEmitter.cs). The conditional skip logic needs to check `rootNode.NeedsList` and also needs to know which node IS the root (since the for-loop iterates all nodes). The emitter already has access to `rootNode` as a parameter to `EmitPublicNormalizeMethod`, so comparing `node.TypeFullName == rootNode.TypeFullName` inside the loop or adding a flag is needed.

**Recommendation:** Amend the plan to:
1. Explicitly state "Prerequisite: Task 5 must be completed (TypeGraphNode has `NeedsList` and `IsRootType` properties)"
2. Clarify the loop logic: when iterating `allNodes` in `EmitPublicNormalizeMethod` (lines 84-97), for the node matching the root, skip the list assignment if `!rootNode.NeedsList`, and always emit `result.Result = __{rootCamel}Arr[0];` after the loop

---

### Finding 2: The for-loop emitting list properties (lines 84-97) needs restructuring — not just a one-line addition

**Category:** Incorrect Code
**Action:** Amend Plan
**File:** `src/DataNormalizer.Generators/Emitters/NormalizerEmitter.cs:84-97`

**Detail:** The current code in `EmitPublicNormalizeMethod` is:

```csharp
for (var i = 0; i < allNodes.Count; i++)
{
    var node = allNodes[i];
    var dtoFullName = EmitterHelpers.GetDtoFullName(...);
    var typeKey = EmitterHelpers.GetTypeKey(node, model);
    var camel = EmitterHelpers.ToCamelCase(node.TypeName);
    var listPropertyName = EmitterHelpers.GetListPropertyName(...);

    sb.AppendLine($"        var __{camel}Col = context.GetCollection<{dtoFullName}>(\"{typeKey}\");");
    sb.AppendLine($"        var __{camel}Arr = new {dtoFullName}[__{camel}Col.Count];");
    sb.AppendLine($"        for (var __i = 0; __i < __{camel}Col.Count; __i++)");
    sb.AppendLine($"            __{camel}Arr[__i] = __{camel}Col[__i];");
    sb.AppendLine($"        result.{listPropertyName} = __{camel}Arr;");
}
```

The plan says "Always emit `result.Result = __{rootCamel}Arr[0];`" but the `__{rootCamel}Arr` variable is created INSIDE the for-loop, scoped to one iteration. The plan needs to specify:

1. The root node's collection/array variables must still be created (the `GetCollection` + copy-to-array lines), even when `NeedsList == false`
2. The `result.{listPropertyName} = __{camel}Arr;` line should be conditionally skipped for the root when `!NeedsList`
3. `result.Result = __{rootCamel}Arr[0];` should be emitted (either inside the loop when processing the root node, or after the loop using the variable that's still in scope since there are no braces creating a separate scope — note the loop body is in a single block so `__{camel}Arr` is accessible)

Wait — actually, re-reading the code: the variables ARE in the same method scope because there are no `{}` creating inner blocks for each iteration. But there IS a problem: each iteration reuses `__` prefixed variable names (`__{camel}Col`, `__{camel}Arr`) where `camel` differs per type. So `__{rootCamel}Arr` IS accessible after the loop. The plan's approach is viable, but it needs to clarify WHERE the `result.Result` assignment goes. It should go after the loop completes, or it needs to be identified by checking if the current node is the root inside the loop.

**Recommendation:** Amend the plan to explicitly describe the code structure:
- Inside the for-loop: for each node, emit the `GetCollection` + array copy as before
- Inside the for-loop: for the root node, skip `result.{listPropertyName} = __{camel}Arr;` if `!rootNode.NeedsList`
- After the for-loop: emit `result.Result = __{rootCamel}Arr[0];` (where `rootCamel` is computed from the root node's TypeName)

---

### Finding 3: `__{rootCamel}Arr[0]` — empty array would cause IndexOutOfRangeException at runtime

**Category:** Incorrect Code
**Action:** Ask User

**Detail:** The plan emits `result.Result = __{rootCamel}Arr[0]` unconditionally. If the root's collection is empty (0 elements), this is an `IndexOutOfRangeException` at runtime.

**Is this possible?** The root type is always the first object processed by `Normalize{RootType}(source, context)` (line 80 of current NormalizerEmitter.cs). That call immediately creates a DTO and calls `GetOrAddIndexAndStore`, which adds it to the collection. So in normal operation, the root collection always has at least 1 element.

However:
- If `source` is null, the generated `Normalize{RootType}` method doesn't guard against it — it would NRE before reaching GetOrAddIndexAndStore
- The `DenormalizerEmitter` already does `return {plural}[0]` (line 347 of DenormalizerEmitter.cs), establishing the pattern that index 0 is trusted

So the empty-array case is unreachable in practice. But the question is whether Task 8 should add a defensive check or accept the implicit guarantee.

**Recommendation:** Ask user: Should the generated normalizer add a null guard for the source parameter (`if (source is null) throw new ArgumentNullException(...)`) to make the contract explicit? Or accept that `[0]` is safe because the normalizer always processes the source? The DenormalizerEmitter already trusts `[0]` without a guard, so consistency argues for accepting it.

---

### Finding 4: Existing NormalizerEmitter tests will break and need updating — plan doesn't mention this

**Category:** Implicit Assumptions
**Action:** Amend Plan

**File:** `tests/DataNormalizer.Generators.Tests/Emitters/NormalizerEmitterTests.cs` (lines 28-48, 82-88, 370-388, etc.)

**Detail:** Multiple existing tests assert the current behavior where every type (including root) gets a list property. After Task 8's changes:

1. **`Emit_SimpleFlatType_GeneratesNormalizeMethodAndHelper` (line 13):** Asserts `result.PersonDtos = `. After Task 8, if the root has `NeedsList = false` (default), this assertion would fail. The test creates a node WITHOUT `NeedsList` set, so it defaults to `false`.

2. **`Emit_NestedNormalizableType_GeneratesTwoHelpers` (line 52):** Asserts `result.PersonDtos = ` and `result.AddressDtos = `. Person (root) with `NeedsList = false` would NOT emit `result.PersonDtos`.

3. **`Emit_PublicNormalizeMethodSignature_HasCorrectReturnTypeAndParameter` (line 365):** Asserts `result.OrderDtos = `. Same issue.

4. **`Emit_WithCustomName_UsesCustomCollectionKey` (line 391):** Asserts `result.PersonDtos = `.

5. **`Emit_MultipleRootTypes_GeneratesOverloadsForEach` (line 306):** Both root types would need `NeedsList` handling.

The plan's Task 11 says "Update all existing tests for root property change" but Task 8 says "Modify: `tests/.../NormalizerEmitterTests.cs`" — so it seems like Task 8 is supposed to update the NormalizerEmitter tests as part of the task, not defer to Task 11. But the plan only specifies 4 new test scenarios and doesn't mention fixing the existing 15+ tests that will break.

**Recommendation:** Amend the plan to:
1. Explicitly state that all existing `NormalizerEmitterTests` that assert on `result.{rootListProp}` must be updated
2. The `CreateNode` helper and test scenarios need to set `NeedsList` (defaulting to `false` means root types won't get list assignments)
3. List which existing tests need modification (at minimum: `Emit_SimpleFlatType`, `Emit_NestedNormalizableType`, `Emit_PublicNormalizeMethodSignature`, `Emit_WithCustomName`, `Emit_MultipleRootTypes`, `Emit_SingleRootType`)
4. Each updated test should also assert `result.Result = `

---

### Finding 5: Test helper `CreateNode` must be updated for `NeedsList` and `IsRootType`

**Category:** Missing Wiring
**Action:** Amend Plan

**File:** `tests/DataNormalizer.Generators.Tests/Emitters/NormalizerEmitterTests.cs:675-699`

**Detail:** The test file has two `CreateNode` overloads:

```csharp
private static TypeGraphNode CreateNode(string fullName, string name, params AnalyzedProperty[] props)
private static TypeGraphNode CreateNode(string fullName, string name, bool hasCircularReference, params AnalyzedProperty[] props)
```

Neither sets `NeedsList` or `IsRootType`. After Task 5 adds these properties to `TypeGraphNode`, the test helpers need updating. Options:
- Add new overloads with `bool needsList` and `bool isRootType` parameters
- Add a named-parameter approach
- Set them on specific test nodes after construction

Also, the `ModelFactories` shared helpers (`tests/DataNormalizer.Generators.Tests/TestUtilities/ModelFactories.cs:8-32`) need the same update. The Task 1 audit (Finding 7) already flagged this as a Task 5 concern.

**Recommendation:** Amend the plan to add a step: "Update `CreateNode` helpers in NormalizerEmitterTests (and optionally ModelFactories) to support `NeedsList` and `IsRootType` parameters."

---

### Finding 6: Root identification inside the for-loop needs a comparison strategy

**Category:** Implicit Assumptions
**Action:** Amend Plan

**File:** `src/DataNormalizer.Generators/Emitters/NormalizerEmitter.cs:84-97`

**Detail:** The for-loop iterates all nodes. To conditionally skip the root's list property, the code needs to determine which node is the root. There are two approaches:

1. **Compare `node.TypeFullName == rootNode.TypeFullName`** — simple but relies on string comparison
2. **Use `node.IsRootType`** from the new property (added in Task 5)

Option 2 has a subtle issue: in multi-root scenarios, `EmitPublicNormalizeMethod` is called once per root with a `rootSpecificNodes` list. If multiple types have `IsRootType = true` (because they're roots in different graphs), and `rootSpecificNodes` includes only nodes for one graph, then checking `IsRootType` could incorrectly match a different root's node. However, examining the code flow, `rootSpecificNodes` is the `allNodes` parameter passed to `EmitPublicNormalizeMethod`, and each call processes one specific root. So either approach works, but comparing against the passed-in `rootNode` parameter is more precise.

**Recommendation:** Amend the plan to specify: "Use `node.TypeFullName == rootNode.TypeFullName` to identify the root node inside the for-loop" (rather than relying on `IsRootType` flag which could be ambiguous in multi-root scenarios).

---

### Finding 7: Missing test — root type with `NeedsList = true` should emit BOTH `Result` AND the list

**Category:** Insufficient Test Coverage
**Action:** Accept (partially covered)

**Detail:** The plan lists this test: "Root with NeedsList = true -> both Result and list property". This is good. However, the test should verify:
1. `result.Result = __{rootCamel}Arr[0]` is emitted
2. `result.{rootListProp} = __{rootCamel}Arr` is ALSO emitted
3. The `Result` assignment references the SAME array variable as the list assignment
4. Other types' list properties are unchanged

The plan's test description covers items 1-2 but doesn't explicitly call out item 3 (same array variable). This is probably fine since the emitter would naturally use the same variable, but worth verifying.

**Recommendation:** No plan change needed. The string assertion `Does.Contain("result.Result = __personArr[0]")` implicitly verifies it references the same variable.

---

### Finding 8: Missing test — root with self-reference (circular) and `NeedsList = true`

**Category:** Insufficient Test Coverage
**Action:** Amend Plan

**Detail:** The plan doesn't include a test for a root type that has a circular self-reference. For example, a `TreeNode` as root where `TreeNode.Parent` references `TreeNode` — this means:
- `TreeNode` has `HasCircularReference = true`
- `TreeNode` as root has `NeedsList = true` (because `TreeNode` is referenced by another node — itself)
- The normalizer must emit both `result.Result` and `result.TreeNodeDtos`

This combines the circular-reference two-DTO pattern with the new root property logic. The existing circular tests (`Emit_CircularReference_*`) use `TreeNode` as root but don't set `NeedsList = true`. After Task 8, those tests need updating AND a new test should verify the combination works.

**Recommendation:** Add test: "Root type with self-reference (circular) — `NeedsList = true` — normalizer emits `result.Result` AND `result.{listProp}` AND uses two-DTO pattern correctly."

---

### Finding 9: The `result.Result` assignment type — is `Result` property typed as DTO or original type?

**Category:** Implicit Assumptions
**Action:** Accept

**File:** Plan Task 6 (ContainerEmitter), cross-referenced with Task 8

**Detail:** Task 6 specifies the container will have:
```csharp
public {RootDtoFullName} Result { get; set; }
```

So `Result` is of the DTO type (e.g., `PersonDto`). The normalizer emits `result.Result = __{rootCamel}Arr[0]` where `__{rootCamel}Arr` is a `{DtoFullName}[]`. So `Arr[0]` is a `DtoFullName` instance, matching the `Result` property type. This is consistent.

The DenormalizerEmitter (Task 9) will need to read `normalized.Result` as a DTO and denormalize from there. This is a Task 9 concern.

**Recommendation:** No change needed. Types are consistent.

---

### Finding 10: Variable naming for `result.Result = __{rootCamel}Arr[0]` — the `rootCamel` variable must be computed

**Category:** Missing Wiring
**Action:** Amend Plan

**File:** `src/DataNormalizer.Generators/Emitters/NormalizerEmitter.cs:67-101`

**Detail:** In `EmitPublicNormalizeMethod`, the `Result` assignment needs to reference the root node's camelCase array variable (`__{rootCamel}Arr`). This variable name is computed during the for-loop iteration when processing the root node. The plan must clarify that:

1. Before or after the for-loop, compute `var rootCamel = EmitterHelpers.ToCamelCase(rootNode.TypeName);`
2. After the for-loop, emit `sb.AppendLine($"        result.Result = __{rootCamel}Arr[0];");`

The variable `__{rootCamel}Arr` exists in the generated code's scope from the loop iteration that processed the root node. Since all the `sb.AppendLine` calls create lines in the same method body, the generated variable is in scope.

**Recommendation:** Amend the plan to add explicit Step: "Compute `rootCamel = EmitterHelpers.ToCamelCase(rootNode.TypeName)` and emit `result.Result = __{rootCamel}Arr[0];` after the for-loop."

---

### Finding 11: Emitter uses `allNodes` for `GetListPropertyName` — disambiguation still works for root

**Category:** Implicit Assumptions
**Action:** Accept

**File:** `src/DataNormalizer.Generators/Emitters/EmitterHelpers.cs:132-164`

**Detail:** `GetListPropertyName` needs the full `allNodes` list to check for duplicate TypeName disambiguation. When `NeedsList = false` for root, the root's list property isn't emitted, but `GetListPropertyName` would still count the root when checking for name collisions among OTHER types. This is correct — the disambiguation logic should consider all types in the graph regardless of whether they have lists, because the same naming scheme is used for container properties, DTO collections, etc.

**Recommendation:** No change needed.

---

## Summary Table

| # | Category | Action | Issue | File:Line | Amendment / Question |
|---|----------|--------|-------|-----------|----------------------|
| 1 | Implicit Assumptions | **Amend Plan** | `TypeGraphNode` lacks `NeedsList`/`IsRootType` — hard dep on Task 5 not stated | TypeGraphNode.cs:5-11 | Explicitly state Task 5 prerequisite in Task 8 description |
| 2 | Incorrect Code | **Amend Plan** | Plan doesn't describe WHERE `result.Result` is emitted relative to the for-loop | NormalizerEmitter.cs:84-97 | Describe: skip root list inside loop, emit `result.Result` after loop |
| 3 | Incorrect Code | **Ask User** | `__{rootCamel}Arr[0]` on empty array — impossible in practice but no guard | NormalizerEmitter.cs (new code) | Should generated normalizer add null/empty guard for source param? |
| 4 | Implicit Assumptions | **Amend Plan** | Existing NormalizerEmitter tests will break — plan doesn't list which ones | NormalizerEmitterTests.cs:13-420 | List the ~6 existing tests that assert on root list props and need updating |
| 5 | Missing Wiring | **Amend Plan** | `CreateNode` test helpers need `NeedsList`/`IsRootType` parameters | NormalizerEmitterTests.cs:675-699 | Add step: "Update CreateNode helpers to accept NeedsList/IsRootType" |
| 6 | Implicit Assumptions | **Amend Plan** | Root identification in for-loop needs explicit comparison strategy | NormalizerEmitter.cs:84-97 | Specify: compare `node.TypeFullName == rootNode.TypeFullName` not `IsRootType` |
| 7 | Insufficient Test Coverage | Accept | Test for `NeedsList = true` should verify same array variable for Result and list | — | Implicitly covered by string assertion on variable name |
| 8 | Insufficient Test Coverage | **Amend Plan** | Missing test for circular self-referencing root with `NeedsList = true` | NormalizerEmitterTests.cs | Add test: self-referencing root type with `NeedsList = true` |
| 9 | Implicit Assumptions | Accept | `Result` property typed as DTO — consistent with container emitter | — | — |
| 10 | Missing Wiring | **Amend Plan** | `rootCamel` variable computation and placement not specified | NormalizerEmitter.cs:67-101 | Add explicit step: compute rootCamel, emit Result assignment after loop |
| 11 | Implicit Assumptions | Accept | `GetListPropertyName` disambiguation still correct when root list omitted | EmitterHelpers.cs:132-164 | — |

**No issues found in:** State Issues (the normalizer's dedup context is unchanged — the `Result` assignment is purely an output wiring change, not a state change)

**Critical items requiring plan amendment:** Findings 1, 2, 4, 5, 6, 8, 10
**Items requiring user decision:** Finding 3
