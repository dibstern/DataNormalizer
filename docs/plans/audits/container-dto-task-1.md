# Audit: Task 1 — ContainerEmitter

**Summary:** Task 1 has several bugs that will cause tests to fail as written: a missing `using` directive, inconsistent `TypeFullName` values vs codebase conventions, a `Properties.Length == 0` skip that contradicts a test assertion, and an unnecessary `GetCSharpTypeName` helper that would produce double `?` for nullable value types.

## Findings

### Finding 1: Missing `using NUnit.Framework;` in test file
**Category:** Incorrect Code
**Action:** Amend Plan
**Details:** The planned test file at lines 35-37 includes:
```csharp
using DataNormalizer.Generators.Emitters;
using DataNormalizer.Generators.Models;
using System.Collections.Immutable;
```
But is missing `using NUnit.Framework;`. The project has no implicit usings enabled (checked `DataNormalizer.Generators.Tests.csproj` — no `<ImplicitUsings>enable</ImplicitUsings>`) and no global usings file. Without this import, `[TestFixture]`, `[Test]`, `Assert.That`, `Does.Contain`, and `Does.Not.Contain` will not compile.

The existing `DtoEmitterTests.cs` has `using NUnit.Framework;` at line 4.

**Recommendation:** Add `using NUnit.Framework;` to the test file's imports.

---

### Finding 2: Test uses `System.String` as TypeFullName but expects `string` in output
**Category:** Incorrect Code
**Action:** Amend Plan
**Details:** The `RootWithSimpleProperty_EmitsSimplePropertyDirectly` test (plan lines 144-170) sets:
```csharp
TypeFullName = "System.String",
```
But asserts:
```csharp
Assert.That(result, Does.Contain("public string OrderNumber { get; set; } = default!;"));
```

The implementation's `GetCSharpTypeName` (plan line 399-405) returns `typeFullName` as-is (no mapping from `System.String` to `string`), so the actual output would be `public System.String OrderNumber { get; set; } = default!;`.

