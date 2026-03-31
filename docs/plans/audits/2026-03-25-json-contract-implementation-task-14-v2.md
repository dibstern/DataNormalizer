# Task 14: Add unparsed config statement diagnostics — V2 Re-Audit Report

**Summary:** The v1 critical findings (storing `Diagnostic` objects, wrong file path, wrong test case `n.DtoSuffix = GetSuffix()`, missing try/finally) were all addressed in the amended plan. However, the amended plan introduces a new type confusion between `DiagnosticInfo` and `GeneratorDiagnosticInfo`, and the pipeline wiring from `ParseContext` through `NormalizationModel` to the `Emit` method has a gap where `GeneratorDiagnosticInfo` needs to be extended or a second type needs to be consistently used.

---

## Verification of v1 Amendments

### v1 Finding #7 (wrong file path): FIXED
The amended plan correctly says "file ALREADY EXISTS at this path" for `src/DataNormalizer.Generators/Diagnostics/DiagnosticDescriptors.cs` (line 1154) and says "Add to the EXISTING `Diagnostics/DiagnosticDescriptors.cs`" (line 1162).

### v1 Finding #1 (wrong test case `n.DtoSuffix = GetSuffix()`): FIXED
The amended plan removed this test case and added an explicit note at line 1271: "Do NOT use this as a test case for DN1001."

### v1 Finding #3/#15 (references non-existent `Execute` method): FIXED
The amended plan correctly references `RegisterSourceOutput` callback (line 1247) and shows the `spc.ReportDiagnostic` pattern.

### v1 Finding #4 (storing `Diagnostic` objects): PARTIALLY FIXED
The amended plan adds a "CRITICAL" warning at line 1184 not to store `Diagnostic` objects and defines a serializable `DiagnosticInfo` record. However, there's a type confusion (see Finding #1 below).

### v1 Finding #5/#6 (try/finally for `BuilderLambdaDepth`): FIXED
The amended plan explicitly shows the try/finally pattern and lists all four sites.

### v1 Finding #9 (ToFullString().Trim() → long messages): FIXED
The amended plan uses `statement.ToString()` and truncates to 100 chars (line 1227).

### v1 Finding #12 (deeply nested lambda test): FIXED
Added as test at line 1267.

### v1 Finding #13 (multiple diagnostics count test): FIXED
Added as test at line 1268.

---

## New/Remaining Findings

