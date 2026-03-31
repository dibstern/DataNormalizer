# Audit: Task 3 — ConfigurationParser UseJsonContract

## Summary

Task 3 extends `ConfigurationParser` to handle `UseJsonContract` lambda bodies. The plan's overall approach is sound and follows established patterns, but has **one critical code error** (the `ProcessAssignment` guard blocks the new receiver kind), **one incomplete code snippet**, **one missing helper function**, and **several test coverage gaps** that need addressing before implementation.

## Findings

### Finding 1: ProcessAssignment guard blocks JsonContractBuilder receiver kind

**Category:** Incorrect Code
**Action:** Amend Plan
**Detail:** The plan's Step 7 says to "extend" `ProcessAssignment` with a new `case ReceiverKind.JsonContractBuilder:`. However, the existing `ProcessAssignment` at `ConfigurationParser.cs:425` has a hard guard:

```csharp
if (receiverKind != ReceiverKind.NamingBuilder)
    return;
```

This early-return means execution will never reach any new `case` for `JsonContractBuilder`. The plan needs to explicitly state that this guard must be restructured — either changed to a switch on `receiverKind` that dispatches to different handler blocks, or changed to `if (receiverKind is not (ReceiverKind.NamingBuilder or ReceiverKind.JsonContractBuilder)) return;`.

**Recommendation:** Amend Step 7 to explicitly state: "Replace the `if (receiverKind != ReceiverKind.NamingBuilder) return;` guard at line 425 with a switch/dispatch that handles both `NamingBuilder` and `JsonContractBuilder`, falling through to return for any other kind." Show the restructured method skeleton.

---

### Finding 2: Plan code snippet has literal ellipsis for SymbolDisplayFormat

**Category:** Incorrect Code
**Action:** Amend Plan
**Detail:** Step 6's code snippet for the `Collection` handler contains:

```csharp
context.CollectionJsonNames[NormalizeFqn(typeFqn.ToDisplayString(...))] = jsonName;
```

The `(...)` is a literal ellipsis in the plan text, not valid C#. The correct call, based on the established pattern at `ConfigurationParser.cs:205,225,257`, should be `SymbolDisplayFormat.FullyQualifiedFormat`. An implementer following the plan literally would get a compilation error.

**Recommendation:** Replace `(...)` with `(SymbolDisplayFormat.FullyQualifiedFormat)` in the Step 6 code snippet.

---

### Finding 3: ExtractStringArgument does not exist and is not defined by the plan