The real Roslyn `TypeGraphAnalyzer` uses `SymbolDisplayFormat.FullyQualifiedFormat` which renders `System.String` as `global::string` → stripped to `string`. All existing `DtoEmitterTests` consistently use `"string"` (the C# keyword alias) as `TypeFullName`, not `"System.String"`.

The same issue appears in the first test (lines 79-80 for personNode's Name property) and the nullable test (lines 207-212 for addressNode's City property), though these properties don't have corresponding assertions that would fail for this reason.

**Recommendation:** Change all test `TypeFullName = "System.String"` to `TypeFullName = "string"` to match codebase conventions and the actual analyzer output. This also makes the `GetCSharpTypeName` helper unnecessary — just use `prop.TypeFullName` directly, like `DtoEmitter.EmitSimpleProperty` does.

---

### Finding 3: `EmitEntityListProperty` skips nodes with empty Properties, but test expects them
**Category:** Incorrect Code
**Action:** Amend Plan
**Details:** The implementation at plan line 332-333:
```csharp
if (node.Properties.Length == 0)
    continue;
```
This skip logic will cause the `JsonNamingPolicy_EmitsJsonPropertyNameAttributes` test to fail. That test (lines 247-258) creates a `personNode` with `Properties = ImmutableArray<AnalyzedProperty>.Empty` and then asserts:
```csharp
Assert.That(result, Does.Contain("[System.Text.Json.Serialization.JsonPropertyName(\"personList\")]"));
```

The node would be skipped, so the assertion would fail. Moreover, the skip logic is semantically wrong: a type in the normalization graph should always get an entity list in the container, regardless of how many properties the type has. A type with zero properties is unusual but valid (it could still be tracked as an entity for reference-equality purposes).

**Recommendation:** Remove the `if (node.Properties.Length == 0) continue;` guard from `EmitEntityListProperty`'s loop in the `Emit` method.

---

### Finding 4: `GetCSharpTypeName` produces double `?` for nullable value types
**Category:** Incorrect Code
**Action:** Amend Plan
**Details:** The plan's helper (lines 399-405):
```csharp
private static string GetCSharpTypeName(string typeFullName, bool isNullable, bool isReferenceType)
{
    var baseName = typeFullName;
    if (isNullable && !isReferenceType)
        return baseName + "?";
    return baseName;
}
```

The Roslyn analyzer stores `TypeFullName` as the full display of the *original* type, including `?` for nullable value types (e.g., `int?` for `Nullable<int>`). The `IsNullable` flag is set separately. So if the analyzer produces `TypeFullName = "int?"` and `IsNullable = true`, this function returns `"int??"`.

Additionally, this function is unnecessary — `DtoEmitter.EmitSimpleProperty` (DtoEmitter.cs:132) just uses `prop.TypeFullName` directly without any mapping. The ContainerEmitter should do the same.

**Recommendation:** Remove `GetCSharpTypeName` entirely. In `EmitRootProperty`, for Simple/Inlined cases, use `prop.TypeFullName` directly (matching DtoEmitter's pattern):
```csharp
case PropertyKind.Simple:
case PropertyKind.Inlined:
    EmitJsonNamingAttribute(sb, prop.Name, jsonNamingPolicy);
    var simpleDefault = prop.IsReferenceType ? " = default!;" : "";
    sb.AppendLine($"    public {prop.TypeFullName} {prop.Name} {{ get; set; }}{simpleDefault}");
    break;
```

---

### Finding 5: No test for Inlined properties on root
**Category:** Insufficient Test Coverage
**Action:** Amend Plan
**Details:** The design doc's mapping table (design doc line 74) specifies that Inlined object properties on the root should be "Kept as-is". The implementation handles this by merging Inlined with Simple in the switch (plan line 347). However, no test exercises this path with an Inlined property on the root node.

While the code path IS covered (shared with Simple), the emitted type for Inlined is the original complex type (e.g., `TestApp.Metadata`), which is different from Simple types like `string` or `int`. A test would verify this distinction.

**Recommendation:** Add a test case like:
```csharp
[Test]
public void RootWithInlinedProperty_EmitsPropertyWithOriginalType()
{
    var rootNode = new TypeGraphNode { ... Properties = ImmutableArray.Create(
        new AnalyzedProperty { Name = "Meta", TypeFullName = "TestApp.Metadata", Kind = PropertyKind.Inlined, IsReferenceType = true, ... }
    )};
    var result = ContainerEmitter.Emit(rootNode, nonRootNodes: [], jsonNamingPolicy: null);
    Assert.That(result, Does.Contain("public TestApp.Metadata Meta { get; set; } = default!;"));
}
```

---

### Finding 6: No test for root with zero properties (only entity lists)
**Category:** Insufficient Test Coverage
**Action:** Accept
**Details:** There's no test for a root node with zero properties paired with non-root entity nodes. This would produce a container with only entity list properties and no root properties. The code would handle this correctly (empty foreach loop), but it's an edge case worth noting.

---

### Finding 7: DtoEmitter uses different `ToCamelCase` than `EmitterHelpers.ToCamelCase`
**Category:** Fragile Code
**Action:** Accept
**Details:** `DtoEmitter.ToCamelCase` (DtoEmitter.cs:110-116) does simple first-char lowering: `"IDName"` → `"iDName"`. `EmitterHelpers.ToCamelCase` (EmitterHelpers.cs:14-46) handles acronyms: `"IDName"` → `"idName"`. The plan's ContainerEmitter uses `EmitterHelpers.ToCamelCase`, which is the better implementation, but it means per-type DTO properties and container properties use different camelCase logic. This won't cause bugs for common property names (single uppercase prefix), and is a pre-existing inconsistency in the codebase, not introduced by this task.

---

### Finding 8: Hardcoded `GeneratedCode` attribute values in DtoEmitter vs constants in ContainerEmitter
**Category:** Fragile Code
**Action:** Accept
**Details:** `DtoEmitter` (DtoEmitter.cs:26) hardcodes `"DataNormalizer", "1.0.0"` in its `[GeneratedCode]` attribute, while the plan's `ContainerEmitter` uses `EmitterHelpers.GeneratorName` and `EmitterHelpers.GeneratorVersion` constants. This is actually better practice in the new code. The inconsistency is pre-existing and not introduced by this task.

---

## Summary Table

| # | Category | Action | Issue |
|---|----------|--------|-------|
| 1 | Incorrect Code | Amend Plan | Missing `using NUnit.Framework;` in test file |
| 2 | Incorrect Code | Amend Plan | `TypeFullName = "System.String"` should be `"string"` to match codebase convention |
| 3 | Incorrect Code | Amend Plan | `Properties.Length == 0` skip contradicts test assertion and is semantically wrong |
| 4 | Incorrect Code | Amend Plan | `GetCSharpTypeName` would produce `"int??"` for nullable value types; should be removed |
| 5 | Insufficient Test Coverage | Amend Plan | No test for Inlined properties on root |
| 6 | Insufficient Test Coverage | Accept | No test for root with zero properties |
| 7 | Fragile Code | Accept | Different `ToCamelCase` implementations between DtoEmitter and EmitterHelpers |
| 8 | Fragile Code | Accept | Hardcoded vs constant `GeneratedCode` attribute values (pre-existing) |

**No issues found in:** Implicit Assumptions (all referenced helpers and types verified to exist with correct signatures)
