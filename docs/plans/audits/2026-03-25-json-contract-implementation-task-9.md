# Audit: Task 9 — Update DenormalizerEmitter for Root Property

**Auditor:** Claude (automated)
**Date:** 2026-03-25
**Scope:** Task 9 of the JSON Contract Implementation Plan

**Summary:** Task 9 updates the denormalizer to read the root from `normalized.Result` instead of from a list, and creates local arrays for reference resolution when `NeedsList = false`. There are several significant issues: (1) the plan conflates DTO variable names with source-object variable names, creating a conceptual gap that must be carefully resolved; (2) the `EmitGetCollections` and `EmitPass1` methods both need modification but the plan only describes changes to `EmitDenormalizeMethod`; (3) the `new[] { rootDto }` approach is correct ONLY when no other type (and not root itself) references root, which is guaranteed by the `NeedsList = false` invariant but deserves explicit verification; (4) the plan relies on `NeedsList`/`IsRootType` from Task 5 but doesn't state this dependency; (5) multiple existing tests will break.

---

## Findings

### Finding 1: Plan conflates DTO array (`{camel}Dtos`) and source object array (`{plural}`) variable names

**Category:** Incorrect Code
**Action:** Amend Plan
**Files:** `src/DataNormalizer.Generators/Emitters/DenormalizerEmitter.cs:86-133`

**Detail:** The plan says:
> If `!rootNode.NeedsList`: `var {rootPlural} = new[] { rootDto };` (local array for ref resolution)

The current denormalizer has TWO separate variables per type:
1. `{camel}Dtos` — the DTO array read from the container (e.g., `personDtos`), created in `EmitGetCollections` (line 97)
2. `{plural}` — the reconstructed source objects array (e.g., `persons`), created in `EmitPass1` (line 114)

Reference resolution in Pass 2 uses `{plural}` (source objects), NOT `{camel}Dtos`. But `rootDto` (from `normalized.Result`) is a **DTO**, not a source object. Writing `var persons = new[] { rootDto }` would be a type mismatch — `persons` is `Person[]` but `rootDto` is `PersonDto`.

What the plan actually needs is:
1. **Replace the `EmitGetCollections` line for root**: Instead of `var personDtos = normalized.PersonDtos;`, emit either:
   - `NeedsList = false`: `var personDtos = new[] { normalized.Result };` (wraps the single DTO in an array)
   - `NeedsList = true`: `var personDtos = normalized.PersonDtos;` (reads from list as before)
2. **Leave `EmitPass1` unchanged**: It creates `persons` from `personDtos` as normal
3. **Replace `EmitRootResolution`**: Instead of `return persons[0]`, use the reconstructed root from Pass 1

The `var {rootPlural} = new[] { rootDto }` line in the plan is unclear at best and a type error at worst. The plan must clearly distinguish between the DTO array variable and the source object array variable.

**Recommendation:** Amend the plan's Step 3 to:
- In `EmitGetCollections`: for root node, emit `var {camel}Dtos = new[] { normalized.Result };` (when `!NeedsList`) or `var {camel}Dtos = normalized.{rootListProp};` (when `NeedsList`). All other types unchanged.
- `EmitPass1` and `EmitPass2`: unchanged (they work on `{camel}Dtos` and `{plural}` respectively).
- `EmitRootResolution`: change to `return {plural}[0];` — which is actually already the current code (line 347). Or if desired, track the root object separately, but `[0]` is still correct since the root is always index 0 in its array.

---

### Finding 2: `EmitGetCollections` needs `rootNode` parameter — current signature doesn't have it

**Category:** Missing Wiring
**Action:** Amend Plan
**File:** `src/DataNormalizer.Generators/Emitters/DenormalizerEmitter.cs:86-99`

**Detail:** The current `EmitGetCollections` signature is:
```csharp
private static void EmitGetCollections(
    StringBuilder sb,
    IReadOnlyList<TypeGraphNode> allNodes,
    NamingModel naming)
```

