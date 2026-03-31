# Audit: Task 1 — JsonContractModel and JsonNameOverride

**Auditor:** Claude (automated)
**Date:** 2026-03-25
**Scope:** Task 1 of the JSON Contract Implementation Plan

**Summary:** Task 1 introduces `JsonContractModel` (with custom equality/hashing), adds new properties to `NormalizationModel` and `AnalyzedProperty`, and tests only `JsonContractModel` equality. The main issues are: (1) a **real bug** in `GetHashCode` — iteration order of `ImmutableDictionary` is not guaranteed to be deterministic across structurally-equal instances, making the hash order-dependent; (2) `NormalizationModel` does NOT implement `IEquatable` or any custom equality, but the incremental generator pipeline currently transforms everything in `TransformConfig` and outputs `GeneratorOutput` (a `record struct` containing `ImmutableArray<GeneratorSourceEntry>` and `ImmutableArray<GeneratorDiagnosticInfo>` which are themselves `record struct`s with string members), so equality is compared at the `GeneratorOutput` level, not `NormalizationModel` level — this means adding properties to `NormalizationModel` without equality has **no impact on caching** today, but it could become a problem if the pipeline ever restructures; (3) the planned test list has significant gaps for a "100% bug-proof TDD" claim.

---

## Findings

### Finding 1: GetHashCode is order-dependent for ImmutableDictionary entries

**Category:** Incorrect Code
**Action:** Amend Plan
**File:** Plan lines 60-69 (JsonContractModel.GetHashCode snippet)

**Detail:** The planned `GetHashCode` implementation iterates over `CollectionJsonNames` (an `ImmutableDictionary`) and XOR-multiplies the hash with each key and value sequentially:

```csharp
foreach (var kvp in CollectionJsonNames)
{
    hash = (hash * 397) ^ kvp.Key.GetHashCode();
    hash = (hash * 397) ^ kvp.Value.GetHashCode();
}
```

The `(hash * 397)` multiply-then-XOR pattern is **order-sensitive** — swapping the iteration order of two entries produces different hash codes. While `ImmutableDictionary` currently uses a sorted/deterministic internal order for equal dictionaries (based on the hash tree structure), this is an **implementation detail**, not a contract. Furthermore, even if it happens to work today, it makes the hash computation **fragile** — a reviewer or future developer cannot reason about correctness without knowing ImmutableDictionary internals.

The `Equals` method is correct because it uses key-value lookup, which is order-independent.

**Recommendation:** Amend the plan to use an order-independent hash combination for dictionary entries. The idiomatic approach is to XOR the per-entry hashes (without multiply accumulation), because XOR is commutative and associative:

```csharp
public override int GetHashCode()
{
    var hash = RootPropertyName?.GetHashCode() ?? 0;
    foreach (var kvp in CollectionJsonNames)
    {
        hash ^= kvp.Key.GetHashCode() ^ (kvp.Value.GetHashCode() * 397);
    }
    return hash;
}
```

