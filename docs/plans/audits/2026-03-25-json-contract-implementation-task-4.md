# Audit: Task 4 — Extend ConfigurationParser for Reference().JsonName()

**Auditor:** Claude (automated)
**Date:** 2026-03-25
**Scope:** Task 4 of the JSON Contract Implementation Plan

**Summary:** Task 4 introduces cross-type method chaining in ConfigurationParser to handle `x.Reference(p => p.Line).JsonName("line")`. The core design uses `CurrentReferenceKey` as mutable state that persists across statements. The audit found **6 issues requiring plan amendment**, including a critical double-processing bug where the plan's Step 5 restructures the existing chaining logic but the plan's code will process inner invocations twice (once in the new early-return path, once in the existing unconditional recursive call), a state corruption scenario where consecutive `Reference()` calls silently overwrite the key, missing `ExtractStringArgument` helper method, and several test coverage gaps for edge cases that could produce silent data loss.

---

## Findings

### Finding 1: Double-processing of inner invocation — plan's Step 5 creates duplicate execution

**Category:** Incorrect Code / State Issues
**Action:** Amend Plan
**File:** `src/DataNormalizer.Generators/Analysis/ConfigurationParser.cs:117-123` (existing code) + Plan Step 5 code

**Detail:** The existing `AnalyzeInvocation` method (line 117-123) already has an unconditional recursive call to process the inner invocation:

```csharp
if (invocation.Expression is MemberAccessExpressionSyntax outerAccess
    && outerAccess.Expression is InvocationExpressionSyntax innerInvocation)
{
    AnalyzeInvocation(innerInvocation, context);  // EXISTING: always fires
}
```

The plan's Step 5 **replaces** this block with a new version that captures the return value and handles JsonName specially. However, the plan text says "At top of AnalyzeInvocation, when outer receiver is an invocation" — implying this is added at the top. If the implementer adds the new code *before* the existing block without removing the existing block, the inner invocation gets processed twice.

Even if the implementer replaces the block correctly, there's still a subtlety: when the new code detects `JsonName` on a `ReferenceBuilder`, it processes the JsonName and returns early (`return ReceiverKind.ReferenceBuilder`). But the existing code below (lines 125-193) would also try to process the same outer invocation. The plan's early return prevents this, which is correct. However, the plan doesn't explicitly state "replace the existing recursive call block" — it says "at top of AnalyzeInvocation."

Furthermore, for chains like `x.Reference(p => p.Line).JsonName("line")`:
1. The new code calls `AnalyzeInvocation(innerInvocation, context)` for `x.Reference(p => p.Line)` — this sets `CurrentReferenceKey` and returns `ReferenceBuilder`
2. The new code then handles `JsonName` and returns early
3. But if it's NOT a `JsonName` call (e.g., some future method on ReferenceBuilder), the code falls through, and the **existing** recursive call at line 122 would fire again, calling `AnalyzeInvocation(innerInvocation, context)` a second time, re-processing `Reference()` and overwriting `CurrentReferenceKey` (though it's the same value)

**Recommendation:** Amend the plan to explicitly state: "Replace the existing recursive chaining block (lines 117-123) with the new code." The new block should handle BOTH the existing same-type chaining case AND the new cross-type chaining case. The new code should use `innerResult` for all purposes and not fall through to a second recursive call. Specifically:

```csharp
if (invocation.Expression is MemberAccessExpressionSyntax outerAccess
    && outerAccess.Expression is InvocationExpressionSyntax innerInvocation)
{
    var innerResult = AnalyzeInvocation(innerInvocation, context);
    
    // Cross-type chain: JsonName on ReferenceBuilder
    if (innerResult == ReceiverKind.ReferenceBuilder)
    {
        var outerMethodName = GetMethodName(outerAccess);
        if (outerMethodName == "JsonName")
        {
            var jsonName = ExtractStringArgument(invocation);
            if (jsonName != null && context.CurrentReferenceKey != null)
                context.PropertyJsonNames[context.CurrentReferenceKey] = jsonName;
            context.CurrentReferenceKey = null;
            return ReceiverKind.ReferenceBuilder;
        }
        // Unknown method on ReferenceBuilder — fall through to normal processing
    }
    // For same-type chains (TypeBuilder -> TypeBuilder, etc.), fall through
    // to process the outer method using GetUltimateReceiverName as before
}
```

