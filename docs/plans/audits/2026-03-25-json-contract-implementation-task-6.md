# Audit: Task 6 -- Update ContainerEmitter for root property and collection JSON names

**Auditor:** Claude (automated)
**Date:** 2026-03-25
**Scope:** Task 6 of the JSON Contract Implementation Plan (2026-03-25-json-contract-implementation.md, lines 424-475)

**Summary:** Task 6 changes `ContainerEmitter.Emit` to accept `JsonContractModel`, adds a `Result` property, conditionally emits list properties based on `NeedsList`, and applies collection JSON name overrides. The task has several significant issues: (1) the signature change breaks **17 existing call sites** in tests and 1 in the generator, but Task 6 only modifies the ContainerEmitter tests -- the generator call site is deferred to Task 10 and all existing tests will fail to compile until Task 11; (2) the `Result` property type and default value are underspecified; (3) empty-string `RootPropertyName` is not addressed; (4) the `EmitJsonPropertyNames = false` case is not specified for the `Result` property's `[JsonPropertyName]` attribute; (5) the test list misses several edge cases that could produce incorrect generated code.

---

## Findings

### Finding 1: Signature change breaks all 16 existing test call sites -- no migration step

**Category:** Missing Wiring
**Action:** Amend Plan
**File:** `tests/DataNormalizer.Generators.Tests/Emitters/ContainerEmitterTests.cs` (lines 28, 53, 66, 77, 90, 101, 112, 123, 135, 151, 164, 184, 203, 224, 242, 259)

**Detail:** The plan changes the `ContainerEmitter.Emit` signature from 3 parameters to 4 (adding `JsonContractModel jsonContract`). There are 16 existing test call sites in `ContainerEmitterTests.cs` that use the 3-parameter version:

```csharp
ContainerEmitter.Emit(personNode, allNodes, DefaultNaming)   // 16 occurrences
```

The plan says "Modify: `tests/DataNormalizer.Generators.Tests/Emitters/ContainerEmitterTests.cs`" but only specifies writing **new** failing tests (Step 1). It does not mention updating the 16 existing test calls to pass the new 4th parameter. These tests will fail to compile.

Task 11 is described as "Update all existing tests for root property change" and lists `ContainerEmitterTests.cs`, but Task 6 itself must produce compiling code at the end (Step 7: "Run tests, commit"). The plan should explicitly state that all existing calls get `JsonContractModel.Default` as the 4th argument.

Additionally, the generator call site at `NormalizeGenerator.cs:153` will also break, but Task 10 explicitly handles this -- however this means the project won't compile between Task 6 and Task 10. The plan should note this or provide a temporary overload.

**Recommendation:** Amend Task 6 to add a step: "Update all 16 existing test call sites to pass `JsonContractModel.Default` as the 4th argument. Also, either (a) add a 3-parameter backward-compatible overload that calls the 4-parameter version with `JsonContractModel.Default`, or (b) update `NormalizeGenerator.cs:153` in this task rather than deferring to Task 10." Option (a) is preferred because it keeps Task 6 scoped and avoids a broken build window.

---

### Finding 2: Existing test nodes don't have `IsRootType` or `NeedsList` properties -- test setup is underspecified

**Category:** Implicit Assumptions
**Action:** Amend Plan
**File:** `src/DataNormalizer.Generators/Models/TypeGraphNode.cs:5-11`, `tests/DataNormalizer.Generators.Tests/Emitters/ContainerEmitterTests.cs:267-275`

**Detail:** Task 6 tests rely on `rootNode.NeedsList == false` and `rootNode.NeedsList == true` to control whether the root type's list property is emitted. But `TypeGraphNode` currently has no `NeedsList` or `IsRootType` properties -- these are added by Task 5. Task 6 depends on Task 5 being complete.

The test helper `CreateNode` in `ContainerEmitterTests.cs` (line 267) and `ModelFactories.CreateNode` in `TestUtilities/ModelFactories.cs` (line 8) do not set `NeedsList` or `IsRootType`. Since these are `bool` properties with `init`, they'll default to `false`. The test for "Root type with NeedsList = true" needs to explicitly construct a node with `NeedsList = true`.

**Recommendation:** Amend Task 6 to specify: (1) Task 5 is a prerequisite for Task 6. (2) The test's `CreateNode` helper must be updated to accept optional `isRootType` and `needsList` parameters (or tests should set these inline). (3) For the "NeedsList = false" test case, verify this is the default behavior when `NeedsList` is not set (which it is, since `bool` defaults to `false`).

---

### Finding 3: Result property type and default value are underspecified