| # | Category | Action | Issue | File:Line | Amendment / Question |
|---|----------|--------|-------|-----------|----------------------|
| 1 | **Incorrect Code** | **Amend Plan** | Type confusion between `DiagnosticInfo` and `GeneratorDiagnosticInfo`. Step 2 (line 1188) says `ParseContext.Diagnostics` should be `List<GeneratorDiagnosticInfo>`, then immediately defines a NEW `DiagnosticInfo(string Id, string Message, int Line, int Column)` record. Step 4 (line 1228) adds `new DiagnosticInfo(...)` to `context.Diagnostics` — but if Diagnostics is `List<GeneratorDiagnosticInfo>`, this is a type mismatch. The existing `GeneratorDiagnosticInfo` at `NormalizeGenerator.cs:218` is `(string Id, string TypeName)` — it has NO `Message` field and no `Line`/`Column` fields. | `NormalizeGenerator.cs:218`, Plan lines 1188-1195, 1228-1233 | **Fix:** Decide on ONE approach: either (a) extend `GeneratorDiagnosticInfo` to add an optional `Message` field (e.g., change `TypeName` to a more generic `MessageArg`), or (b) use the new `DiagnosticInfo` type consistently. If (b), `ParseContext.Diagnostics` must be `List<DiagnosticInfo>` not `List<GeneratorDiagnosticInfo>`. The plan text at line 1188 must match the type used at line 1228. |
| 2 | **Missing Wiring** | **Amend Plan** | The pipeline conversion from `DiagnosticInfo` to `GeneratorDiagnosticInfo` is missing. `ParseContext` collects diagnostics → `NormalizationModel.ParserDiagnostics` (Step 5, line 1244) stores `ImmutableArray<DiagnosticInfo>`. But `TransformConfig` at `NormalizeGenerator.cs:100` builds `ImmutableArray<GeneratorDiagnosticInfo>` and puts them in `GeneratorOutput`. The plan's Step 5 code (line 1250-1256) iterates `model.ParserDiagnostics` — but `model` here refers to `NormalizationModel`, and `Emit` receives `GeneratorOutput`, not `NormalizationModel`. The conversion step in `TransformConfig` that reads `model.ParserDiagnostics` and adds them to the `diagnostics` builder as `GeneratorDiagnosticInfo` entries is never shown. | `NormalizeGenerator.cs:98-114, 179-184` | **Fix:** Add explicit step showing the code in `TransformConfig` that converts `model.ParserDiagnostics` (type `ImmutableArray<DiagnosticInfo>`) into `GeneratorDiagnosticInfo` entries and appends them to the `diagnostics` builder alongside the existing DN0001/DN0003 entries. Also update the `Emit` switch statement (line 200-205) to handle `"DN1001"` and `"DN1002"` IDs, since the current `default` case incorrectly maps to `ConfigClassMustBePartial`. |
| 3 | **Incorrect Code** | **Amend Plan** | The `Emit` method's switch at `NormalizeGenerator.cs:200-205` has a dangerous `default` fallthrough: any unrecognized diagnostic ID maps to `DiagnosticDescriptors.ConfigClassMustBePartial`. When DN1001 and DN1002 are added, if the `Emit` switch is not updated, they'll be reported as "Configuration class must be partial" errors. The amended plan shows `spc.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.UnparsedConfigStatement, Location.None, diag.Message))` at line 1252-1255, but this code appears to operate on `model.ParserDiagnostics` directly, not through the existing `foreach (var diag in output.Diagnostics)` loop. The plan must clarify WHERE this code goes. | `NormalizeGenerator.cs:200-206` | **Fix:** Specify that the `Emit` method's existing switch must be updated to add `"DN1001" => DiagnosticDescriptors.UnparsedConfigStatement` and `"DN1002" => DiagnosticDescriptors.DuplicateCollectionType`. Also decide: does `diag.TypeName` get passed as the message arg (which would be wrong for DN1001, where the arg is statement text not a type name), or does `GeneratorDiagnosticInfo` need a more generic field? |
| 4 | **Non-Strict Typing** | **Amend Plan** | The `GeneratorDiagnosticInfo(string Id, string TypeName)` record's second field is named `TypeName`, but for DN1001 it would carry statement text (not a type name), and for DN1002 it carries a type FQN. This is semantically misleading and fragile. | `NormalizeGenerator.cs:218` | **Fix:** Either rename `TypeName` to `MessageArg` (breaking existing usage at lines 107, 112, 206), or add a separate field. The plan should specify which approach and show the required changes to all call sites. |
| 5 | **Fragile Code** | **Amend Plan** | The `DiagnosticInfo` record stores `Line` and `Column` from `statement.GetLocation()` (Step 4, line 1231-1232), but Step 5 reconstructs the diagnostic with `Location.None` (line 1254). The Line/Column fields are collected but never used. This is dead data — either remove them from `DiagnosticInfo` (simplify to `(string Id, string Message)`) or reconstruct a `Location` from them. | Plan lines 1194-1195, 1254 | **Fix:** If `Location.None` is the final approach (which is consistent with existing diagnostics), simplify `DiagnosticInfo` to `(string Id, string Message)` and remove the `Line`/`Column` fields and the `GetLocation()` calls in Step 4. This also eliminates the `GetLineSpan()` call which can be expensive. |
| 6 | **Implicit Assumptions** | **Accept** | `Location.None` means DN1001 diagnostics will show in the IDE error list without a file/line reference. Users won't be able to click the error to jump to the problematic statement. This is consistent with existing DN0001-DN0004 diagnostics which also use `Location.None`. The truncated statement text in the message helps identify the issue, but for large config files this may be insufficient. | `NormalizeGenerator.cs:193, 206` | This is a known limitation of the serializable diagnostic approach in incremental generators. Not a bug, but worth noting. |
| 7 | **Implicit Assumptions** | **Amend Plan** | The plan's Step 3 lists lambda-entry methods as "`ProcessNamingLambda`, `ProcessJsonContractLambda`, `ProcessGraphLambda`, `ProcessForTypeLambda`" (line 1216). These names don't match the actual methods: `ProcessUseNamingLambda` (line 381), `ProcessGraphLambdaArgument` (line 361), `ProcessForType` (line 215), and the new `ProcessJsonContractLambda`. Using wrong names risks the implementer failing to find the right methods. | `ConfigurationParser.cs:215, 361, 381` | **Fix:** Update the method name list to: `ProcessUseNamingLambda`, `ProcessGraphLambdaArgument`, `ProcessForType`, and the new `ProcessJsonContractLambda`. |
| 8 | **Missing Wiring** | **Amend Plan** | DN1002 detection is in Task 3 (line 305-310), but DN1002's `DiagnosticDescriptor` is only added in Task 14 (line 1173-1179). Task 3 is a prerequisite for Task 14, so Task 3 would need to emit DN1002 diagnostics BEFORE the descriptor exists. Either (a) Task 3 must also add the DN1002 descriptor, or (b) the DN1002 detection code in Task 3 must reference a descriptor defined earlier, or (c) Task 3 stores diagnostic data without the descriptor and Task 14 adds both the descriptor and the emission. | Plan lines 305-310 (Task 3), 1173-1179 (Task 14) | **Fix:** Move the `DuplicateCollectionType` descriptor definition to Task 3 (where it's first used), or clarify that Task 3 only stores diagnostic data and Task 14 adds the descriptor + emission wiring. Currently the cross-task dependency is unclear. |
| 9 | **Incorrect Code** | **Amend Plan** | Task 3 Step 7 (line 308) uses `GeneratorDiagnosticInfo.DuplicateCollectionType(normalizedFqn, invocation.GetLocation())` — a static factory method that doesn't exist on the `GeneratorDiagnosticInfo` record struct, and it passes `invocation.GetLocation()` which is a `Location` object (violating the "no Location in pipeline data" rule from Task 14). This Task 3 code is inconsistent with Task 14's serializable approach. | Plan line 308 | **Fix:** Task 3's DN1002 diagnostic code must use the same serializable approach as Task 14. Replace `GeneratorDiagnosticInfo.DuplicateCollectionType(...)` with `context.Diagnostics.Add(new DiagnosticInfo("DN1002", normalizedFqn, ...))` or similar, matching Task 14's pattern. |
| 10 | **Missing Wiring** | **Accept** | The `ParseConfig` test helper at `ConfigurationParserTests.cs:555-604` returns `NormalizationModel`. Once `ParserDiagnostics` is added to `NormalizationModel`, the test assertions can access `model.ParserDiagnostics` directly. This wiring appears straightforward. | `ConfigurationParserTests.cs:604` | No issue — the test infrastructure supports this naturally. |

