# Audit: Task 2 — Runtime Configuration Classes

## Summary

Task 2 creates five new/modified files (`JsonContractBuilder`, `ReferenceBuilder`, `NormalizeJsonNameAttribute`, and modifications to `GraphBuilder` and `TypeBuilder`) but specifies **zero tests**. The plan says "Run tests, commit" without listing any test file. While the existing codebase has a clear pattern for testing these syntactic marker classes (`tests/DataNormalizer.Tests/Configuration/ConfigurationTests.cs` and `tests/DataNormalizer.Tests/Attributes/AttributeTests.cs`), the plan completely omits the corresponding tests for the new classes. Additionally, there is a critical behavioral inconsistency in `UseJsonContract` versus the established `UseNaming` pattern.

---

## Findings

| # | Category | Action | Issue | File:Line | Amendment / Question |
|---|----------|--------|-------|-----------|----------------------|
| 1 | Insufficient Test Coverage | **Amend Plan** | Zero tests specified for 3 new classes + 2 modifications | N/A (no test file listed) | Add test file `tests/DataNormalizer.Tests/Configuration/JsonContractBuilderTests.cs` (or extend `ConfigurationTests.cs`) and `tests/DataNormalizer.Tests/Attributes/AttributeTests.cs`. See Finding 1 detail. |
| 2 | Incorrect Code | **Amend Plan** | `UseJsonContract` creates and discards a `JsonContractBuilder`, breaking fluent chaining context | Plan Step 4 snippet | `UseJsonContract` should follow the `UseNaming` pattern exactly. The method signature and body are correct, but the plan should document that the `Action<JsonContractBuilder>` lambda executes and its result is discarded. This IS the established pattern. See Finding 2 detail. |
| 3 | Implicit Assumptions | **Accept** | All builder classes are syntactic markers with no runtime state storage — correct per codebase pattern | `src/DataNormalizer/Configuration/GraphBuilder.cs:56-60`, `TypeBuilder.cs:17` | The plan's no-state approach matches the established pattern. The source generator reads these via syntax analysis (ConfigurationParser.cs), not runtime execution. |
| 4 | Missing Wiring | **Amend Plan** | `TypeBuilder.Reference` returns `ReferenceBuilder` (new type), but `TypeBuilder.cs` has `using System.Linq.Expressions;` — needs to import `ReferenceBuilder` (same namespace, OK). However, `ReferenceBuilder` is a non-generic `new()` return, unlike all existing `TypeBuilder` methods which return `this`. This breaks the fluent chaining pattern. | Plan Step 5, `TypeBuilder.cs:1-11` | See Finding 4 detail. |
| 5 | Insufficient Test Coverage | **Amend Plan** | `NormalizeJsonNameAttribute` needs tests in `AttributeTests.cs` matching the existing pattern | `tests/DataNormalizer.Tests/Attributes/AttributeTests.cs` | Add tests for: `TargetsProperty`, `IsNotInherited`, `DoesNotAllowMultiple`, `IsSealed`, `Name_PropertyReturnsConstructorValue`. See Finding 5 detail. |
| 6 | Implicit Assumptions | **Amend Plan** | `ReferenceBuilder.JsonName()` returns `this` — but `Reference()` returns `new()`, so chaining `Reference(p => p.X).JsonName("x")` creates a fresh builder each time (no `this` continuity). This is fine for syntax analysis, but tests should verify the fluent API actually compiles and chains. | Plan Step 2 + Step 5 | See Finding 6 detail. |
| 7 | Missing Wiring | **Accept** | `JsonContractBuilder.Collection<T>(string)` has an empty body (no return, void method). This is consistent with the syntactic marker pattern but is the only `void` method on a builder — all others return `this` or a new builder. | Plan Step 1 snippet | This is intentional per the pattern. The ConfigurationParser in Task 3 will read this as a method invocation in the syntax tree. No action needed. |

---

## Finding 1: Zero Tests for New Classes (CRITICAL)

