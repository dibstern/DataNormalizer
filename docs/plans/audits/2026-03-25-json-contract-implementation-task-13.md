# Task 13: Add compilation checks to emitter unit tests -- Audit Report

**Summary:** The task's core concept is sound -- compiling emitter output catches real bugs that string-matching misses. However, the proposed `EmitterCompilationHelper` has several critical gaps: it omits multiple required assembly references, doesn't account for user-defined types (TestApp.Person, TestApp.Address, etc.) that the generated code references, and the normalizer/denormalizer output cannot compile standalone because it references `DataNormalizer.Runtime.NormalizationContext`. The plan also underspecifies which tests get compilation assertions and whether cross-file compilation is needed.

---

## Findings

| # | Category | Action | Issue | File:Line | Amendment / Question |
|---|----------|--------|-------|-----------|----------------------|
| 1 | Incorrect Code | **Amend Plan** | **Missing `DataNormalizer.Runtime` assembly reference.** The NormalizerEmitter generates code referencing `DataNormalizer.Runtime.NormalizationContext` (NormalizerEmitter.cs:79, :110). The compilation helper must include a MetadataReference for the DataNormalizer runtime assembly. The test project already has `<ProjectReference Include="..\..\src\DataNormalizer\DataNormalizer.csproj" />`, so `typeof(DataNormalizer.Runtime.NormalizationContext).Assembly.Location` should work. | NormalizerEmitter.cs:79,110 | Add `MetadataReference.CreateFromFile(typeof(DataNormalizer.Runtime.NormalizationContext).Assembly.Location)` to the References array. |
| 2 | Incorrect Code | **Amend Plan** | **Generated code references user-defined types that don't exist.** Every emitter test uses fake types like `TestApp.Person`, `TestApp.Address`, `TestApp.Metadata`, `TestApp.OrderStatus`, etc. The generated DTO code references these types (e.g., `public TestApp.Metadata Meta { get; set; }` for inlined props, `public TestApp.OrderStatus Status { get; set; }` for enum props). The container emitter references DTO types from other files. The normalizer references source types AND DTO types. None of these types exist in the compilation. The helper must either: (a) generate stub type declarations for all referenced user types, or (b) only compile tests that use primitive-only types, or (c) accept that most tests cannot have compilation added. Without stubs, almost every test will fail with CS0234/CS0246 errors. | DtoEmitterTests.cs:86-91, NormalizerEmitterTests.cs:26 | Add a mechanism to the helper to generate stub types. For example, accept a list of stub source strings: `AssertCompiles(result, "namespace TestApp { public class Person { public string Name {get;set;} public int Age {get;set;} } }")`. Alternatively, have the helper auto-generate empty stubs for unresolved types. The plan must specify the approach. |
| 3 | Incorrect Code | **Amend Plan** | **Missing `System.Runtime` assembly reference.** On modern .NET (net9.0), `typeof(object).Assembly.Location` points to `System.Private.CoreLib.dll`, not `System.Runtime.dll`. Roslyn compilation requires explicit references to `System.Runtime` for fundamental type forwarding. Without it, even `object` may not resolve. Standard pattern is to use `MetadataReference.CreateFromFile(Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location)!, "System.Runtime.dll"))`. | Plan Step 1 code snippet | Add System.Runtime.dll reference. Also add `System.Collections.dll` and `System.Linq.dll` (for `SequenceEqual` in DTO Equals). |
| 4 | Incorrect Code | **Amend Plan** | **Missing `System.Memory` / `System.Runtime.InteropServices` reference for `MemoryExtensions`.** DtoEmitter.cs:181 generates `System.MemoryExtensions.SequenceEqual(System.MemoryExtensions.AsSpan(...))` for collection equality. This requires a reference to the assembly containing `System.MemoryExtensions` (typically `System.Memory.dll` or `System.Runtime.dll` depending on TFM). Without it, any DTO with collection properties will fail compilation. | DtoEmitter.cs:181 | Add MetadataReference for the assembly containing `System.MemoryExtensions`. On net9.0, this is in `System.Memory.dll`. |
| 5 | Incorrect Code | **Amend Plan** | **Missing `System.Collections.Immutable` reference.** DenormalizerEmitter generates code using `System.Collections.Immutable.ImmutableList.CreateBuilder<T>()` and `System.Collections.Immutable.ImmutableArray.CreateBuilder<T>()` (DenormalizerEmitter.cs:318, :336). Tests that exercise ImmutableList/ImmutableArray collection kinds need this assembly reference. | DenormalizerEmitter.cs:318,336 | Add `MetadataReference.CreateFromFile(typeof(System.Collections.Immutable.ImmutableArray).Assembly.Location)` to the References array. |
| 6 | Implicit Assumptions | **Amend Plan** | **`System.Text.Json` is not an explicit package dependency.** The plan assumes `typeof(System.Text.Json.Serialization.JsonPropertyNameAttribute).Assembly.Location` will resolve. This works on net9.0 (framework includes it), but only if the test project's build includes the assembly in its output. Since there's no explicit `System.Text.Json` PackageReference and the test project targets net9.0, this should work via the framework reference, but this is an implicit dependency worth documenting. If the runtime library ever drops to netstandard2.0 or below, this breaks. | Plan Step 1 code snippet | Note in the helper that System.Text.Json is resolved from the framework. Add a comment explaining this assumption. |
| 7 | Implicit Assumptions | **Amend Plan** | **Normalizer/Denormalizer output requires source types to compile, not just DTO types.** The normalizer generates `public static TestApp.PersonResultDto Normalize(TestApp.Person source)` -- it takes the original source type as a parameter and accesses its properties (`source.Name`, `source.HomeAddress`, etc.). The denormalizer generates `new TestApp.Person()` and sets properties on it. Compilation requires full stub types with matching properties, not just empty stubs. This is a significantly larger scope than implied by the plan's simple `AssertCompiles(result)` call. | NormalizerEmitter.cs:77, DenormalizerEmitter.cs:114-117 | The plan should specify that normalizer/denormalizer compilation tests need comprehensive source type stubs with correct properties, or limit compilation checks to DtoEmitter and ContainerEmitter output only (which reference fewer external types). |
| 8 | Fragile Code | **Amend Plan** | **Assembly.Location can return empty string in single-file publish or trimmed apps.** While this won't affect standard `dotnet test`, it's a known fragility. The plan should use the `AppContext.BaseDirectory` + known DLL names pattern or the `Basic.Reference.Assemblies` NuGet package (provides net6.0/net7.0/net8.0/net9.0 reference assemblies as embedded resources) for a more robust approach. | Plan Step 1 code snippet | Consider using `Basic.Reference.Assemblies.Net90` NuGet package, which provides all needed references as `ReferenceAssemblies.Net90.References.All`. This eliminates all assembly-location fragility and missing-reference issues in one stroke. Alternatively, document the `.Assembly.Location` approach and enumerate all required assemblies explicitly. |
| 9 | Fragile Code | **Amend Plan** | **Hardcoded reference list becomes a maintenance trap.** Every time the emitters change to reference a new BCL type (e.g., `System.Collections.Immutable`, `System.Text.RegularExpressions`, etc.), the reference list must be updated or tests silently fail. The plan doesn't address how to maintain this. | Plan Step 1 code snippet | Either (a) use `Basic.Reference.Assemblies` for a complete set, or (b) add a comment in the helper documenting what each reference is for and when to add new ones, or (c) load all assemblies from the runtime directory (`AppContext.BaseDirectory` or `RuntimeEnvironment.GetRuntimeDirectory()`). |
| 10 | Implicit Assumptions | **Ask User** | **Scope ambiguity: "main" test case vs all tests.** The plan says "For each emitter's 'main' test case, add a call to `EmitterCompilationHelper.AssertCompiles(result)`." It's unclear which tests qualify as "main." DtoEmitterTests has 23 test methods. ContainerEmitterTests has 14. NormalizerEmitterTests has 16. DenormalizerEmitterTests has 14. Should compilation assertions be added to all of them, only the first test in each file, or only specific representative tests? | Plan Step 2 | Clarify: should EVERY test get an `AssertCompiles` call, or only a subset? If a subset, which ones? Note that adding to all tests significantly increases test execution time (Roslyn compilation is not cheap). |
| 11 | Implicit Assumptions | **Amend Plan** | **Plan says "modify all emitter test files" but does NOT mention `EmitterHelpersTests.cs`.** The file list says to modify "all emitter test files" but only lists DtoEmitterTests, ContainerEmitterTests, NormalizerEmitterTests, DenormalizerEmitterTests. `EmitterHelpersTests.cs` is also in the Emitters directory but tests helper methods (GetDtoName, GetListPropertyName), not code generation. It should be explicitly excluded to avoid confusion. | Plan file list | Add explicit note: "EmitterHelpersTests.cs is excluded -- it tests naming logic, not code generation." |
| 12 | Implicit Assumptions | **Amend Plan** | **ContainerEmitter.Emit signature changes in Task 6 but plan snippet uses old signature.** Task 6 changes ContainerEmitter.Emit to take a 4th parameter `JsonContractModel jsonContract`. But the Task 13 cross-file example on line 773 shows `ContainerEmitter.Emit(personNode, allNodes, naming, jsonContract)` -- this correctly uses the new signature, but the plan doesn't mention that Task 13 depends on Task 6 being complete. Since Task 13 comes after Task 6 in the plan, this should be fine sequentially, but deserves explicit acknowledgment. | Plan line 773 | Add note that this task assumes Tasks 6-9 are already complete (emitter signatures have changed). |
| 13 | Incorrect Code | **Amend Plan** | **Missing `System.CodeDom` assembly reference.** Generated DTOs use `[System.CodeDom.Compiler.GeneratedCode(...)]` attribute (DtoEmitter.cs:24, ContainerEmitter.cs:26). On net9.0, this type is in `System.CodeDom.dll` which must be explicitly referenced in the Roslyn compilation. | DtoEmitter.cs:24 | Add `MetadataReference.CreateFromFile(typeof(System.CodeDom.Compiler.GeneratedCodeAttribute).Assembly.Location)` to the References array. |
| 14 | Incorrect Code | **Amend Plan** | **Missing negative test to validate the helper itself.** The plan mentions compilation checks but doesn't include a test that verifies the helper correctly CATCHES invalid code. Without this, a bug in the helper (e.g., missing references causing false positives, or accidentally ignoring errors) could make all checks meaningless. | Plan | Add a test: emit deliberately invalid C# (e.g., `"public class Broken { public UnknownType Foo { get; set; } }"`), call `AssertCompiles`, and verify it THROWS/FAILS. This validates the helper works. |
| 15 | Implicit Assumptions | **Amend Plan** | **`CSharpCompilationOptions` may need `NullableContextOptions.Enable`.** Generated code uses `#nullable enable` pragma. While this is a per-file pragma that should work regardless of compilation options, some nullable diagnostics may differ based on the compilation-level setting. The plan should set `NullableContextOptions = NullableContextOptions.Enable` in the compilation options for consistency. | Plan Step 1 code snippet | Add `.WithNullableContextOptions(NullableContextOptions.Enable)` to `CSharpCompilationOptions`. |

