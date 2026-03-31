# Audit Synthesis: JSON Contract Implementation Plan

> Dispatched 15 auditors across 15 tasks. Focus: 100% TDD confidence that passing tests guarantee zero bugs.

## Status: AMENDED (Pass 1)

All 63 Amend Plan findings and 6 Ask User decisions have been applied to the plan document.

## Amendments Applied

| # | Finding | Task(s) | Amendment |
|---|---------|---------|-----------|
| 1 | NeedsList wrong for Collection back-references | 5 | Use `CollectionElementTypeFullName` not `TypeFullName`; added explicit note and correct code |
| 2 | Self-referencing root gets wrong NeedsList | 5 | Removed `n != rootNode` filter; self-references now count |
| 3 | FQN key format implicitly coupled | 4, 5 | Extracted `FqnHelper` shared utility class; both parsers use `FqnHelper.BuildPropertyKey` |
| 4 | Diagnostic objects break incrementality | 14 | Changed to serializable `DiagnosticInfo` record; reconstruct `Diagnostic` in RegisterSourceOutput |
| 5 | TypeGraphAnalyzer.Analyze() missing NormalizationModel param | 5, 10 | Added `NormalizationModel model` parameter; updated call site in Task 10 |
| 6 | NormalizeFqn is private in ConfigurationParser | 4, 5 | Extracted to `FqnHelper` (same as #3) |
| 7 | Intermediate compilation breakage Tasks 6-10 | 6, 10 | Added backward-compatible 3-param overload in Task 6; Task 10 removes it |
| 8 | GetHashCode order-dependent | 1 | Changed to XOR-based order-independent hashing |
| 9 | Task 2 has zero tests | 2 | Added Step 6 with ~13 unit tests across ConfigurationTests.cs and AttributeTests.cs |
| 10 | NormalizeJsonNameAttribute first attr with constructor param | 2 | Added constructor/Name property tests |
| 11 | ProcessAssignment guard blocks JsonContractBuilder | 3 | Restructured from `if != NamingBuilder` to switch statement |
| 12 | ExtractStringArgument not defined | 3 | Added Step 3 defining the helper method |
| 13 | ToDisplayString(...) has literal ellipsis | 3 | Fixed to `SymbolDisplayFormat.FullyQualifiedFormat` |
| 14 | Missing parser tests | 3 | Added 5 additional tests (empty lambda, double call, non-literal, generic type, duplicate Collection) |
| 15 | Double-processing bug in chaining | 4 | Replaced existing block; unified same-type and cross-type chain handling |
| 16 | CurrentReferenceKey corruption | 4 | Documented as known limitation; recommended fluent pattern |
| 17 | CurrentReferenceKey never declared | 4 | Added to ParseContext in Step 3 |
| 18 | Missing Task 4 tests | 4 | Added 5 additional tests |
| 19 | TypeGraphNode init setter mutation | 5 | Changed to construct new node (with expression or new instance) |
| 20 | typeSymbol vs type variable name | 5 | Added note to check existing parameter name |
| 21 | JsonNameOverride on all construction sites | 5 | Added explicit note to update all ~9 `new AnalyzedProperty` sites |
| 22 | Missing Task 5 tests | 5 | Added 4 additional tests (Collection back-ref, self-ref, FQN consistency, PropertyKinds) |
| 23 | 16 test call sites break on signature change | 6 | Backward-compatible overload handles this |
| 24 | Test nodes lack IsRootType/NeedsList | 6 | Added Step 4 to update test helpers with defaults |
| 25 | Result property type/default not specified | 6 | Added explicit test and `default!` initialization |
| 26 | EmitJsonPropertyNames=false + Result | 6 | Added test: Result [JsonPropertyName] always emitted |
| 27 | Missing Task 6 tests | 6 | Added 5 additional tests |
| 28 | HasJsonPropertyNameAttribute guard dropped | 7 | Added explicit handling: suppress source attribute when override exists |
| 29 | Missing Task 7 tests | 7 | Added 6 additional tests (Simple, Inlined, mixed, copySourceAttributes, empty string, negative) |
| 30 | WHERE result.Result is emitted | 8 | Specified: after loop, skip root inside loop |
| 31 | CreateNode helpers | 8 | Added Step 3 for test helper updates |
| 32 | Root identification strategy | 8 | Use `node.IsRootType` property |
| 33 | Existing tests that break | 8 | Added Step 3 with explicit enumeration guidance |
| 34 | Missing Task 8 test | 8 | Added circular self-ref test |
| 35 | Type mismatch in denormalizer | 9 | Fixed: change is in EmitGetCollections producing DTO arrays |
| 36 | EmitGetCollections needs rootNode | 9 | Added to Step 4 |
| 37 | rootDto vs source object type | 9 | Clarified: EmitPass1/Pass2/RootResolution unchanged |
| 38 | Existing tests that break | 9 | Added Step 3 |
| 39 | Wrong variable name nodes vs rootNodes | 10 | Added note to check actual variable name |
| 40 | Zero new tests in Task 10 | 10 | Added note explaining reliance on existing + Task 11 |
| 41 | Broken tests acknowledgment | 10 | Added Step 5 note |
| 42 | Missing JsonNamingTests.cs | 11 | Added to file list |
| 43 | 5 access patterns not just [0] | 11 | Documented all 5 patterns in Step 1 |
| 44 | Missing EmitterHelpersTests.cs | 11 | Added to file list with conditional note |
| 45 | Dual sample configs | 11 | Documented both configs in Step 4 |
| 46 | Coordinated emitter test changes | 11 | Added NeedsList table in Step 0 |
| 47 | NeedsList table for implementer | 11 | Added Step 0 with table template |
| 48 | Missing full JSON roundtrip | 12 | Added serialize→deserialize→denormalize test |
| 49 | Missing reflection check | 12 | Added container property reflection assertion |
| 50 | Missing dedup assertions | 12 | Added shared-place index equality checks |
| 51 | Missing nullable reference test | 12 | Added MarketingCarrier? null/populated test |
| 52 | Missing default "result" name test | 12 | Added Step 8 |
| 53 | Missing DataNormalizer.Runtime reference | 13 | Added to EmitterCompilationHelper |
| 54 | User-defined types don't exist in compilation | 13 | Added `AssertCompilesWithStubs` method and stub strategy |
| 55 | Incomplete BCL references | 13 | Replaced with Basic.Reference.Assemblies.Net90 |
| 56 | Basic.Reference.Assemblies recommendation | 13 | Added as Step 1 (NuGet package) |
| 57 | Missing negative test | 13 | Added Step 4 |
| 58 | Missing NullableContextOptions | 13 | Added to CSharpCompilationOptions |
| 59 | Wrong test case (n.DtoSuffix = GetSuffix()) | 14 | Removed; added note explaining why it's wrong |
| 60 | Broken diagnostic pipeline | 14 | Fixed: IIncrementalGenerator + RegisterSourceOutput |
| 61 | Wrong file path for DiagnosticDescriptors | 14 | Fixed to `Diagnostics/DiagnosticDescriptors.cs` |
| 62 | Missing try/finally | 14 | Added explicit try/finally pattern at all sites |
| 63 | Missing dotnet restore/tool restore | 15 | Added Steps 1-2 |
| 64 | Format before check | 15 | Added `dotnet csharpier .` before check |
| 65 | --no-build on test step | 15 | Changed to `--no-build` |
| 66 | Missing dotnet pack | 15 | Added Step 7 |

## User Decisions Applied

| # | Question | Decision | Applied In |
|---|----------|----------|------------|
| 1 | Duplicate Collection<T> for same type | Diagnostic error DN1002 | Tasks 3, 14 |
| 2 | RootPropertyName = "" | Fall back to "result" | Tasks 6, header |
| 3 | [NormalizeJsonName("")] | Treated as null | Tasks 5, 7, header |
| 4 | Null guard on Result | No guard needed (Accept) | N/A |
| 5 | SearchPlace-as-root test | Small dedicated graph with realistic fields | Task 12 (TransportNetwork) |
| 6 | Code coverage in final check | Yes, include coverage | Task 15 |

---

## Amend Plan (63 findings)

### Cross-Task: Architectural Issues

1. **Task 5/9: `NeedsList` computation is wrong for Collection back-references** — `p.TypeFullName` for a `List<Root>` property is the full list type, NOT the element type. The check `p.TypeFullName == rootFqn` always fails for collections. Must use `p.CollectionElementTypeFullName` (or equivalent) instead.
   - Tasks affected: 5, 8, 9, 11, 12

2. **Task 5: Self-referencing root types produce wrong `NeedsList`** — `n != rootNode` filter excludes the root node, so `TreeNode { Parent: TreeNode }` gets `NeedsList = false`. The denormalizer (Task 9) would then create `new[] { rootDto }` which breaks index-based resolution when the root appears at non-zero positions.
   - Tasks affected: 5, 8, 9

3. **Task 4/5: FQN key format implicitly coupled** — `"{typeFqn}.{propName}"` is constructed independently in ConfigurationParser (Task 4) and TypeGraphAnalyzer (Task 5) with no shared helper. Different `SymbolDisplayFormat` choices would silently cause mismatches.
   - Tasks affected: 4, 5

4. **Task 14: Storing `Diagnostic` objects in pipeline data breaks incrementality** — `Diagnostic` contains `Location` which holds `SyntaxTree` references. This violates the fundamental source generator rule: never store syntax/semantic references in pipeline data. Must store serializable diagnostic info instead (matching existing `GeneratorDiagnosticInfo` pattern).
   - Tasks affected: 14

5. **Task 5: `TypeGraphAnalyzer.Analyze()` has no `NormalizationModel` parameter** — The plan adds code reading `model.PropertyJsonNameOverrides` but the method doesn't receive a model. Signature change required.
   - Tasks affected: 5, 10

6. **Task 5: `NormalizeFqn` is `private static` in ConfigurationParser** — Inaccessible from TypeGraphAnalyzer. Must be extracted to a shared utility.
   - Tasks affected: 4, 5

7. **Tasks 6-10: Intermediate compilation breakage** — ContainerEmitter signature changes in Task 6 but NormalizeGenerator isn't updated until Task 10. Tasks 6-9 cannot compile independently. Need backward-compatible overloads or reorder tasks.
   - Tasks affected: 6, 7, 8, 9, 10

### Task 1: JsonContractModel

8. **GetHashCode is order-dependent** — `foreach` over `ImmutableDictionary` iteration order is not contractually guaranteed. Equal dictionaries could produce different hashes. Use XOR-based order-independent hashing.

### Task 2: Runtime Configuration Classes

9. **ZERO unit tests specified** — No test file listed. New classes (`JsonContractBuilder`, `ReferenceBuilder`, `NormalizeJsonNameAttribute`) and modified classes (`GraphBuilder`, `TypeBuilder`) have no tests. Must add ~13 tests matching existing patterns in `ConfigurationTests.cs` and `AttributeTests.cs`.

10. **`NormalizeJsonNameAttribute` is the first attribute with a constructor parameter** — Must test the `Name` property returns the constructor value.

### Task 3: ConfigurationParser UseJsonContract

11. **`ProcessAssignment` guard blocks new receiver kind** — Existing `if (receiverKind != ReceiverKind.NamingBuilder) return;` prevents the `JsonContractBuilder` case from being reached. Plan must restructure this guard.

12. **`ExtractStringArgument` helper not defined** — Referenced in Tasks 3 and 4 but doesn't exist. Plan must include its implementation.

13. **Code snippet has literal ellipsis** — `ToDisplayString(...)` must be `ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)`.

14. **Missing tests**: empty lambda body, UseJsonContract called twice (last-wins?), non-literal string argument, generic type argument FQN format.

### Task 4: ConfigurationParser Reference().JsonName()

15. **Double-processing bug** — Plan's Step 5 adds a new chaining block but doesn't replace the existing one. Inner invocations could be processed twice.

16. **`CurrentReferenceKey` corruption** — Single mutable field; multiple split-statement `Reference()` variables corrupt each other. `var r1 = x.Reference(A); var r2 = x.Reference(B); r1.JsonName("a")` stores wrong key.

17. **`CurrentReferenceKey` never declared** — Property not added to ParseContext in the plan text.

18. **Missing tests**: consecutive Reference() without JsonName, Reference() then Reference().JsonName() (key overwrite), JsonName(""), JsonName(variable), two ForType blocks isolation.

### Task 5: TypeGraphAnalyzer

19. **`TypeGraphNode` uses `init` setters** — Plan says "Set `rootNode.NeedsList`" post-construction, which is a compile error with init-only properties.

20. **Plan uses `typeSymbol` but parameter is named `type`** — Compile error in code snippet.

21. **`JsonNameOverride` must be added to ALL 9 `new AnalyzedProperty` construction sites** — Plan doesn't mention this.

22. **Missing tests**: Collection back-reference to root, self-referencing root, FQN consistency integration test, `[NormalizeJsonName]` on different PropertyKinds.

### Task 6: ContainerEmitter

23. **16 existing test call sites break** when signature changes from 3 to 4 params. No migration step.

24. **Test nodes lack `IsRootType`/`NeedsList`** — Test helpers need updating.

25. **Result property DTO type and default value not specified** — `default!` vs nullable unclear.

26. **`EmitJsonPropertyNames = false` + Result property** — Should Result's `[JsonPropertyName]` still be emitted when global flag is off?

27. **Missing tests**: Result property type, empty RootPropertyName, CollectionJsonNames key format match, EmitJsonPropertyNames=false interaction.

### Task 7: DtoEmitter

28. **`HasJsonPropertyNameAttribute` guard dropped without replacement** — When `copySourceAttributes=true` and property has both source `[JsonPropertyName]` AND `JsonNameOverride`, two attributes emitted = compile error.

29. **Missing tests**: Simple property with override, Inlined property with override, mixed DTO, `copySourceAttributes=true` + existing attribute + override, negative assertion that override doesn't change CLR property name.

### Task 8: NormalizerEmitter

30. **Plan doesn't describe WHERE `result.Result` is emitted** relative to existing for-loop. Must specify: skip root list inside loop, emit `result.Result` after loop.

31. **`CreateNode` test helpers need `NeedsList`/`IsRootType` parameters**.

32. **Root identification strategy inside loop** — Must specify comparison (`node.TypeFullName == rootNode.TypeFullName`).

33. **~6 existing tests will break** — Plan doesn't enumerate them.

34. **Missing test**: Circular self-referencing root with `NeedsList = true`.

### Task 9: DenormalizerEmitter

35. **Type mismatch** — `var {rootPlural} = new[] { rootDto }` conflates DTO arrays with source object arrays. Should be in `EmitGetCollections`, wrapping in a DTO array.

36. **`EmitGetCollections` needs `rootNode` parameter** — Plan doesn't mention signature change.

37. **Plan says "use rootDto instead of {rootPlural}[0]"** but `rootDto` is DTO type while return type is source object. Current code is already correct.

38. **Multiple existing tests break** — Plan doesn't enumerate.

### Task 10: NormalizeGenerator Wiring

39. **Plan snippet uses `nodes` but actual variable is `rootNodes`** — Compile error.

40. **ZERO new tests** — No E2E test exercises full JsonContract pipeline (config → parser → analyzer → generator → output).

41. **No acknowledgment of broken tests** between Tasks 6-9 and Task 11.

### Task 11: Update Existing Tests

42. **Missing file: `JsonNamingTests.cs`** — Contains 8+ assertions checking root-type JSON property names that will break.

43. **5 distinct access patterns, not just `[0]`** — `.Length` checks, `Has.Length` constraints, variable assignments, inline property access all need updating. `.Length` on a removed property = compile error.

44. **Missing file: `EmitterHelpersTests.cs`** — Tests list property name generation.

45. **Samples have dual configs** — `Program.cs` has both `SampleNormalization` and `CorporateNormalization` with root-list access.

46. **Emitter tests need coordinated changes** — Assertions depend on `NeedsList` value per test scenario.

47. **Implementer needs explicit `NeedsList` table** — Which root type in which test has `NeedsList=true` vs `false`.

### Task 12: Integration Tests

48. **Missing full JSON roundtrip** — Need serialize → deserialize → denormalize test (not just normalize + check JSON).

49. **Missing reflection check** — Verify container type literally lacks `SearchResponseDtos` property.

50. **Missing dedup assertions** — Shared `SearchPlace` instances need explicit index equality checks.

51. **Missing nullable reference test** — `MarketingCarrier?` JSON name override on nullable reference property.

52. **Missing default `"result"` name test** — No test for default when `RootPropertyName` not set.

### Task 13: Compilation Checks

53. **Missing `DataNormalizer.Runtime` assembly reference** — Generated code uses runtime types not in reference list.

54. **User-defined types don't exist in compilation** — Generated code references `TestApp.Person` etc. that don't exist. Need stub type generation strategy.

55. **Incomplete BCL references** — Missing `System.Runtime`, `System.Memory`, `System.Collections.Immutable`, `System.CodeDom`.

56. **Recommend `Basic.Reference.Assemblies.Net90` NuGet** — Eliminates ~5 findings in one step.

57. **Missing negative test** — No test validates helper catches invalid code.

58. **Missing `NullableContextOptions.Enable`** — Generated code uses `#nullable enable` but compilation doesn't set it.

### Task 14: Diagnostics

59. **Wrong test case** — `n.DtoSuffix = GetSuffix()` matches existing `AssignmentExpressionSyntax` branch, never falls to `default`. Test would pass for wrong reason.

60. **Broken diagnostic pipeline** — Plan references `NormalizeGenerator.Execute` which doesn't exist (it's `IIncrementalGenerator`). Existing `GeneratorDiagnosticInfo` doesn't support statement text/location.