This eliminates the double-processing risk entirely.

---

### Finding 2: `GetUltimateReceiverName` resolves to the TypeBuilder root — confirmed correct for same-type chains, but wrong for cross-type chains

**Category:** Implicit Assumptions
**Action:** Accept (plan handles this correctly)
**File:** `src/DataNormalizer.Generators/Analysis/ConfigurationParser.cs:528-537`

**Detail:** The plan states: "GetUltimateReceiverName resolves to the root receiver (TypeBuilder), which won't work for JsonName on ReferenceBuilder." This is confirmed correct by reading the code:

```csharp
private static string? GetUltimateReceiverName(MemberAccessExpressionSyntax memberAccess)
{
    return memberAccess.Expression switch
    {
        IdentifierNameSyntax id => id.Identifier.Text,
        InvocationExpressionSyntax inv when inv.Expression is MemberAccessExpressionSyntax innerAccess =>
            GetUltimateReceiverName(innerAccess),
        _ => null,
    };
}
```

For `x.Reference(p => p.Line).JsonName("line")`, this walks through:
- `JsonName` member access: Expression is `x.Reference(p => p.Line)` (InvocationExpression) -> recurse
- `Reference` member access: Expression is `x` (IdentifierName) -> returns `"x"`

So `GetUltimateReceiverName` returns `"x"`, and `ReceiverMap["x"]` is `ReceiverKind.TypeBuilder`. The switch would then try to match `"JsonName"` with `ReceiverKind.TypeBuilder` and fall through to `default: return null`.

The plan correctly handles this by intercepting the chain in the early block before `GetUltimateReceiverName` is called. The early return prevents the TypeBuilder-based lookup.

**Recommendation:** None. The plan's approach is correct, but this reinforces why Finding 1 matters — if the early return doesn't fire, the fallthrough will misidentify the receiver.

---

### Finding 3: `ExtractStringArgument` helper is used but never defined in the plan

**Category:** Missing Wiring
**Action:** Amend Plan
**File:** Plan Step 5 and Step 6 code snippets

**Detail:** The plan calls `ExtractStringArgument(invocation)` in both Step 5 (chained path) and Step 6 (split-statement path). This method does not exist in the current codebase. The plan also references it in Task 3 Step 6 (`var jsonName = ExtractStringArgument(invocation);`), suggesting it was introduced there, but Task 3's text only shows pseudocode for it — no actual method definition.

Looking at the existing codebase, the closest analogues are:
- `ExtractPropertyNameFromLambdaArg` (line 560) — extracts from lambda body
- `ProcessWithName` (line 326) — extracts string from literal argument inline, not via helper

`ExtractStringArgument` needs to:
1. Get argument 0 from the invocation's ArgumentList
2. Check if it's a `LiteralExpressionSyntax`
3. Return `literal.Token.ValueText`

**Recommendation:** Amend the plan to add an explicit step defining `ExtractStringArgument`:

```csharp
private static string? ExtractStringArgument(InvocationExpressionSyntax invocation)
{
    if (invocation.ArgumentList.Arguments.Count == 0)
        return null;
    var argExpr = invocation.ArgumentList.Arguments[0].Expression;
    return argExpr is LiteralExpressionSyntax literal ? literal.Token.ValueText : null;
}
```

Note: This may already be created in Task 3. If so, the plan should cross-reference: "Uses `ExtractStringArgument` added in Task 3 Step 6."

---

### Finding 4: `CurrentReferenceKey` silently overwritten by consecutive `Reference()` calls without consumption

**Category:** State Issues
**Action:** Amend Plan

