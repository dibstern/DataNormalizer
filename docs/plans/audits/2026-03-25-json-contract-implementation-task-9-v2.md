# Re-Audit: Task 9 — Update DenormalizerEmitter for Root Property (v2, Post-Amendment)

**Auditor:** Claude (automated)
**Date:** 2026-03-25
**Scope:** Task 9 of the JSON Contract Implementation Plan, re-audited after amendments from v1 findings

**Summary:** The v1 audit identified 12 findings including critical type mismatches and missing wiring. The amended plan correctly resolves ALL critical issues. The change is now properly localized to `EmitGetCollections`, the type analysis is correct (`normalized.Result` is the root DTO type and `new[] { normalized.Result }` produces the correct `T[]`), and `EmitPass1`/`EmitPass2`/`EmitRootResolution` correctly need no changes. Two minor residual issues remain around test coverage specificity and a potential edge case in the `NeedsList=true` code path.

---

## Verification of v1 Amendments

### v1 Finding 1 (Type Mismatch) — RESOLVED

**Original issue:** Plan conflated DTO arrays and source object arrays, writing `var {rootPlural} = new[] { rootDto }` which would be a type mismatch.

**Amendment:** Changed to `var {camel}Dtos = new[] { normalized.Result };` in `EmitGetCollections`. This is correct. Tracing through the code:

- `EmitGetCollections` (line 86-99) produces `{camel}Dtos` variables — these are DTO arrays
- `normalized.Result` will be the root DTO type (e.g., `PersonDto`) after Task 6 adds the `Result` property to the container
- `new[] { normalized.Result }` creates `PersonDto[]` — matching the type that `EmitPass1` (line 114) expects when it reads `{camel}Dtos[i]`
- No type mismatch. ✓

### v1 Finding 2 (Missing `rootNode` parameter) — RESOLVED

**Amendment:** Plan now explicitly says "Add `rootNode` parameter to `EmitGetCollections`". The current call site at line 69 (`EmitGetCollections(sb, allNodes, naming)`) already has `rootNode` available in the enclosing `EmitDenormalizeMethod` (parameter at line 59). Passing it through is straightforward. ✓

### v1 Finding 4 (Self-referencing root edge case) — RESOLVED

**Amendment:** Task 5's plan (Step 3, Step 7) now explicitly includes self-referencing roots in the `NeedsList` computation. The amended plan states "**Self-referencing root type** (`TreeNode` has `TreeNode? Parent`) → root `NeedsList = true` (self-references count)" and the code at lines 569-581 does NOT exclude `n != rootNode`. With `NeedsList=true`, the denormalizer reads the full list from `normalized.TreeNodeDtos`, and all indices are valid. ✓

### v1 Finding 5 (Hard dependency on Task 5) — RESOLVED

**Amendment:** Plan now lists "Prerequisites: Task 5 (IsRootType, NeedsList), Task 6 (container has Result)" at the top of Task 9. ✓

### v1 Finding 6 (`EmitRootResolution` unchanged) — RESOLVED

**Amendment:** Plan now explicitly says "Do NOT change `EmitPass1`, `EmitPass2`, or `EmitRootResolution`" and "The existing `return {plural}[0]` return pattern in root resolution remains correct." This is correct — `EmitRootResolution` at line 347 already returns `{plural}[0]` which is the reconstructed source object at index 0. ✓

### v1 Finding 7 (Existing test breakage) — RESOLVED

**Amendment:** Plan Step 3 says "Same as Task 8: update `CreateNode` calls with `isRootType`/`needsList`. List which existing tests break and need updating." Task 5 Step 4 specifies `needsList: true` as the backward-compatible default, which means existing tests that don't set it will behave as `NeedsList=true` — preserving the current `normalized.{listProp}` behavior. Only tests that explicitly set `needsList: false` will get the new `new[] { normalized.Result }` behavior. ✓

### v1 Finding 8 (Task 5 Collection check bug) — RESOLVED

**Amendment:** Task 5's plan now uses `p.CollectionElementTypeFullName == rootFqn` (line 575 of plan). This is a Task 5 fix, not Task 9, but it's correctly flagged. ✓

### v1 Finding 12 (Root identification in EmitGetCollections) — RESOLVED

**Amendment:** Plan Step 4 specifies adding `rootNode` parameter and comparing to identify the root node within the loop. ✓

---

## New Findings for v2

### Finding 1: Test list lacks specificity on WHICH existing tests need `needsList`/`isRootType` updates

**Category:** Implicit Assumptions
**Action:** Accept
**File:** `tests/DataNormalizer.Generators.Tests/Emitters/DenormalizerEmitterTests.cs`