---

## Detailed Analysis

### Critical: User-Defined Type Stubs (Finding #2 + #7)

This is the most significant gap. Let me trace through what happens when you try to compile DtoEmitter output for the simplest test case (`Emit_SimpleFlatType`):

The emitter produces:
```csharp
#nullable enable
namespace TestApp;
[System.CodeDom.Compiler.GeneratedCode("DataNormalizer", "1.0.0")]
public partial class PersonDto : System.IEquatable<PersonDto>
{
    public string Name { get; set; } = default!;
    public int Age { get; set; }
    // ... Equals, GetHashCode
}
```

This **should** compile with just BCL references (no user types needed for simple props). But the moment you test `InlinedProp("Meta", "TestApp.Metadata")`, the output includes `public TestApp.Metadata Meta { get; set; }` -- this requires a `TestApp.Metadata` type to exist.

For NormalizerEmitter, **every** test requires source types because the generated code takes source types as parameters and accesses their properties.

### Critical: Assembly Reference Completeness (Findings #1, #3, #4, #5, #13)

The generated code references at least these BCL types/namespaces:
- `System.IEquatable<T>` -- System.Runtime.dll
- `System.CodeDom.Compiler.GeneratedCodeAttribute` -- System.CodeDom.dll (or System.Runtime on some TFMs)
- `System.Text.Json.Serialization.JsonPropertyNameAttribute` -- System.Text.Json.dll
- `System.MemoryExtensions.SequenceEqual` / `AsSpan` -- System.Memory.dll
- `System.Array.Empty<T>` -- System.Runtime.dll
- `System.Collections.Generic.List<T>` -- System.Collections.dll
- `System.Collections.Generic.EqualityComparer<T>` -- System.Runtime.dll
- `System.Collections.Immutable.*` -- System.Collections.Immutable.dll
- `DataNormalizer.Runtime.NormalizationContext` -- DataNormalizer.dll

The plan's snippet only lists `typeof(object).Assembly.Location` and `System.Text.Json`. At minimum 5-6 additional references are needed.

### Recommendation: Use `Basic.Reference.Assemblies` NuGet Package

The cleanest solution to findings #3, #4, #5, #8, #9, and #13 is to add the `Basic.Reference.Assemblies.Net90` NuGet package to the test project. This provides all .NET 9.0 reference assemblies as a single `ReferenceAssemblies.Net90.References.All` collection, eliminating all assembly-path fragility and missing-reference issues. Only the DataNormalizer.Runtime assembly would need to be added separately.

---

**No issues found in:** (none -- all investigated categories had findings)