To conditionally emit different code for the root node, this method needs to know WHICH node is the root and whether `NeedsList` is true/false. The plan says changes are in `EmitDenormalizeMethod` but doesn't specify that `EmitGetCollections` (called from `EmitDenormalizeMethod` at line 69) needs modification.

Options:
1. Pass `rootNode` as an additional parameter to `EmitGetCollections`
2. Handle the root node's collection read in `EmitDenormalizeMethod` before calling `EmitGetCollections`, and skip the root in `EmitGetCollections`
3. Use the `IsRootType` flag on the node within `EmitGetCollections`

**Recommendation:** Amend the plan to specify that `EmitGetCollections` is modified to accept `rootNode` (or use `node.IsRootType` / `node.NeedsList` flags) and emit the appropriate code for the root vs non-root nodes.

---

### Finding 3: `EmitPass1` uses `{camel}Dtos.Length` — works correctly with `new[] { normalized.Result }`

**Category:** Implicit Assumptions
**Action:** Accept

**File:** `src/DataNormalizer.Generators/Emitters/DenormalizerEmitter.cs:114`

**Detail:** `EmitPass1` generates: `var {plural} = new {node.TypeFullName}[{camel}Dtos.Length];`

When `NeedsList = false`, the generated code would be `var persons = new Person[personDtos.Length]` where `personDtos = new[] { normalized.Result }`. Since `personDtos.Length == 1`, this creates a single-element array. The root DTO at index 0 gets reconstructed into `persons[0]`. This works correctly because when `NeedsList = false`, only one root object exists (no other type references it for index-based lookups beyond index 0).

The `for (var i = 0; i < {camel}Dtos.Length; i++)` loop also works correctly for a single-element array.

**Recommendation:** No change needed. Pass 1 works transparently with a single-element DTO array.

---

### Finding 4: `EmitPass2` reference resolution — `new[] { rootDto }` only has index 0, indices > 0 would fail

**Category:** Incorrect Code
**Action:** Amend Plan (cross-task concern with Task 5)

**File:** `src/DataNormalizer.Generators/Emitters/DenormalizerEmitter.cs:174-196`

**Detail:** When `NeedsList = false`, the root DTO array has exactly 1 element. Pass 2 resolves references via `{targetPlural}[idx]`. If any DTO has an index > 0 pointing into the root's array, it would be an `IndexOutOfRangeException`.

The `NeedsList = false` invariant guarantees that NO other type references root, so no other type's Pass 2 will index into the root's array. However, there's a subtle edge case:

**Self-referencing root types:** A `TreeNode` with `Parent: TreeNode?` and `Children: List<TreeNode>` is the only type in the graph. Task 5's computation `allNodes.Any(n => n != rootNode && ...)` returns `false` (no OTHER node references root), so `NeedsList = false`. But the normalizer produces an array of ALL TreeNode DTOs (could be many). With `NeedsList = false`, the container only has `Result` (one DTO), and the denormalizer creates a 1-element array. Pass 2 for TreeNode tries to resolve `Parent` and `Children` indices — pointing to indices that don't exist.

This is fundamentally a Task 5 design bug (the `NeedsList` computation should include self-references), but Task 9 would manifest the runtime failure. Task 9's tests should cover or explicitly exclude this scenario.

**Recommendation:** 
1. File a cross-task note: Task 5's `NeedsList` computation must include self-references (`n != rootNode` exclusion is wrong for self-referencing types)
2. Task 9 should add a test verifying denormalization works for self-referencing root with `NeedsList = true` (after Task 5 is fixed)
3. Task 9 can safely assume `NeedsList` is computed correctly by Task 5 — if `NeedsList = false`, then truly no index > 0 references into root's array

---

### Finding 5: `TypeGraphNode` doesn't yet have `NeedsList` or `IsRootType` — hard dependency on Task 5

**Category:** Implicit Assumptions
**Action:** Amend Plan
**File:** `src/DataNormalizer.Generators/Models/TypeGraphNode.cs:5-11`