**Category:** Insufficient Test Coverage  
**Action:** Amend Plan

The existing codebase has a clear, thorough testing pattern:

1. **`tests/DataNormalizer.Tests/Attributes/AttributeTests.cs`** tests every attribute for:
   - `AttributeTargets` correctness
   - `Inherited = false`
   - `AllowMultiple = false`
   - `IsSealed`

2. **`tests/DataNormalizer.Tests/Configuration/ConfigurationTests.cs`** tests every builder for:
   - Return type correctness (e.g., `Is.InstanceOf<GraphBuilder<T>>()`)
   - Fluent chaining returns self (`Is.SameAs(graphBuilder)`)
   - Lambda execution (captures `lambdaCalled = true`)
   - Full chain scenarios

Task 2 creates `JsonContractBuilder`, `ReferenceBuilder`, `NormalizeJsonNameAttribute`, and adds `UseJsonContract` to `GraphBuilder` and `Reference`/`ReferenceCollection` to `TypeBuilder` — but lists no tests.

**Required tests to add:**

In `tests/DataNormalizer.Tests/Configuration/ConfigurationTests.cs`:
- `JsonContractBuilder_RootPropertyName_CanSetAndGet` — verifies the property setter/getter
- `JsonContractBuilder_Collection_DoesNotThrow` — calling `Collection<T>("name")` doesn't throw
- `ReferenceBuilder_JsonName_ReturnsSelf` — `Is.SameAs` pattern
- `GraphBuilder_UseJsonContract_ReturnsSelf` — `Is.SameAs(graphBuilder)` pattern
- `GraphBuilder_UseJsonContract_ExecutesAction` — `lambdaCalled = true` pattern
- `TypeBuilder_Reference_ReturnsReferenceBuilder` — `Is.InstanceOf<ReferenceBuilder>()`
- `TypeBuilder_ReferenceCollection_ReturnsReferenceBuilder` — `Is.InstanceOf<ReferenceBuilder>()`
- `TypeBuilder_Reference_JsonName_Chain_Compiles` — verifies `x.Reference(p => p.Name).JsonName("name")` works

In `tests/DataNormalizer.Tests/Attributes/AttributeTests.cs`:
- `NormalizeJsonNameAttribute_TargetsProperty`
- `NormalizeJsonNameAttribute_IsNotInherited`
- `NormalizeJsonNameAttribute_DoesNotAllowMultiple`
- `NormalizeJsonNameAttribute_IsSealed`
- `NormalizeJsonNameAttribute_Name_ReturnsConstructorValue`

---

## Finding 2: UseJsonContract Follows Established Discard Pattern (Non-Issue on Review)

**Category:** Incorrect Code  
**Action:** Accept

On closer inspection, `UseJsonContract` follows the exact same pattern as `UseNaming` (GraphBuilder.cs:56-60) and `ForType` (GraphBuilder.cs:44-49): create a new builder, pass it to the lambda, discard it, return `this`. This is the established pattern for syntactic marker builders. The source generator's ConfigurationParser reads the lambda body via syntax analysis, not runtime execution.

The plan's code is correct. No issue.

---

## Finding 4: Reference/ReferenceCollection Return Type Breaks Fluent Chain

**Category:** Missing Wiring  
**Action:** Amend Plan

All existing `TypeBuilder<T>` methods return `TypeBuilder<T>` (i.e., `this`) to enable fluent chaining:

```csharp
// Current pattern (TypeBuilder.cs:17-46):
public TypeBuilder<T> IgnoreProperty(Expression<Func<T, object?>> selector) => this;
public TypeBuilder<T> NormalizeProperty(Expression<Func<T, object?>> selector) => this;
public TypeBuilder<T> InlineProperty(Expression<Func<T, object?>> selector) => this;
public TypeBuilder<T> IncludeProperty(Expression<Func<T, object?>> selector) => this;
public TypeBuilder<T> WithName(string name) => this;
public TypeBuilder<T> UsePropertyMode(PropertyMode mode) => this;
```