**Category:** Implicit Assumptions
**Action:** Amend Plan
**File:** Plan lines 456-459

**Detail:** The plan says:

```csharp
var rootJsonName = jsonContract.RootPropertyName ?? "result";
// [JsonPropertyName("{rootJsonName}")]
// public {RootDtoFullName} Result { get; set; }
```

But `{RootDtoFullName}` is a pseudovariable -- the plan doesn't specify what this resolves to. Looking at the existing code, the DTO full name for the root type would be `EmitterHelpers.GetDtoFullName(rootNode.TypeFullName, rootNode.TypeName, naming)` (e.g., `TestApp.PersonDto`).

Additionally, the plan doesn't specify:
- What the **default value** is. Should it be `default!`? `null`? The existing list properties use `= System.Array.Empty<...>();` which is safe. A reference-type property with `{ get; set; }` and no default will be `null`, which is inconsistent with the `#nullable enable` directive (it would produce a nullable warning).
- Whether the property should be nullable: `public TestApp.PersonDto? Result { get; set; }` vs `public TestApp.PersonDto Result { get; set; } = default!;`

**Recommendation:** Amend Task 6 Step 4 to explicitly state: "The Result property type is the root DTO full name from `EmitterHelpers.GetDtoFullName(rootNode.TypeFullName, rootNode.TypeName, naming)`. Emit with a `default!` initializer to satisfy nullable analysis: `public {dtoFullName} Result { get; set; } = default!;`" Alternatively, ask the user whether `Result` should be nullable.

---

### Finding 4: Empty-string `RootPropertyName` produces `[JsonPropertyName("")]` -- is this intentional?

**Category:** Implicit Assumptions
**Action:** Ask User
**File:** Plan lines 190, 457

**Detail:** Task 3's test list says: "`RootPropertyName` with empty string -> stored as empty." The `??` operator in `jsonContract.RootPropertyName ?? "result"` only substitutes for `null`, not for empty string. So if the user configures `c.RootPropertyName = ""`, the generated code will emit `[JsonPropertyName("")]`.

An empty string JSON property name is technically valid JSON but is almost certainly a user error. Should empty string be treated the same as null (falling back to `"result"`)?

**Recommendation:** Ask user: Should `RootPropertyName = ""` produce `[JsonPropertyName("")]` or fall back to `[JsonPropertyName("result")]`? If the latter, the plan should use `string.IsNullOrEmpty(jsonContract.RootPropertyName) ? "result" : jsonContract.RootPropertyName` instead of `??`.

---

### Finding 5: Result property `[JsonPropertyName]` is always emitted regardless of `EmitJsonPropertyNames`

**Category:** Implicit Assumptions
**Action:** Amend Plan
**File:** Plan lines 456-459, `ContainerEmitter.cs:47-54`

**Detail:** The existing `EmitJsonNamingAttribute` method (line 47-54) checks `naming.EmitJsonPropertyNames` and skips the attribute if it's `false`. The plan's Step 4 says to always emit `[JsonPropertyName("{rootJsonName}")]` on the `Result` property.

But what should happen when `EmitJsonPropertyNames = false`? Options:
1. Always emit `[JsonPropertyName]` on `Result` (because the user explicitly configured a root property name)
2. Only emit when `EmitJsonPropertyNames = true` (consistent with list properties)
3. Only emit when `RootPropertyName` is explicitly set (non-null), regardless of `EmitJsonPropertyNames`

The plan doesn't address this. Currently, for list properties, the JSON attribute is entirely controlled by `EmitJsonPropertyNames`. Having the Result property behave differently would be inconsistent.

**Recommendation:** Amend Task 6 to specify: "Emit `[JsonPropertyName]` on the Result property if either (a) `naming.EmitJsonPropertyNames` is true, OR (b) `jsonContract.RootPropertyName` is explicitly set (non-null). When `EmitJsonPropertyNames` is false and `RootPropertyName` is null, skip the attribute." Add a test for this case: "EmitJsonPropertyNames = false AND no custom RootPropertyName -> no JsonPropertyName attribute on Result."

---

### Finding 6: `CollectionJsonNames` key match depends on exact FQN format from ConfigurationParser

**Category:** Fragile Code
**Action:** Amend Plan
**File:** Plan lines 235, 469

**Detail:** The plan checks `jsonContract.CollectionJsonNames[node.TypeFullName]` where `node.TypeFullName` is the format produced by `TypeGraphAnalyzer` (e.g., `"TestApp.SearchRoute"`). The dictionary keys are set in `ConfigurationParser` via `NormalizeFqn(typeFqn.ToDisplayString(...))`.