---

## Category Summary

**No issues found in:** (none — all categories had findings)

**Incorrect Code (3 findings: #1, #3, #9):** Type confusion between `DiagnosticInfo` and `GeneratorDiagnosticInfo` is the most critical. The `Emit` switch default-to-ConfigClassMustBePartial is a latent bug. Task 3's DN1002 code uses a non-existent factory method and passes `Location` objects.

**Missing Wiring (3 findings: #2, #8, #10):** The `TransformConfig` conversion step is unspecified. DN1002 descriptor ordering between Task 3 and Task 14 is unclear.

**Non-Strict Typing (1 finding: #4):** `TypeName` field semantically wrong for statement text.

**Fragile Code (1 finding: #5):** Dead `Line`/`Column` fields collected but never used.

**Implicit Assumptions (2 findings: #6, #7):** `Location.None` is acceptable but limits UX. Method names in the plan don't match actual code.

---

## Critical Path

The most impactful fix is **Finding #1 + #2 + #3 + #5 combined**: The plan should specify a single consistent diagnostic data type and show its complete journey through the pipeline:

1. Define `DiagnosticInfo(string Id, string MessageArg)` (without unused Line/Column)
2. `ParseContext.Diagnostics` is `List<DiagnosticInfo>`
3. `NormalizationModel.ParserDiagnostics` is `ImmutableArray<DiagnosticInfo>`
4. In `TransformConfig`, convert each `DiagnosticInfo` to `GeneratorDiagnosticInfo` (after renaming `TypeName` to `MessageArg`) and add to the `diagnostics` builder
5. In `Emit`, update the switch to handle `"DN1001"` and `"DN1002"` IDs

Alternatively: unify on `GeneratorDiagnosticInfo` everywhere by renaming its `TypeName` field to `MessageArg`, then use it directly in `ParseContext.Diagnostics` and `NormalizationModel.ParserDiagnostics`. This avoids introducing a second type entirely.