**Detail:** The current `TypeGraphNode` has:
```csharp
public required string TypeFullName { get; init; }
public required string TypeName { get; init; }
public required ImmutableArray<AnalyzedProperty> Properties { get; init; }
public bool HasCircularReference { get; init; }
```

No `NeedsList` or `IsRootType`. Task 5 adds these. Task 9 depends on them but doesn't state the dependency.

**Recommendation:** Amend the plan to state: "Prerequisite: Task 5 must be completed (TypeGraphNode has `NeedsList` and `IsRootType` properties)."

---

### Finding 6: `EmitRootResolution` currently returns `{plural}[0]` — plan says to change to `rootDto`

**Category:** Incorrect Code
**Action:** Amend Plan
**File:** `src/DataNormalizer.Generators/Emitters/DenormalizerEmitter.cs:343-348`

**Detail:** The plan says:
> Root resolution: use `rootDto` instead of `{rootPlural}[0]`

The current code at line 347 is: `sb.AppendLine($"        return {plural}[0];");`

`{plural}[0]` is a reconstructed source object (e.g., `persons[0]` of type `Person`). Replacing this with `rootDto` (which is a DTO of type `PersonDto`) would be a type mismatch — the method returns `Person`, not `PersonDto`.

If the plan intends to cache a reference to the reconstructed root (e.g., `var root = {plural}[0]; return root;`), that's fine but unnecessary. The current `return {plural}[0]` already works correctly in all cases:
- `NeedsList = false`: `personDtos = new[] { normalized.Result }`, Pass 1 creates `persons[0]` from it, `return persons[0]` works
- `NeedsList = true`: `personDtos = normalized.PersonDtos`, Pass 1 creates `persons` array, `return persons[0]` works (root is always index 0)

**Recommendation:** Amend the plan: `EmitRootResolution` should remain `return {plural}[0]`. There is no need to change it. The root is always at index 0 in its type's array (guaranteed by the normalizer processing the root first via `Normalize{RootType}(source, context)` in `EmitPublicNormalizeMethod`). Remove the plan's instruction to "use `rootDto` instead of `{rootPlural}[0]`" as it conflates DTO and source object types.

---

### Finding 7: Existing DenormalizerEmitter tests will break — plan doesn't enumerate them

**Category:** Implicit Assumptions
**Action:** Amend Plan
**File:** `tests/DataNormalizer.Generators.Tests/Emitters/DenormalizerEmitterTests.cs`

**Detail:** Multiple existing tests assert on the current behavior where ALL types (including root) get their DTOs from `normalized.{listProp}`. After Task 9's changes:

1. **`Emit_SimpleFlatType_GeneratesDenormalizeMethod` (line 13):** Asserts `Does.Contain("TestApp.PersonResultDto normalized")` — still works.
2. **`Emit_NestedNormalizableType_GetsDtoCollections` (line 117):** Asserts `normalized.PersonDtos` and `normalized.AddressDtos`. If root (Person) has `NeedsList = false`, the generated code would NOT contain `normalized.PersonDtos` — it would have `new[] { normalized.Result }` instead. This test breaks.
3. **`Emit_RootResolution_UsesIndexZero` (line 106):** Asserts `persons[0]`. If `EmitRootResolution` changes per the plan (to use `rootDto`), this breaks. If we keep `persons[0]` (per Finding 6), it stays.
4. **`Emit_WithCustomName_UsesTypeNameForContainerProperty` (line 172):** Asserts `normalized.PersonDtos`. Same issue as #2.

Additionally, the test helpers (`CreateNode` at line 350) don't set `NeedsList` or `IsRootType` — they default to `false`. This means all existing tests implicitly have `NeedsList = false` for all nodes, which changes the root node behavior.

**Recommendation:** Amend the plan to:
1. List which existing tests need modification (at minimum: tests at lines 117, 172)
2. Update `CreateNode` helper to support `NeedsList` and `IsRootType` parameters
3. Each test scenario that involves root + other types should set root's `NeedsList` appropriately

---