**Detail:** The plan sets `CurrentReferenceKey` in Step 4 whenever `Reference()` or `ReferenceCollection()` is encountered. If a user writes:

```csharp
x.Reference(p => p.Line);       // sets CurrentReferenceKey = "Type.Line"
x.Reference(p => p.Vehicle);    // overwrites to "Type.Vehicle" — "Type.Line" key is LOST
```

This is likely benign (the user didn't call `.JsonName()` on either), but it's a latent data integrity issue. More problematically:

```csharp
x.Reference(p => p.Line);           // sets key = "Type.Line"
x.Reference(p => p.Vehicle).JsonName("v");  // key is ALREADY "Type.Line" when Reference() fires
                                             // Reference() overwrites to "Type.Vehicle"
                                             // JsonName() stores "Type.Vehicle" -> "v" ✓ CORRECT
```

Wait — this actually works because the chaining logic processes inner-to-outer. For `x.Reference(p => p.Vehicle).JsonName("v")`, the inner `Reference()` call sets the key, then the outer `JsonName()` consumes it. The standalone `x.Reference(p => p.Line)` was processed in a previous statement.

But there IS a problem with the split-statement pattern:

```csharp
var r1 = x.Reference(p => p.Line);       // sets CurrentReferenceKey = "Type.Line"  
var r2 = x.Reference(p => p.Vehicle);    // overwrites CurrentReferenceKey = "Type.Vehicle"
r1.JsonName("line");                      // consumes CurrentReferenceKey = "Type.Vehicle" — WRONG!
r2.JsonName("vehicle");                  // CurrentReferenceKey is null — LOST!
```

The split-statement pattern only works for a single `Reference()` / `JsonName()` pair at a time. Multiple split-statement references are broken.

**Recommendation:** Amend the plan to either:

**Option A (simple, covers most cases):** Document the limitation: "The split-statement pattern supports only one `Reference()` without an immediately following `JsonName()` at a time. Multiple split references require immediate `.JsonName()` chaining." Add a test for the two-variable-split case that verifies it produces no crash (though the data will be wrong).

**Option B (correct, more complex):** Instead of a single `CurrentReferenceKey`, use a per-variable mapping. When `Reference()` is assigned to a local variable via `ProcessLocalDeclaration`, store the reference key in a `Dictionary<string, string> ReferenceBuilderKeyMap` (varName -> referenceKey). When `r.JsonName("line")` is processed with `receiverKind == ReceiverKind.ReferenceBuilder`, look up `receiverName` in `ReferenceBuilderKeyMap` instead of using `CurrentReferenceKey`. This handles multiple split-statement references correctly.

This is a design decision that affects correctness for an edge case.

---

### Finding 5: `CurrentReferenceKey` is not declared in ParseContext in the plan

**Category:** Missing Wiring
**Action:** Amend Plan
**File:** Plan Step 3 vs Step 4

**Detail:** Step 3 adds `PropertyJsonNames` to `ParseContext`. Step 4 uses `context.CurrentReferenceKey` but never shows it being added to `ParseContext`. The plan should include:

```csharp
public string? CurrentReferenceKey { get; set; }
```

in the `ParseContext` class additions in Step 3.

**Recommendation:** Amend Step 3 to explicitly add `CurrentReferenceKey` to `ParseContext`.

---

### Finding 6: `ReferenceBuilder` ReceiverKind is referenced but plan says it's "added in Task 3"

**Category:** Implicit Assumptions
**Action:** Accept (cross-task dependency, correctly sequenced)
**File:** Plan Task 3 Step 3

**Detail:** Task 3 Step 3 adds `ReferenceBuilder` to the `ReceiverKind` enum (line 207: `ReferenceBuilder, // NEW (for Task 4)`). Task 4 then uses it. This is correctly sequenced — Task 3 must run before Task 4.

**Recommendation:** None. Dependency is correctly documented.

---

### Finding 7: Tests missing — consecutive Reference() calls overwriting CurrentReferenceKey

**Category:** Insufficient Test Coverage
**Action:** Amend Plan

**Detail:** The plan lists these tests:
- `Reference().JsonName()` -> entry stored
- `ReferenceCollection().JsonName()` -> same
- Multiple `Reference().JsonName()` on same TypeBuilder -> all stored
- `Reference()` without `JsonName()` -> no entry, no crash
- Split-statement pattern

**Missing critical tests:**

1. **Two `Reference()` calls without `JsonName()` in between:** `x.Reference(p => p.A); x.Reference(p => p.B);` — verify no crash, no spurious entries (and that `CurrentReferenceKey` doesn't leak into later unrelated statements)

2. **`Reference()` without `JsonName()` followed by `Reference().JsonName()`:** `x.Reference(p => p.A);` then `x.Reference(p => p.B).JsonName("b");` — verify only `"Type.B"` is stored, not `"Type.A"`

3. **`JsonName()` with empty string argument:** `x.Reference(p => p.A).JsonName("")` — should it store `""` or skip? `ExtractStringArgument` will return `""` which is truthy. Plan should decide.

4. **`JsonName()` with non-literal argument:** `x.Reference(p => p.A).JsonName(someVariable)` — `ExtractStringArgument` returns null. Verify no crash, no entry stored.

5. **Two `ForType` blocks each with `Reference().JsonName()`:** Verify keys are `"Type1.Prop"` and `"Type2.Prop"` — tests isolation between type contexts.

6. **`Reference()` on property that doesn't exist:** `x.Reference(p => p.NonExistent)` — `ExtractPropertyNameFromLambdaArg` will return `"NonExistent"` since it does syntax-only extraction (line 560-574). The key will be `"Type.NonExistent"`. Verify this doesn't crash (it will just be an unused key).

7. **Intervening non-Reference statements between split `Reference()` and `JsonName()`:**
   ```csharp
   var r = x.Reference(p => p.Line);
   x.IgnoreProperty(p => p.Name);  // intervening statement
   r.JsonName("line");
   ```
   Verify `CurrentReferenceKey` survives the intervening IgnoreProperty call. Looking at `ProcessPropertyAction` (line 261-324) — it does NOT touch `CurrentReferenceKey`, so the key survives. But this should be tested.

**Recommendation:** Add tests 1-5 to the plan. Tests 6-7 are lower priority (Accept if not added) but would strengthen coverage.

---

### Finding 8: `ProcessLocalDeclaration` exists and correctly returns ReceiverKind — split-statement works

**Category:** Implicit Assumptions
**Action:** Accept

**File:** `src/DataNormalizer.Generators/Analysis/ConfigurationParser.cs:85-101`

**Detail:** The plan says "ProcessLocalDeclaration returning the kind from AnalyzeInvocation" for the split-statement pattern. Verifying the existing code:

```csharp
private static void ProcessLocalDeclaration(LocalDeclarationStatementSyntax localDecl, ParseContext context)
{
    foreach (var variable in localDecl.Declaration.Variables)
    {
        if (variable.Initializer?.Value is not InvocationExpressionSyntax invocation)
            continue;
        var varName = variable.Identifier.Text;
        var result = AnalyzeInvocation(invocation, context);
        if (result is not null)
        {
            context.ReceiverMap[varName] = result.Value;
        }
    }
}
```

This correctly calls `AnalyzeInvocation`, gets the return `ReceiverKind`, and maps the variable name. When `Reference()` returns `ReceiverKind.ReferenceBuilder`, the variable `r` will be mapped to `ReferenceBuilder`. Then `r.JsonName("line")` will look up `r` in `ReceiverMap`, get `ReferenceBuilder`, and match the `"JsonName" when receiverKind == ReceiverKind.ReferenceBuilder` case.

The `GetUltimateReceiverName` call for `r.JsonName("line")` returns `"r"` (simple identifier), so `ReceiverMap["r"]` works correctly.

**Recommendation:** None. The plan's split-statement approach is sound.

---

### Finding 9: `TypeBuilderMap` exists in ParseContext — confirmed

**Category:** Implicit Assumptions
**Action:** Accept

**File:** `src/DataNormalizer.Generators/Analysis/ConfigurationParser.cs:639`

**Detail:** The plan uses `context.TypeBuilderMap.TryGetValue(receiverName, out var refTypeFqn)` in Step 4. Confirming it exists at line 639:

```csharp
public Dictionary<string, string> TypeBuilderMap { get; } = new();
```

And it's populated in `ProcessForType` at line 240:
```csharp
context.TypeBuilderMap[lambdaParamName] = fqn;
```

This maps lambda parameter names (e.g., `"x"` in `p => { x.Reference(...) }`) to their fully-qualified type names.

**Recommendation:** None.

---

### Finding 10: Three-deep chains — plan doesn't address `x.Reference(p => p.A).JsonName("a").SomeFutureMethod()`

**Category:** Fragile Code
**Action:** Accept

**Detail:** For `x.Reference(p => p.A).JsonName("a")` as a 2-deep chain:
- Outer: `JsonName("a")`, inner: `x.Reference(p => p.A)` -> works per plan

For a hypothetical 3-deep chain like `x.Reference(p => p.A).JsonName("a").SomeFuture()`:
- Outer: `SomeFuture()`, inner: `x.Reference(p => p.A).JsonName("a")`
- The recursive `AnalyzeInvocation` on the inner would process the Reference+JsonName pair correctly
- The outer `SomeFuture()` would fall through to default (return null) since no handler exists
- `GetUltimateReceiverName` would walk to `x` -> TypeBuilder, and try to match `"SomeFuture"` on TypeBuilder -> no match -> return null

This is safe today because `ReferenceBuilder` only has `JsonName()`. If future methods are added, they would need the same cross-type handling pattern. This is a design limitation, not a bug.

**Recommendation:** None. The current plan handles the exact API surface. Future ReferenceBuilder methods would need additional handlers, but that's a future concern.

---

### Finding 11: String-based key format `"{typeFqn}.{refPropName}"` must match TypeGraphAnalyzer's lookup format

**Category:** Fragile Code
**Action:** Amend Plan

**File:** Plan Step 4 (key construction) vs Plan Task 5 Step 3 (key lookup)

**Detail:** In Task 4 Step 4, the key is constructed as:
```csharp
context.CurrentReferenceKey = $"{refTypeFqn}.{refPropName}";
```

In Task 5 Step 3, the key is looked up as:
```csharp
var propKey = $"{typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}.{prop.Name}";
propKey = NormalizeFqn(propKey);
```

The `refTypeFqn` in Task 4 comes from `context.TypeBuilderMap[receiverName]`, which is set in `ProcessForType` (line 225-226):
```csharp
var fqn = NormalizeFqn(typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
context.TypeBuilderMap[lambdaParamName] = fqn;
```

So the parser's key uses an already-normalized FQN (no `global::` prefix). The analyzer in Task 5 normalizes after construction. These should match, but the coupling is fragile — there's no shared constant or helper ensuring the key format is identical.

**Recommendation:** Amend the plan to add a note in Task 5 (or a shared comment) that the key format `"{normalizedFqn}.{propertyName}"` must match exactly between ConfigurationParser (writer) and TypeGraphAnalyzer (reader). Alternatively, extract a static helper like `PropertyKey(string typeFqn, string propName) => $"{typeFqn}.{propName}"` used by both. This is a minor fragility — not a bug today, but a maintenance trap.

---

### Finding 12: `ExtractPropertyNameFromLambdaArg` exists and works correctly for the Reference() use case

**Category:** Implicit Assumptions
**Action:** Accept

**File:** `src/DataNormalizer.Generators/Analysis/ConfigurationParser.cs:560-574`

**Detail:** The plan's Step 4 uses `ExtractPropertyNameFromLambdaArg(invocation)` for the Reference() call. The existing implementation:

```csharp
private static string? ExtractPropertyNameFromLambdaArg(InvocationExpressionSyntax invocation)
{
    if (invocation.ArgumentList.Arguments.Count == 0) return null;
    var argExpr = invocation.ArgumentList.Arguments[0].Expression;
    var body = GetLambdaBody(argExpr);
    return body switch
    {
        MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.Text,
        _ => null,
    };
}
```

For `x.Reference(p => p.Line)`, the lambda body `p.Line` is a `MemberAccessExpressionSyntax`, so it returns `"Line"`. This works correctly. The method is syntax-only (doesn't verify the property exists on the type), which is fine for the parser's role.

**Recommendation:** None.

---

## Summary Table

| # | Category | Action | Issue | File:Line | Amendment / Question |
|---|----------|--------|-------|-----------|----------------------|
| 1 | Incorrect Code | **Amend Plan** | Double-processing of inner invocation — plan must explicitly replace existing recursive block, not add alongside it | ConfigurationParser.cs:117-123 | Rewrite Step 5 to show full replacement of the existing chaining block, not an addition. Show the unified block handling both same-type and cross-type chains. |
| 2 | Implicit Assumptions | Accept | GetUltimateReceiverName resolves to TypeBuilder root — confirmed true, plan handles correctly | ConfigurationParser.cs:528-537 | -- |
| 3 | Missing Wiring | **Amend Plan** | `ExtractStringArgument` helper used but never defined | Plan Step 5, Step 6 | Add explicit step defining `ExtractStringArgument` or cross-reference Task 3 where it's introduced |
| 4 | State Issues | **Amend Plan** | `CurrentReferenceKey` silently overwritten by consecutive `Reference()` calls; split-statement pattern with multiple variables is broken | Plan Step 4, Step 6 | Either document the limitation or implement per-variable `ReferenceBuilderKeyMap` (Ask User for design choice) |
| 5 | Missing Wiring | **Amend Plan** | `CurrentReferenceKey` never added to ParseContext in the plan | Plan Step 3 | Add `public string? CurrentReferenceKey { get; set; }` to ParseContext additions |
| 6 | Implicit Assumptions | Accept | `ReferenceBuilder` ReceiverKind added in Task 3 — correct dependency ordering | Plan Task 3 Step 3 | -- |
| 7 | Insufficient Test Coverage | **Amend Plan** | Missing tests for key overwrite scenarios, empty string JsonName, non-literal JsonName arg, cross-ForType isolation, intervening statements | -- | Add 5 tests: (1) Reference+Reference without JsonName, (2) Reference then Reference().JsonName(), (3) JsonName(""), (4) JsonName(variable), (5) two ForType blocks with Reference().JsonName() |
| 8 | Implicit Assumptions | Accept | ProcessLocalDeclaration exists and correctly maps variable to ReceiverKind | ConfigurationParser.cs:85-101 | -- |
| 9 | Implicit Assumptions | Accept | TypeBuilderMap exists and is populated correctly | ConfigurationParser.cs:639, :240 | -- |
| 10 | Fragile Code | Accept | Three-deep chains not handled but not needed for current API surface | -- | -- |
| 11 | Fragile Code | **Amend Plan** | Key format `"{typeFqn}.{propName}"` coupling between parser and analyzer is implicit | Plan Step 4 + Task 5 Step 3 | Add note or shared helper ensuring key format consistency between ConfigurationParser and TypeGraphAnalyzer |
| 12 | Implicit Assumptions | Accept | ExtractPropertyNameFromLambdaArg exists and works for Reference() | ConfigurationParser.cs:560-574 | -- |

**No issues found in:** Non-Strict Typing (the plan uses proper types throughout)

**Critical items requiring plan amendment:** Findings 1, 3, 4, 5, 7, 11

**Design decision needed from user:** Finding 4 — should the split-statement pattern support multiple simultaneous `Reference()` variables, or is the single-variable limitation acceptable?