If `NormalizeFqn` or `ToDisplayString` produces a different format than `TypeGraphNode.TypeFullName`, the lookup will silently fail and fall back to the default camelCase name. This is a string-matching fragility.

The test plan should include a test that verifies the FQN format matches by using the same format in the test setup as the real code path would produce.

**Recommendation:** Amend Task 6 test list to add: "CollectionJsonNames key uses the exact same FQN format as `TypeGraphNode.TypeFullName`." The test should construct a `CollectionJsonNames` dictionary using the node's `TypeFullName` as the key, confirming the lookup works end-to-end in the emitter.

---

### Finding 7: Missing test -- Result property type matches the root DTO full name

**Category:** Insufficient Test Coverage
**Action:** Amend Plan
**File:** Plan lines 430-438

**Detail:** The planned tests verify:
- `Result` property exists with `[JsonPropertyName("result")]`
- Root list is conditionally emitted

But no test verifies that the `Result` property has the **correct type**. For example, if the root type is `TestApp.Person` with default naming, the Result property should be `public TestApp.PersonDto Result { get; set; }`. A bug in computing the type would be missed.

**Recommendation:** Add test: "Default container Result property has correct DTO type (matches root DTO full name from `EmitterHelpers.GetDtoFullName`)."

---

### Finding 8: Missing test -- EmitJsonPropertyNames = false with collection JSON name override

**Category:** Insufficient Test Coverage
**Action:** Amend Plan
**File:** Plan lines 437-438

**Detail:** The plan tests:
- Collection with `CollectionJsonNames` override -> custom `[JsonPropertyName]`
- No override -> default camelCase

But it doesn't test: `EmitJsonPropertyNames = false` combined with a `CollectionJsonNames` override. Should the override still emit the attribute even when the global setting says not to? This parallels Finding 5 for the Result property.

**Recommendation:** Add test: "EmitJsonPropertyNames = false with CollectionJsonNames override -> [JsonPropertyName] still emitted for overridden types, no attribute for non-overridden types." This ensures explicit overrides always apply regardless of the global setting.

---

### Finding 9: Missing test -- Container with root NeedsList = false has NO root type list property at all

**Category:** Insufficient Test Coverage
**Action:** Amend Plan
**File:** Plan line 434

**Detail:** The plan says: "Root type does NOT appear in a list property (root NeedsList = false)." But the test should verify with `Does.Not.Contain` on the CLR property name for the root list. The plan needs to be explicit that no array property of the root DTO type exists in the output -- not just that it's "not in a list property" (which is vague).

For example, with root `Person` and `NeedsList = false`:
- `Does.Not.Contain("PersonDtos")` 
- `Does.Contain("Result")` (the Result property)
- Still contains list properties for non-root types

The current plan test description is ambiguous about what "does NOT appear in a list property" means concretely.

**Recommendation:** Amend the test description to be explicit: "Root type with NeedsList = false -> output Does.Not.Contain the root type's list property name (e.g., 'PersonDtos'), but DOES contain 'Result' and all non-root list properties."

---

### Finding 10: No backward-compatible overload -- project won't compile between Tasks 6 and 10

**Category:** Missing Wiring
**Action:** Amend Plan
**File:** `NormalizeGenerator.cs:153`

**Detail:** The plan changes `ContainerEmitter.Emit` from 3 to 4 parameters in Task 6 but doesn't update the call site in `NormalizeGenerator.cs:153` until Task 10. Between Tasks 6 and 10, the project will not compile. This violates the TDD principle of "green after each task."

The commit message for Task 6 is "feat: update ContainerEmitter with root property and collection JSON name overrides" -- this implies a working commit, but the project cannot build.

**Recommendation:** Either: (a) Add a backward-compatible 3-parameter overload in Task 6 that forwards to the 4-parameter version with `JsonContractModel.Default`, OR (b) Move the `NormalizeGenerator.cs` call site update from Task 10 to Task 6. Option (a) is cleaner -- the overload can be removed in Task 10.

---

### Finding 11: Existing tests assert root type IS in a list -- these will break

**Category:** Missing Wiring
**Action:** Amend Plan
**File:** `ContainerEmitterTests.cs:31-43`, `ContainerEmitterTests.cs:86-93`

**Detail:** Several existing tests assert that the root type appears in the container's list properties:

- Line 35: `Does.Contain("public TestApp.PersonDto[] PersonDtos")` 
- Line 92: `Does.Contain("public TestApp.PersonDto[] PersonDtos { get; set; }")`