Or use `HashCode.Combine` for each entry and XOR the results. Alternatively, sort keys before hashing (since they're strings), but XOR is simpler and sufficient.

---

### Finding 2: NormalizationModel does not implement IEquatable — new properties won't participate in equality

**Category:** Implicit Assumptions
**Action:** Accept

**File:** `src/DataNormalizer.Generators/Models/NormalizationModel.cs:6`

**Detail:** `NormalizationModel` is a plain `class` with no `IEquatable<NormalizationModel>`, no custom `Equals`, and no custom `GetHashCode`. The plan adds `JsonContract` and `PropertyJsonNameOverrides` to it but doesn't add equality support.

Examining the generator pipeline in `NormalizeGenerator.cs`, `NormalizationModel` is consumed entirely within `TransformConfig` (lines 56-96) and is never passed through the incremental pipeline. The incremental caching operates on `GeneratorOutput` (a `record struct`), which contains only string-based source entries and diagnostics. So `NormalizationModel` equality is **not currently needed** for incremental caching correctness.

However, `NamingModel` (which is a property of `NormalizationModel`) *does* implement `IEquatable<NamingModel>`. This is either (a) leftover from a design that planned to cache at the model level, or (b) future-proofing. Either way, the inconsistency is notable but not a bug today.

**Recommendation:** No plan change needed for Task 1. But if the pipeline ever adds a `.WithComparer()` or model-level caching, `NormalizationModel` will need full equality support including the new `JsonContract` and `PropertyJsonNameOverrides` fields.

---

### Finding 3: Missing test — null RootPropertyName in equality (both null vs both null)

**Category:** Insufficient Test Coverage
**Action:** Amend Plan

**Detail:** The planned tests cover:
- Two identical instances (presumably with non-null `RootPropertyName`)
- Different `RootPropertyName`
- Default equality

But there is no explicit test for:
- Two instances where `RootPropertyName` is `null` on both → `Equals` should be true
- One instance with `RootPropertyName = null` and the other with `RootPropertyName = "result"` → `Equals` should be false

The `Default` test partially covers the both-null case (since `Default` has `RootPropertyName = null`), but the plan should be explicit about testing nullability.

**Recommendation:** Add test: "Both `RootPropertyName` null → Equals true" and "One null, one non-null `RootPropertyName` → Equals false" to the test list.

---

### Finding 4: Missing test — GetHashCode consistency (equal objects must have equal hashes)

**Category:** Insufficient Test Coverage
**Action:** Amend Plan

**Detail:** The plan lists "Two identical instances → Equals true, GetHashCode matches" but does not explicitly test this for:
- Instances with non-empty `CollectionJsonNames` (the dictionary hashing path)
- The `Default` case (both empty dictionaries, both null RootPropertyName)

The first test likely covers a simple case, but the hash consistency for dictionary content is critical because of Finding 1 (order-dependent hashing). A test with multiple dictionary entries where iteration order could vary would catch the order-dependence bug.

**Recommendation:** Add test: "Two instances with identical multi-entry CollectionJsonNames → GetHashCode matches." Ideally construct the dictionaries in different insertion orders to stress iteration order.

---

### Finding 5: Missing test — GetHashCode order-independence for dictionary entries

**Category:** Insufficient Test Coverage
**Action:** Amend Plan

**Detail:** There is no test that constructs two `JsonContractModel` instances with the same key-value pairs added in different orders and verifies `GetHashCode` equality. This is the direct test for the bug identified in Finding 1.

**Recommendation:** Add test: "Two instances with same dictionary entries added in different order → Equals true AND GetHashCode matches."

Note: `ImmutableDictionary` doesn't guarantee different internal ordering based on insertion order (it's a hash tree), so this test might pass even with the buggy code. The fix from Finding 1 (order-independent hashing) is the real solution; this test is defense-in-depth.

---

### Finding 6: Missing tests — NormalizationModel and AnalyzedProperty equality with new fields

**Category:** Insufficient Test Coverage
**Action:** Accept (but note for downstream tasks)

**Detail:** The plan adds `JsonContract` to `NormalizationModel` and `JsonNameOverride` to `AnalyzedProperty`, but neither class implements `IEquatable` or custom equality. Since there's no equality to test, there's nothing to test here in Task 1.

However, the user asked for "100% confidence that TDD covers ALL possible sources of bugs." If a future task adds equality to these types, the new fields must be included. The plan should note this as a dependency.

**Recommendation:** No change for Task 1. This is a downstream concern (potentially for Tasks 5-10 where these values are consumed).

---

### Finding 7: ModelFactories.CreateNode will need updating for new TypeGraphNode fields

**Category:** Implicit Assumptions
**Action:** Accept (downstream task concern)

**File:** `tests/DataNormalizer.Generators.Tests/TestUtilities/ModelFactories.cs:8-32`