**Category:** Missing Wiring
**Action:** Amend Plan
**Detail:** Step 6 calls `ExtractStringArgument(invocation)`, but this function does not exist anywhere in the codebase (verified by grep). The plan does not provide its implementation. The existing `WithName` handler at `ConfigurationParser.cs:336-343` does string argument extraction inline (checking `Arguments[0].Expression is LiteralExpressionSyntax`). The plan needs to either:
- Define `ExtractStringArgument` as a new helper (preferred for reuse in Task 4's `JsonName` handler)
- Or specify to inline the extraction logic

**Recommendation:** Add a step between Steps 5 and 6: "Create `ExtractStringArgument` helper method" with implementation:

```csharp
private static string? ExtractStringArgument(InvocationExpressionSyntax invocation, int argIndex = 0)
{
    if (invocation.ArgumentList.Arguments.Count <= argIndex)
        return null;
    var argExpr = invocation.ArgumentList.Arguments[argIndex].Expression;
    return argExpr is LiteralExpressionSyntax literal ? literal.Token.ValueText : null;
}
```

This is also needed by Task 4 (`JsonName` handler), so creating it here prevents duplication.

---

### Finding 4: Task 1 and Task 2 are prerequisites not explicitly verified

**Category:** Implicit Assumptions
**Action:** Accept
**Detail:** Task 3 depends on:
- `JsonContractModel` class (created by Task 1) — does not yet exist
- `JsonContract` property on `NormalizationModel` (added by Task 1) — does not yet exist
- `UseJsonContract` method on `GraphBuilder<T>` (created by Task 2) — does not yet exist
- `JsonContractBuilder` class (created by Task 2) — does not yet exist

The plan correctly orders tasks sequentially (Task 1 -> Task 2 -> Task 3). The test helper includes the runtime assembly which will contain Task 2's classes. No action needed, but the implementer should verify Tasks 1-2 are complete before starting.

**Recommendation:** Informational only. No change needed.

---

### Finding 5: Missing test — empty UseJsonContract lambda body

**Category:** Insufficient Test Coverage
**Action:** Amend Plan
**Detail:** The plan tests "No UseJsonContract" but not "UseJsonContract with empty lambda body `UseJsonContract(c => { })`". An empty lambda body should produce `JsonContractModel.Default` (null `RootPropertyName`, empty `CollectionJsonNames`). Without this test, there's no verification that an empty lambda doesn't crash or produce unexpected state.

**Recommendation:** Add to Step 1 tests:
- `graph.UseJsonContract(c => { })` with empty body -> `JsonContract.RootPropertyName` is null and `CollectionJsonNames` is empty

---

### Finding 6: Missing test — duplicate Collection<T> calls for same type

**Category:** Insufficient Test Coverage
**Action:** Ask User
**Detail:** Since `CollectionJsonNames` is a `Dictionary<string, string>`, calling `c.Collection<SearchLine>("lines")` followed by `c.Collection<SearchLine>("routes")` would silently overwrite `"lines"` with `"routes"`. The plan has no test specifying this behavior. Is this the desired behavior (last-wins), or should the parser detect and report this as an error?

**Recommendation:** User decision needed: Should duplicate `Collection<T>()` calls for the same type be (a) last-wins (current Dictionary behavior), (b) first-wins, or (c) a diagnostic error? Add a test for the chosen behavior.

---

### Finding 7: Missing test — Collection<T> with generic type parameter

**Category:** Insufficient Test Coverage
**Action:** Amend Plan
**Detail:** No test covers `c.Collection<List<SearchLine>>("lineCollections")` or similar generic type arguments. The `GetTypeArgumentSymbol` method at `ConfigurationParser.cs:496-511` handles this correctly (it returns `INamedTypeSymbol` for any resolved type), and `NormalizeFqn` with `FullyQualifiedFormat` would produce the right FQN. However, a test would verify the FQN format for generic types is consistent with what downstream consumers expect.

**Recommendation:** Add a test for `Collection<T>()` with a nested generic type to verify the FQN is stored correctly. This is lower priority but catches format mismatches.

---

### Finding 8: Missing test — UseJsonContract called multiple times

**Category:** Insufficient Test Coverage
**Action:** Amend Plan
**Detail:** No test covers what happens when `UseJsonContract` is called twice in the same graph lambda:

```csharp
graph.UseJsonContract(c => { c.RootPropertyName = "result"; });
graph.UseJsonContract(c => { c.RootPropertyName = "data"; });
```

Based on the `ParseContext` design (single `RootPropertyName` and single `CollectionJsonNames` dict), the second call would overwrite the first. This is the same pattern as `UseNaming` (which has a "CalledTwice_LastValuesWin" test at line 362). A corresponding test should exist.

**Recommendation:** Add test: "UseJsonContract called twice → last RootPropertyName wins" and "UseJsonContract called twice with different Collection<T> calls → all entries merged, overlapping keys use last value."

---

### Finding 9: Non-literal string argument handling for Collection<T>

**Category:** Insufficient Test Coverage
**Action:** Amend Plan
**Detail:** No test covers what happens with `c.Collection<SearchLine>(someVariable)` where the argument is not a string literal. The `ExtractStringArgument` helper (Finding 3) should return null for non-literals, and the `Collection` handler should silently skip storage. This matches the existing pattern for `WithName` (line 340-341) and `UseNaming` non-literal RHS (tested at line 343-360). A test should verify this.

**Recommendation:** Add test: "Collection<T> with non-literal string argument → silently ignored, no entry in CollectionJsonNames"

---

### Finding 10: ReceiverKind.ReferenceBuilder added prematurely in Task 3

**Category:** Fragile Code
**Action:** Accept
**Detail:** Step 3 adds both `JsonContractBuilder` and `ReferenceBuilder` to the `ReceiverKind` enum, but `ReferenceBuilder` is only used in Task 4. Adding it prematurely is harmless (unused enum values don't cause issues) and reduces the diff in Task 4. The plan explicitly marks this with a comment `// NEW (for Task 4)`.

**Recommendation:** Informational only. The approach is fine.

---

### Finding 11: ProcessJsonContractLambda doesn't clean up ReceiverMap

**Category:** State Issues
**Action:** Accept
**Detail:** The plan says `ProcessJsonContractLambda` should register the lambda param as `ReceiverKind.JsonContractBuilder` and call `ProcessStatements`. After the lambda body is processed, the param name remains in `ReceiverMap`. This follows the exact pattern of `ProcessGraphLambdaArgument` (line 372) and `ProcessUseNamingLambda` (line 395) — neither cleans up their receiver registrations. In practice this is safe because lambda parameter names are locally scoped and unlikely to collide with outer variable names in typical user code.

**Recommendation:** Informational only. Follows established pattern.

---

### Finding 12: ParseContext fields not cleared between multiple NormalizeGraph calls

**Category:** State Issues
**Action:** Accept
**Detail:** `RootPropertyName` and `CollectionJsonNames` are added as single fields on `ParseContext`. If a user calls `NormalizeGraph<A>` with `UseJsonContract` and then `NormalizeGraph<B>` without it, graph B would inherit graph A's `RootPropertyName`. This is the same limitation documented in the existing code at `ConfigurationParser.cs:620-623`:

> "NOTE: These are never cleared between multiple NormalizeGraph calls. Phase 1 assumes a single graph per config."

The plan inherits this known limitation. No regression.

**Recommendation:** Informational only. Same known limitation as Phase 1 naming.

---

### Finding 13: Test helper template may need JsonContractBuilder using directive

**Category:** Implicit Assumptions
**Action:** Accept
**Detail:** The test helper template at `ConfigurationParserTests.cs:557-572` includes `using DataNormalizer.Configuration;`. Since `JsonContractBuilder` will be in that namespace (Task 2 creates it there), the tests should compile without changes to the template. The test source code uses `graph.UseJsonContract(c => { ... })` which references `GraphBuilder<T>.UseJsonContract(Action<JsonContractBuilder>)` — the `JsonContractBuilder` type is resolved via the using directive.

**Recommendation:** Informational only. No template change needed.

---

## No issues found in: Non-Strict Typing

The plan uses established patterns with `INamedTypeSymbol?`, proper null checks, and dictionary types consistent with the existing codebase. No `any`-equivalent or loose typing issues.

---

## Summary Table

| # | Category | Action | Issue |
|---|----------|--------|-------|
| 1 | Incorrect Code | **Amend Plan** | `ProcessAssignment` guard on line 425 blocks new `JsonContractBuilder` case |
| 2 | Incorrect Code | **Amend Plan** | `ToDisplayString(...)` has literal ellipsis instead of `SymbolDisplayFormat.FullyQualifiedFormat` |
| 3 | Missing Wiring | **Amend Plan** | `ExtractStringArgument` helper not defined anywhere |
| 4 | Implicit Assumptions | Accept | Tasks 1-2 prerequisites not yet implemented |
| 5 | Insufficient Test Coverage | **Amend Plan** | Missing test: empty UseJsonContract lambda body |
| 6 | Insufficient Test Coverage | **Ask User** | Missing test: duplicate Collection<T> calls — what's the intended behavior? |
| 7 | Insufficient Test Coverage | **Amend Plan** | Missing test: Collection<T> with generic type argument |
| 8 | Insufficient Test Coverage | **Amend Plan** | Missing test: UseJsonContract called multiple times (last-wins semantics) |
| 9 | Insufficient Test Coverage | **Amend Plan** | Missing test: Collection<T> with non-literal string argument |
| 10 | Fragile Code | Accept | ReferenceBuilder enum value added early |
| 11 | State Issues | Accept | Lambda param not cleaned from ReceiverMap (follows existing pattern) |
| 12 | State Issues | Accept | ParseContext JsonContract fields not reset per graph (known Phase 1 limitation) |
| 13 | Implicit Assumptions | Accept | Test template already has correct using directive |