**Detail:** The plan says "List which existing tests break and need updating" but doesn't enumerate them. With `needsList: true` as the default in `CreateNode` (per Task 5 Step 4), existing tests should NOT break — they'd produce the same `normalized.{listProp}` code as before. However, a few tests may need attention:

- `Emit_NestedNormalizableType_GetsDtoCollections` (line 117): Asserts `normalized.PersonDtos` — still passes with `needsList: true` default ✓
- `Emit_WithCustomName_UsesTypeNameForContainerProperty` (line 172): Asserts `normalized.PersonDtos` — still passes ✓
- `Emit_RootResolution_UsesIndexZero` (line 106): Asserts `persons[0]` — still passes ✓

Actually, no existing test will break because the backward-compatible `needsList: true` default means `EmitGetCollections` continues to emit `var {camel}Dtos = normalized.{listProp};` for all nodes. New tests would need to explicitly set `needsList: false` on root nodes.

**Recommendation:** No change needed. The backward-compatible default handles this correctly. But the plan should add new tests with `needsList: false` to cover the new path — which it does in Step 1.

---

### Finding 2: NeedsList=true path — root node still reads from list, but `Result` property also exists

**Category:** Implicit Assumptions
**Action:** Accept
**File:** `DenormalizerEmitter.cs:86-99`

**Detail:** When `NeedsList=true`, the plan says `var {camel}Dtos = normalized.{rootListProp};`. This reads the full list for pass1/pass2, and `return {plural}[0]` returns the root. But the container also has a `Result` property (set by the normalizer per Task 8). The denormalizer ignores `Result` entirely in this case.

This is correct behavior: `Result` is the same DTO as `{rootListProp}[0]` (Task 8 emits `result.Result = __{rootCamel}Arr[0]`), so reading from the list and returning `{plural}[0]` is functionally identical to reading from `Result`. The denormalizer needs the full list for pass2 reference resolution anyway.

**Recommendation:** No change needed. This is architecturally sound. The denormalizer uses the list when it exists (because pass2 needs indices > 0), and `Result` is for API consumers, not the denormalizer.

---

### Finding 3: Root node identification in `EmitGetCollections` — compare by `TypeFullName` or `IsRootType`?

**Category:** Missing Wiring
**Action:** Amend Plan
**File:** `DenormalizerEmitter.cs:86-99`

**Detail:** The plan says "Add `rootNode` parameter to `EmitGetCollections`" but doesn't specify HOW the root is identified within the loop. There are two options:

1. **Object equality:** `node == rootNode` (reference comparison) — may fail if Task 5 creates new node instances (since `TypeGraphNode` is a `sealed class`, not a record, `with` expressions won't work — Task 5 acknowledges this and says "create a new instance"). If new instances are created, the `rootNode` passed to `EmitDenormalizeMethod` would be the NEW instance, not the one in `allNodes`. But looking at the plan more carefully — Task 5's `rootNode` parameter in `EmitDenormalizeMethod` (line 59) comes from `EmitterHelpers.FindNode(allNodes, rootType.FullyQualifiedName)` (line 36), which searches `allNodes` by `TypeFullName`. So `rootNode` IS the same object reference as the one in `allNodes`. This works.

2. **`node.IsRootType` flag:** Cleaner, doesn't depend on reference equality. But this means `EmitGetCollections` doesn't need the `rootNode` parameter at all.

3. **`TypeFullName` comparison:** `node.TypeFullName == rootNode.TypeFullName`. Safest, no reference equality assumption.

The plan says "pass `rootNode` as parameter" which implies option 1 or 3. Since `EmitterHelpers.FindNode` at line 36 returns a reference FROM `allNodes`, reference equality (option 1) is safe. But the plan should be explicit about the comparison mechanism.

**Recommendation:** The plan should specify: "Compare `node.TypeFullName == rootNode.TypeFullName`" or "use `node.IsRootType`" rather than leaving the comparison implicit. Minor — the implementer will likely get this right regardless.

---

### Finding 4: `EmitGetCollections` still calls `GetListPropertyName` for root even when NeedsList=false

**Category:** Fragile Code
**Action:** Accept
**File:** `DenormalizerEmitter.cs:96`

**Detail:** Currently, `EmitGetCollections` calls `GetListPropertyName(node, allNodes, naming)` for every node (line 96). When the root has `NeedsList=false`, the plan says the root line becomes `var {camel}Dtos = new[] { normalized.Result };` — which doesn't use `listPropertyName` at all. The plan should clarify that `GetListPropertyName` is NOT called for the root when `NeedsList=false` (it's unnecessary and calling it wouldn't cause a bug, just wasted computation).

**Recommendation:** No plan change needed. The implementer will naturally skip `GetListPropertyName` for the NeedsList=false path since it's not needed. No bug risk.

---

### Finding 5: Test for NeedsList=false should assert absence of `normalized.{rootListProp}`

**Category:** Implicit Assumptions
**Action:** Amend Plan
**File:** `tests/DataNormalizer.Generators.Tests/Emitters/DenormalizerEmitterTests.cs`

**Detail:** The plan's Step 1 says "Root with `NeedsList = false` → in `EmitGetCollections`, wrap Result in a single-element DTO array: `var {camel}Dtos = new[] { normalized.Result };`". The test should assert BOTH:
1. The positive: output contains `new[] { normalized.Result }`
2. The negative: output does NOT contain `normalized.{rootListProp}` for root (e.g., `Does.Not.Contain("normalized.PersonDtos")`)

This ensures the root isn't reading from a list property that doesn't exist on the container (Task 6 skips emitting the root list when NeedsList=false).

**Recommendation:** Add negative assertion to the NeedsList=false test: `Assert.That(result, Does.Not.Contain("normalized.PersonDtos"))`.

---

### Finding 6: `TypeGraphNode` is a `sealed class` — Task 5's `with` expression won't compile

**Category:** Incorrect Code (cross-task)
**Action:** Accept (Task 5 concern, not Task 9)
**File:** `src/DataNormalizer.Generators/Models/TypeGraphNode.cs:5`

**Detail:** Task 5 Step 7 suggests `var finalRootNode = rootNode with { IsRootType = true, NeedsList = rootNeedsList };` but `TypeGraphNode` is a `sealed class`, not a `record`. The `with` expression requires a record type. Task 5 does acknowledge this: "OR if TypeGraphNode is a class: create a new instance copying all properties". This is a Task 5 implementation concern. Task 9 just consumes `NeedsList` and `IsRootType`, so it's unaffected as long as the properties exist.

**Recommendation:** Not a Task 9 concern. Flag for Task 5 audit if not already noted.

---

## Summary Table

| # | Category | Action | Issue | File:Line | Amendment / Question |
|---|----------|--------|-------|-----------|----------------------|
| 1 | Implicit Assumptions | Accept | Existing tests won't break due to backward-compatible `needsList: true` default — analysis confirmed | DenormalizerEmitterTests.cs | -- |
| 2 | Implicit Assumptions | Accept | NeedsList=true ignores `Result` property; architecturally sound since list contains same data | DenormalizerEmitter.cs:86-99 | -- |
| 3 | Missing Wiring | **Amend Plan** | Root node comparison mechanism not specified — plan should state `node.TypeFullName == rootNode.TypeFullName` or `node.IsRootType` | DenormalizerEmitter.cs:86-99 | Add: "identify root by comparing `node.TypeFullName == rootNode.TypeFullName`" or "use `node.IsRootType`" |
| 4 | Fragile Code | Accept | `GetListPropertyName` called unnecessarily for root when NeedsList=false — no bug, just unused result | DenormalizerEmitter.cs:96 | -- |
| 5 | Implicit Assumptions | **Amend Plan** | NeedsList=false test should also assert ABSENCE of `normalized.{rootListProp}` | DenormalizerEmitterTests.cs | Add negative assertion: `Does.Not.Contain("normalized.PersonDtos")` to NeedsList=false test |
| 6 | Incorrect Code | Accept | TypeGraphNode is `sealed class`, not record — Task 5's `with` expression needs alternative. Not a Task 9 concern. | TypeGraphNode.cs:5 | -- |

**v1 Findings Status:** All 8 critical findings (1, 2, 4, 5, 6, 7, 8, 12) are correctly resolved in the amended plan.

**Verification of core type analysis:**
- `normalized.Result` is the root DTO type (confirmed via Task 6's ContainerEmitter plan and NormalizerEmitter's `result.Result = __{rootCamel}Arr[0]` in Task 8) ✓
- `new[] { normalized.Result }` produces `T[]` where T is root DTO type ✓
- `EmitPass1` transparently handles 1-element arrays ✓
- `EmitPass2` never indexes beyond bounds when NeedsList=false (guaranteed by Task 5 computation) ✓
- `EmitRootResolution`'s `return {plural}[0]` is correct for both NeedsList scenarios ✓
- Self-referencing root correctly gets NeedsList=true via Task 5's amended computation ✓

**No issues found in:** State Issues, Non-Strict Typing

**Overall assessment:** The amended plan is ready for implementation with two minor amendments (Finding 3: specify comparison mechanism; Finding 5: add negative test assertion).