**Detail:** Task 5 will add `IsRootType` and `NeedsList` to `TypeGraphNode`. The existing `ModelFactories.CreateNode` methods don't set these (they'll default to `false`). This is fine for Task 1 (which doesn't add these fields), but Task 5 will need to update `ModelFactories` or the emitter tests will construct nodes without proper root/needsList flags.

**Recommendation:** No change for Task 1. Note for Task 5 audit.

---

### Finding 8: TypeGraphAnalyzer.Analyze signature doesn't accept NormalizationModel — PropertyJsonNameOverrides won't flow

**Category:** Implicit Assumptions
**Action:** Accept (Task 5 concern)

**File:** `src/DataNormalizer.Generators/Analysis/TypeGraphAnalyzer.cs:10-17`

**Detail:** Task 5 plans to read `PropertyJsonNameOverrides` inside `TypeGraphAnalyzer` when building `AnalyzedProperty.JsonNameOverride`. But the current `Analyze` method signature takes individual parameters (`inlinedTypes`, `explicitTypes`, etc.) — not the full `NormalizationModel`. Task 5 will need to either:
- Add `PropertyJsonNameOverrides` as a new parameter to `Analyze`, or
- Refactor to accept the full `NormalizationModel`

This impacts the `NormalizeGenerator.cs:78-85` call site. Task 1 just adds the property to the model, so this is fine for Task 1.

**Recommendation:** No change for Task 1. Task 5 should explicitly address the Analyze method signature change.

---

### Finding 9: ImmutableDictionary<string, string> typing for CollectionJsonNames and PropertyJsonNameOverrides

**Category:** Non-Strict Typing
**Action:** Accept

**Detail:** Both `CollectionJsonNames` and `PropertyJsonNameOverrides` use `ImmutableDictionary<string, string>`. The keys are fully-qualified type names (for `CollectionJsonNames`) or `"TypeFqn.PropertyName"` dot-joined strings (for `PropertyJsonNameOverrides`). These are stringly-typed — a typo in a key won't be caught at compile time and will silently fall through to default behavior.

However, this is consistent with how the existing codebase handles type FQNs (e.g., `TypeConfigurations` uses `ImmutableDictionary<string, TypeConfiguration>` with string FQN keys). Introducing a wrapper type would be over-engineering for this context.

**Recommendation:** No change. This is an accepted pattern in the codebase.

---

### Finding 10: Plan doesn't specify `using System.Collections.Immutable` for JsonContractModel

**Category:** Incorrect Code
**Action:** Accept

**Detail:** The code snippet in the plan includes `using System.Collections.Immutable;` at the top (line 34 of the plan). This is correct. No issue.

**Recommendation:** None.

---

## Summary Table

| # | Category | Action | Issue | File:Line | Amendment / Question |
|---|----------|--------|-------|-----------|----------------------|
| 1 | Incorrect Code | **Amend Plan** | GetHashCode is order-dependent for ImmutableDictionary iteration | Plan:60-69 | Use XOR-based order-independent hash for dictionary entries |
| 2 | Implicit Assumptions | Accept | NormalizationModel has no equality; not needed for current pipeline | NormalizationModel.cs:6 | -- |
| 3 | Insufficient Test Coverage | **Amend Plan** | No explicit test for null RootPropertyName equality edge cases | Plan:92-98 | Add tests for both-null and one-null-one-nonnull RootPropertyName |
| 4 | Insufficient Test Coverage | **Amend Plan** | No test for GetHashCode consistency with non-empty dictionaries | Plan:92-98 | Add test: identical multi-entry dict instances have matching hashes |
| 5 | Insufficient Test Coverage | **Amend Plan** | No test for GetHashCode order-independence of dictionary entries | Plan:92-98 | Add test: same entries, different construction order, equal hashes |
| 6 | Insufficient Test Coverage | Accept | No equality tests for NormalizationModel/AnalyzedProperty (they have no equality impl) | Multiple | Downstream concern for Tasks 5+ |
| 7 | Implicit Assumptions | Accept | ModelFactories.CreateNode will need updating for TypeGraphNode new fields | ModelFactories.cs:8-32 | Task 5 concern |
| 8 | Implicit Assumptions | Accept | TypeGraphAnalyzer.Analyze signature won't accept PropertyJsonNameOverrides | TypeGraphAnalyzer.cs:10-17 | Task 5 concern |
| 9 | Non-Strict Typing | Accept | String-typed dictionary keys are consistent with codebase pattern | -- | -- |

**No issues found in:** (all categories had at least one note)

**Critical items requiring plan amendment:** Findings 1, 3, 4, 5