61. **Wrong file path** — `DiagnosticDescriptors.cs` already exists at `Diagnostics/DiagnosticDescriptors.cs`.

62. **Missing try/finally** — `BuilderLambdaDepth` needs protection at all increment sites.

### Task 15: Final Build

63. **Missing `dotnet restore` and `dotnet tool restore`** — `--no-restore` build will fail without prior restore. CSharpier won't be found without tool restore.

64. **Step 4 contradicts Step 2** — `check` is read-only. Need `dotnet csharpier .` (format) before `dotnet csharpier check .`.

65. **Should use `--no-build` on test step** — Tests the same build artifacts.

66. **Missing `dotnet pack` verification** — Packaging can fail even when build/test pass.

---

## Ask User (6 findings)

1. **Task 3: Duplicate `Collection<T>` for same type** — Is last-wins correct, or should this be a diagnostic error?

2. **Task 6: `RootPropertyName = ""`** — Should empty string produce `[JsonPropertyName("")]` or fall back to `"result"`?

3. **Task 5: `[NormalizeJsonName("")]`** — Valid override or treated as null?

4. **Task 8/9: Null guard on `Result`** — Should generated normalizer/denormalizer add null guard for source param?

5. **Task 12: `SearchPlace`-as-root test needs second config class** — Duplicates generated DTOs. Acceptable?

6. **Task 15: Code coverage** — Should final check include coverage threshold?

---

## Accept (29 findings)

_(Informational only, no action required — omitted for brevity. See individual task audits in `docs/plans/audits/`.)_

---

## Verdict

**63 Amend Plan + 6 Ask User findings.** This plan cannot be executed as-is — the TDD approach has significant gaps and several code snippets contain bugs that would compile-fail or produce incorrect behavior.

**Most critical issues:**
1. `NeedsList` computation is fundamentally wrong for Collection back-references (always fails)
2. Self-referencing root types get wrong `NeedsList` value
3. Intermediate tasks can't compile independently (Tasks 6-10)
4. Task 2 has zero tests for new runtime classes
5. Storing `Diagnostic` objects in pipeline data breaks incrementality (Task 14)
6. Multiple code snippets have compile errors (wrong variable names, missing method signatures)

**Handing off to plan-audit-fixer to resolve.**