### Finding 8: Task 5's `NeedsList` computation has a bug with Collection properties

**Category:** Incorrect Code (cross-task)
**Action:** Amend Plan (flag for Task 5)

**File:** Plan line 408-411

**Detail:** Task 5's NeedsList computation checks `p.TypeFullName == rootFqn` for Collection properties. But for Collection properties, `TypeFullName` is the COLLECTION type (e.g., `System.Collections.Generic.List<TestApp.Person>`), not the element type. The element type is in `CollectionElementTypeFullName`.

So a scenario like: `Address` has property `Owners: List<Person>` (root = Person). The check `p.TypeFullName == "TestApp.Person"` on the Collection property would check `"System.Collections.Generic.List<TestApp.Person>" == "TestApp.Person"` which is FALSE. NeedsList would incorrectly be false.

This is a Task 5 bug but directly impacts Task 9's correctness. If NeedsList is wrong, the denormalizer generates incorrect code.

**Recommendation:** Flag for Task 5 audit/amendment: the NeedsList computation for Collection properties must check `p.CollectionElementTypeFullName == rootFqn` in addition to `p.TypeFullName == rootFqn`. This is critical for Task 9's correct behavior.

---

### Finding 9: Missing test — Denormalization with `NeedsList = true` and root appearing at various indices

**Category:** Insufficient Test Coverage
**Action:** Amend Plan

**Detail:** The plan's test list doesn't include a scenario where `NeedsList = true` and the root's list has multiple DTOs. In this case, `normalized.Result` is the entry-point DTO, but `normalized.{rootListProp}` has multiple root-type DTOs (e.g., Person referenced by Address). The denormalizer should use the list for Pass 1/2 and return the object reconstructed from `Result` (which should be at index 0 in the list).

The test should verify:
1. Pass 1 iterates the full list (not just Result)
2. Pass 2 resolves references into the full list
3. Return value is `{plural}[0]` (same object as reconstructed from `Result`)

**Recommendation:** Add test: "Root with `NeedsList = true` — denormalizer reads from list for Pass1/Pass2, returns `{plural}[0]`."

---

### Finding 10: Missing test — null `Result` handling (defensive)

**Category:** Insufficient Test Coverage
**Action:** Ask User

**Detail:** The plan doesn't include a test for when `normalized.Result` is null. Since `Result` is a DTO property on the container, it could technically be null if the container is deserialized from JSON with a missing `result` field. The generated denormalizer would produce `new[] { null }` for the DTO array, then Pass 1 would NRE when accessing `personDtos[i].Name`.

Should the generated denormalizer have a null guard (e.g., `if (normalized.Result is null) throw new ArgumentException("Result cannot be null")`)?

**Recommendation:** Ask user: Should the generated denormalizer add a null guard for `normalized.Result`? This would only matter during deserialization of malformed JSON — normal normalize-then-denormalize roundtrips always produce a non-null Result.

---

### Finding 11: Missing test — roundtrip (normalize then denormalize produces identical graph)

**Category:** Insufficient Test Coverage
**Action:** Accept

**Detail:** A roundtrip test (normalize then denormalize) is more of an integration test than a unit test for the emitter. Task 9's emitter tests verify the generated CODE text, not its runtime behavior. Integration roundtrip tests belong in Task 11 or the integration test suite.

**Recommendation:** No change needed for Task 9. Roundtrip tests are covered by integration tests elsewhere.

---

### Finding 12: Root node identification — EmitGetCollections iterates allNodes and needs to distinguish root

**Category:** Missing Wiring
**Action:** Amend Plan
**File:** `src/DataNormalizer.Generators/Emitters/DenormalizerEmitter.cs:86-99`

**Detail:** The current `EmitGetCollections` iterates all nodes uniformly. To handle root specially, the method needs to either:
1. Accept `rootNode` as a parameter and compare `node.TypeFullName == rootNode.TypeFullName` 
2. Use `node.IsRootType` and `node.NeedsList` flags