After Task 6, if the default behavior for root nodes is `NeedsList = false` (since `bool` defaults to `false`), these assertions will FAIL because the root type's list property will be suppressed.

This means Task 6 cannot simply add the new parameter and new behavior -- it must also update existing tests to either:
1. Set `NeedsList = true` on root nodes for backward-compatible test behavior, OR
2. Remove/update the assertions about root list properties

Task 11 is supposed to handle this, but Task 6's own "Run tests, commit" step (Step 7) requires all tests to pass.

**Recommendation:** Amend Task 6 to explicitly state: "When passing `JsonContractModel.Default` to existing tests, root nodes have `NeedsList = false` by default. Update existing tests that assert root type list properties: either set `NeedsList = true` on the root node, or remove the root list assertion and add a Result property assertion instead." This is a significant scope increase for Task 6 that should be acknowledged.

---

### Finding 12: Plan doesn't specify whether Result property uses `{ get; set; }` or `{ get; init; }`

**Category:** Implicit Assumptions
**Action:** Accept

**File:** Plan lines 456-459

**Detail:** The existing list properties use `{ get; set; }`. The plan shows `{ get; set; }` in the pseudocode. Since the NormalizerEmitter sets `result.Result = ...` (Task 8), it must be `{ get; set; }`. This is consistent and correct.

**Recommendation:** None needed. The pattern is implicit but correct.

---

## Summary Table

| # | Category | Action | Issue | File:Line | Amendment / Question |
|---|----------|--------|-------|-----------|----------------------|
| 1 | Missing Wiring | **Amend Plan** | 16 existing test call sites break; no migration step specified | ContainerEmitterTests.cs:28+ | Add step: pass `JsonContractModel.Default` to all existing calls, or add backward-compatible 3-param overload |
| 2 | Implicit Assumptions | **Amend Plan** | Test nodes lack `IsRootType`/`NeedsList` -- Task 5 dependency + test helper updates not specified | TypeGraphNode.cs:5-11, ContainerEmitterTests.cs:267 | Specify Task 5 prerequisite and updated CreateNode helper for NeedsList |
| 3 | Implicit Assumptions | **Amend Plan** | Result property DTO type and default value (`default!` vs nullable) not specified | Plan:456-459 | Specify: use `GetDtoFullName` for type, `= default!` for initializer |
| 4 | Implicit Assumptions | **Ask User** | `RootPropertyName = ""` produces `[JsonPropertyName("")]` -- bug or feature? | Plan:457 | Should empty string fall back to `"result"` or emit empty? |
| 5 | Implicit Assumptions | **Amend Plan** | Result `[JsonPropertyName]` emission when `EmitJsonPropertyNames = false` is unspecified | Plan:456-459, ContainerEmitter.cs:47-54 | Specify behavior and add test for EmitJsonPropertyNames=false with/without explicit RootPropertyName |
| 6 | Fragile Code | **Amend Plan** | CollectionJsonNames key must exactly match TypeGraphNode.TypeFullName format | Plan:469 | Add test verifying FQN format consistency between dictionary key and node.TypeFullName |
| 7 | Insufficient Test Coverage | **Amend Plan** | No test verifies Result property has correct DTO type | Plan:430-438 | Add test: Result property type matches root DTO full name |
| 8 | Insufficient Test Coverage | **Amend Plan** | No test for `EmitJsonPropertyNames = false` + collection override interaction | Plan:437-438 | Add test for explicit override with global JSON names disabled |
| 9 | Insufficient Test Coverage | **Amend Plan** | "Root not in list property" test description is vague | Plan:434 | Make explicit: `Does.Not.Contain("PersonDtos")` + still has non-root lists |
| 10 | Missing Wiring | **Amend Plan** | Project won't compile between Tasks 6 and 10 (NormalizeGenerator.cs:153) | NormalizeGenerator.cs:153 | Add backward-compatible 3-param overload or move generator update to Task 6 |
| 11 | Missing Wiring | **Amend Plan** | Existing tests assert root IS in list -- will fail when NeedsList defaults to false | ContainerEmitterTests.cs:35,92 | Update existing test assertions or set NeedsList=true on root nodes in existing tests |
| 12 | Implicit Assumptions | Accept | Result property getter/setter style not specified (but `{ get; set; }` is implied and correct) | Plan:456-459 | -- |

**No issues found in:** State Issues, Non-Strict Typing

**Critical items requiring plan amendment:** Findings 1, 2, 3, 5, 10, 11 (these will cause compilation failures or test failures if not addressed)