The plan adds:
```csharp
public ReferenceBuilder Reference(Expression<Func<T, object?>> selector) => new();
public ReferenceBuilder ReferenceCollection(Expression<Func<T, object?>> selector) => new();
```

This returns a **different type** (`ReferenceBuilder` instead of `TypeBuilder<T>`), which means calling `Reference()` terminates the `TypeBuilder` fluent chain. After `.Reference(p => p.Line).JsonName("line")`, you cannot chain more `TypeBuilder` methods like `.IgnoreProperty(...)`.

This is **intentional by design** (the user usage in Task 12 shows separate statements for each `Reference` call), but the plan should:

1. Explicitly document this chain-breaking behavior
2. Ensure the ConfigurationParser (Task 4) handles the transition from `TypeBuilder` -> `ReferenceBuilder` receiver kind (which Task 4 does address)
3. Add a test that verifies multiple `Reference()` calls require separate statements or separate `ForType` invocations

---

## Finding 5: Missing NormalizeJsonNameAttribute Tests

**Category:** Insufficient Test Coverage  
**Action:** Amend Plan

`NormalizeJsonNameAttribute` is the first attribute with a constructor parameter (`string name`) and a `Name` property. The existing attributes (`NormalizeConfigurationAttribute`, `NormalizeIgnoreAttribute`, `NormalizeIncludeAttribute`) are all parameterless. The plan must add a test for the constructor/property behavior:

```csharp
[Test]
public void NormalizeJsonNameAttribute_Name_ReturnsConstructorValue()
{
    var attr = new NormalizeJsonNameAttribute("myName");
    Assert.That(attr.Name, Is.EqualTo("myName"));
}
```

Additionally, the standard attribute tests (`TargetsProperty`, `IsNotInherited`, `DoesNotAllowMultiple`, `IsSealed`) must be added.

---

## Finding 6: ReferenceBuilder Fluent API Returns Fresh Instance (Not `this`)

**Category:** Implicit Assumptions  
**Action:** Accept

The plan has `ReferenceBuilder.JsonName()` returning `this`, which is correct. But `TypeBuilder.Reference()` returns `new ReferenceBuilder()` (not a shared instance). In the chained syntax `x.Reference(p => p.Line).JsonName("line")`:

1. `Reference(...)` creates a fresh `ReferenceBuilder`
2. `.JsonName("line")` is called on that fresh instance, which returns `this` (same fresh instance)
3. The return value is discarded

This is fine for the syntactic marker pattern because the ConfigurationParser reads the method calls from the AST, not from runtime object state. But it means `ReferenceBuilder.JsonName` returning `this` is cosmetically correct but functionally irrelevant — the builder is discarded immediately. Tests should verify the fluent chain compiles and doesn't throw, not that `this` is preserved.

---

## No Issues Found In

- **State Issues**: These are all stateless syntactic marker classes. No shared state, no race conditions, no caches.
- **Non-Strict Typing**: All types are concrete and correctly constrained. `Expression<Func<T, object?>>` matches the existing pattern exactly. No `any` or loose generics.
- **Fragile Code**: No hardcoded values beyond the established pattern. No string matching or format assumptions.

---

## Summary of Required Plan Amendments

1. **Add a test file step** to Task 2 specifying `tests/DataNormalizer.Tests/Configuration/ConfigurationTests.cs` modifications (8 new tests) and `tests/DataNormalizer.Tests/Attributes/AttributeTests.cs` modifications (5 new tests).

2. **Document the chain-breaking behavior** of `Reference()`/`ReferenceCollection()` — these intentionally return `ReferenceBuilder` instead of `TypeBuilder<T>`, ending the TypeBuilder fluent chain.

3. **Test the constructor parameter** on `NormalizeJsonNameAttribute` — this is the first attribute with a constructor argument, and it deserves explicit coverage that `Name` returns the provided value (including edge cases like empty string and null).