The plan's Step 3 says "In `EmitDenormalizeMethod`" but the actual code changes need to happen in the helper methods called BY `EmitDenormalizeMethod`. The plan needs to specify which helper methods are modified and how root identification works.

**Recommendation:** Amend Step 3 to describe changes to `EmitGetCollections` specifically: pass `rootNode` as parameter, and for the root node, conditionally emit either `var {camel}Dtos = new[] { normalized.Result };` or `var {camel}Dtos = normalized.{rootListProp};`.

---

## Summary Table

| # | Category | Action | Issue | File:Line | Amendment / Question |
|---|----------|--------|-------|-----------|----------------------|
| 1 | Incorrect Code | **Amend Plan** | Plan conflates DTO array and source object array variables — `var {rootPlural} = new[] { rootDto }` is a type mismatch | DenormalizerEmitter.cs:86-133 | Rewrite Step 3: modify `EmitGetCollections` to emit `var {camel}Dtos = new[] { normalized.Result };` for root when `!NeedsList`. Leave Pass1/Pass2 unchanged. |
| 2 | Missing Wiring | **Amend Plan** | `EmitGetCollections` needs `rootNode` parameter — not mentioned in plan | DenormalizerEmitter.cs:86-99 | Add step: pass rootNode to EmitGetCollections, use it for conditional root handling |
| 3 | Implicit Assumptions | Accept | `EmitPass1` works correctly with single-element DTO array | DenormalizerEmitter.cs:114 | -- |
| 4 | Incorrect Code | **Amend Plan** | Self-referencing root with `NeedsList = false` breaks denormalization — cross-task issue with Task 5 | Plan line 408-411 | Flag: Task 5's NeedsList computation must include self-references. Task 9 should add test for self-referencing root with `NeedsList = true`. |
| 5 | Implicit Assumptions | **Amend Plan** | Hard dependency on Task 5 (`NeedsList`/`IsRootType` don't exist yet) not stated | TypeGraphNode.cs:5-11 | Add prerequisite statement: "Task 5 must be completed first" |
| 6 | Incorrect Code | **Amend Plan** | Plan says "use `rootDto` instead of `{rootPlural}[0]`" — `rootDto` is a DTO, return type is source object | DenormalizerEmitter.cs:343-348 | Remove instruction to change EmitRootResolution. `return {plural}[0]` is already correct. |
| 7 | Implicit Assumptions | **Amend Plan** | Existing tests will break — not enumerated in plan | DenormalizerEmitterTests.cs:117,172 | List tests needing modification; update CreateNode helper for NeedsList/IsRootType |
| 8 | Incorrect Code | **Amend Plan** (Task 5) | NeedsList computation checks `p.TypeFullName` for Collections — should check `CollectionElementTypeFullName` | Plan line 408-411 | Flag for Task 5: add `p.CollectionElementTypeFullName == rootFqn` check for Collection properties |
| 9 | Insufficient Test Coverage | **Amend Plan** | Missing test for `NeedsList = true` with multiple root-type DTOs in list | -- | Add test: root with `NeedsList = true`, list has multiple DTOs, Pass1/Pass2 use full list |
| 10 | Insufficient Test Coverage | **Ask User** | No null guard for `normalized.Result` in generated denormalizer | -- | Should generated denormalizer add null guard for `Result`? |
| 11 | Insufficient Test Coverage | Accept | Roundtrip test belongs in integration tests, not emitter unit tests | -- | -- |
| 12 | Missing Wiring | **Amend Plan** | Root identification in `EmitGetCollections` not specified | DenormalizerEmitter.cs:86-99 | Specify: pass rootNode to EmitGetCollections, compare TypeFullName for conditional emit |

**No issues found in:** State Issues (denormalization is a pure reconstruction from immutable DTO arrays — no shared mutable state or concurrency concerns)

**Critical items requiring plan amendment:** Findings 1, 2, 4, 5, 6, 7, 8, 12
**Items requiring user decision:** Finding 10
**Cross-task concerns:** Findings 4 and 8 are Task 5 bugs that would cause Task 9 runtime failures
