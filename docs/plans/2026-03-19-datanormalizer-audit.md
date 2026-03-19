# DataNormalizer Implementation Plan - Audit Synthesis

**Plan:** `docs/plans/2026-03-19-datanormalizer-implementation.md`
**Auditors dispatched:** 3 (Tasks 1-6, Tasks 7-9, Tasks 10-13)
**Date:** 2026-03-19

---

## User Decisions

| Finding | Decision |
|---------|----------|
| .NET 10 availability | Include in v1.0 targets (net8.0;net9.0;net10.0) |
| Hash collision data loss | Use proper equality-based dedup, not hash-only |

---

## Amendments Applied

### Structural: TDD Reordering

The plan is reordered for TDD. New task order:

| Phase | Tasks | Rationale |
|-------|-------|-----------|
| 1. Scaffolding + Skills | Solution, tooling, CI/CD, **`.opencode/skills`, `AGENTS.md`** | Skills and agents guide all subsequent phases — must exist first |
| 2. Runtime (TDD) | Attributes, config types, NormalizationContext, NormalizedResult | Pure classes, fully TDD-able |
| 3. Generator analysis (snapshot TDD) | Diagnostics, type graph analyzer, config parser | Write expected output → implement to match |
| 4. Generator emitters (snapshot TDD) | DTO emitter, equality emitter, normalizer emitter, denormalizer emitter | Each independently snapshot-tested |
| 5. Pipeline (E2E TDD) | Wire generator, end-to-end generator tests | Full pipeline integration |
| 6. Integration (TDD) | Real domain types + roundtrip tests | Proves generated code works |
| 7. Docs & Samples | README, samples | Polish and documentation |

Key changes:
- **Skills and AGENTS.md moved to Phase 1** — they exist to guide the agent during development; creating them at the end defeats their purpose. The dotnet-guidelines, dotnet-tdd, source-generator-dev, and other skills must be in place before writing any C# code.
- **Type graph analyzer before config parser** (analyzer is simpler, tests Roslyn symbol APIs only; parser requires syntax tree walking).
- **Emitters split into 4 separate tasks**, each TDD'd via snapshots.

### Finding 1.2: Missing PolySharp polyfill

**Task 1 amendment:** Add `PolySharp` to `Directory.Packages.props`:
```xml
<ItemGroup Label="Polyfills">
  <PackageVersion Include="PolySharp" Version="1.14.1" />
</ItemGroup>
```
And reference in generator csproj:
```xml
<PackageReference Include="PolySharp" PrivateAssets="all" />
```
This provides `IsExternalInit`, nullable attributes, etc. for netstandard2.0 + C# 12.

### Finding 2.1/2.2: CSharpier tool manifest ordering

**Task 2 amendment:** Move dotnet tool manifest creation from Task 3 to Task 2. Remove global install. New Step 5:
```
Run: dotnet new tool-manifest
Run: dotnet tool install csharpier
Run: dotnet csharpier .
```

### Finding 3.1/3.2: CI/release SDK installation

**Task 3 amendment:** Install all SDKs in every workflow job instead of matrix:
```yaml
- name: Setup .NET
  uses: actions/setup-dotnet@v4
  with:
    dotnet-version: |
      8.0.x
      9.0.x
      10.0.x
```
Remove the matrix strategy. Single job builds/tests all TFMs.

### Finding 5.3: Missing NormalizationConfig tests

**Task 5 amendment:** Add tests:
```csharp
[Test]
public void NormalizationConfig_IsAbstract()
{
    Assert.That(typeof(NormalizationConfig).IsAbstract, Is.True);
}

[Test]
public void NormalizationConfig_HasConfigureMethod()
{
    var method = typeof(NormalizationConfig)
        .GetMethod("Configure", BindingFlags.Instance | BindingFlags.NonPublic);
    Assert.That(method, Is.Not.Null);
    Assert.That(method!.IsAbstract, Is.True);
}
```

### Finding 6.1: Hash collision data loss → Use proper equality

**Task 6 amendment:** Replace hash-only dedup with equality-based dedup. `NormalizationContext.GetOrAddIndex` signature becomes:
```csharp
public (int Index, bool IsNew) GetOrAddIndex<TDto>(string typeKey, TDto dto)
    where TDto : IEquatable<TDto>
```
Internal storage changes from `Dictionary<int, int>` to `Dictionary<string, List<(TDto Dto, int Index)>>` with hash bucketing + equality checking on collision. Or simpler: `Dictionary<string, Dictionary<TDto, int>>` using the generated `IEquatable<TDto>`.

This means the normalizer emitter must first create the DTO, then call `GetOrAddIndex` with it (rather than computing a hash first). The flow becomes:
1. Create DTO, populate simple properties
2. Call `context.GetOrAddIndex(typeKey, dto)` → returns (index, isNew)
3. If isNew: recursively normalize nested properties, store in collection
4. Either way: return the index

### Finding 6.2: Unused type parameter T

**Task 6 amendment:** Remove unused `T` from non-generic overload. The equality-based `GetOrAddIndex<TDto>` (from 6.1) naturally uses the type parameter.

### Finding 6.3: Null-padded list fragility

**Task 6 amendment:** Add `ArgumentOutOfRangeException` guard for negative indices. Add comment documenting sequential-from-0 invariant.

### Finding 6.9: API mismatch (string key vs type-based)

**Task 6 amendment:** Add convenience overload:
```csharp
public IReadOnlyList<T> GetCollection<T>() where T : class
    => GetCollection<T>(typeof(T).Name);
```
Keep string-key version as the primary internal API. Update design doc to show both.

### Finding 7.1: ConfigInfo stores SemanticModel (breaks incremental caching)

**Task 7 amendment:** `ConfigInfo` must only contain equatable primitives. Extract all data from SemanticModel within the `transform:` delegate:
```csharp
internal readonly record struct ConfigInfo(
    string ClassName,
    string FullyQualifiedName,
    string Namespace,
    bool IsPartial,
    Location Location,
    // Store the syntax reference for later retrieval, not the full node/model
    SyntaxReference? ConfigureMethodReference);
```
The actual parsing happens inside `RegisterSourceOutput` using `context.Compilation.GetSemanticModel()` to reconstruct the semantic model from the syntax reference. This is the standard pattern for incremental generators.

### Finding 8.1: Parser API misaligned with generator

**Task 8 amendment:** Change `ConfigurationParser.Parse` signature to accept an already-identified class:
```csharp
internal static NormalizationModel Parse(
    ClassDeclarationSyntax configClass,
    SemanticModel semanticModel)
```
Remove internal config class discovery (generator already found it).

### Finding 8.2: Parenthesized lambdas silently ignored

**Task 8 amendment:** Handle both lambda forms:
```csharp
var lambda = invocation.ArgumentList.Arguments
    .Select(static a => a.Expression)
    .FirstOrDefault(static e =>
        e is SimpleLambdaExpressionSyntax or ParenthesizedLambdaExpressionSyntax);

var body = lambda switch
{
    SimpleLambdaExpressionSyntax simple => simple.Body,
    ParenthesizedLambdaExpressionSyntax paren => paren.Body,
    _ => null
};
```

### Finding 8.3: Local variable patterns silently ignored

**Task 8 amendment:** Add `LocalDeclarationStatementSyntax` handling in `ParseMethodBody`. Track variable-to-builder mappings. When encountering `var graph = builder.NormalizeGraph<T>()`, record that variable `graph` corresponds to a `GraphBuilder` for that root type. Subsequent `graph.Inline<X>()` calls are then correctly associated.

### Finding 8.5: Chained fluent calls broken

**Task 8 amendment:** Reverse chain processing. For a chain like `builder.ForType<Person>().IgnoreProperty(x => x.Name)`, walk to the innermost invocation first, create the `TypeConfiguration`, then process outward:
```csharp
private static void ParseInvocationChain(
    InvocationExpressionSyntax outermost,
    SemanticModel semanticModel,
    NormalizationModel model)
{
    // Collect all invocations in the chain (outermost → innermost)
    var chain = new List<InvocationExpressionSyntax>();
    var current = outermost;
    while (current is not null)
    {
        chain.Add(current);
        current = (current.Expression as MemberAccessExpressionSyntax)
            ?.Expression as InvocationExpressionSyntax;
    }
    // Process innermost first (reverse)
    chain.Reverse();
    TypeConfiguration? context = null;
    foreach (var invocation in chain)
    {
        ParseInvocation(invocation, semanticModel, model, ref context);
    }
}
```

### Finding 8.7: InlinedTypes uses short name

**Task 8 amendment:** Use `inlinedType.ToDisplayString()` when adding to `InlinedTypes`. Compare against `namedType.ToDisplayString()` in the analyzer.

### Finding 8.8: Missing tests for chained calls + local variables

**Task 8 amendment:** Add concrete tests for:
- Chained: `builder.ForType<Person>().IgnoreProperty(x => x.Name).IgnoreProperty(x => x.Age)`
- Local variable: `var graph = builder.NormalizeGraph<Person>(); graph.Inline<Metadata>();`
- Parenthesized lambda: `builder.NormalizeGraph<Person>((graph) => { graph.Inline<Metadata>(); })`

### Finding 9.2: Auto-discovers in explicit ForType mode

**Task 9 amendment:** Add `bool autoDiscover` parameter to `TypeGraphAnalyzer.Analyze()`. When `false` (root came from `ForType`), only recurse into types listed in `model.ExplicitTypes` or explicitly `NormalizedProperties`.

### Finding 9.3: Collection detection incomplete

**Task 9 amendment:** Replace string prefix matching with interface check:
```csharp
private static bool TryGetCollectionElementType(ITypeSymbol type, out ITypeSymbol? elementType)
{
    elementType = null;
    if (type is IArrayTypeSymbol arrayType)
    {
        elementType = arrayType.ElementType;
        return true;
    }
    // Check all interfaces for IEnumerable<T>
    foreach (var iface in type.AllInterfaces)
    {
        if (iface.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T
            && iface.TypeArguments.Length == 1)
        {
            elementType = iface.TypeArguments[0];
            return true;
        }
    }
    return false;
}
```

### Finding 9.4: IsSimpleType missing enums and Nullable<T>

**Task 9 amendment:** Add to `IsSimpleType`:
```csharp
if (type.TypeKind == TypeKind.Enum) return true;
if (type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T
    && type is INamedTypeSymbol nullable)
{
    return IsSimpleType(nullable.TypeArguments[0]);
}
```

### Finding 9.5: Remove unused SemanticModel parameter

**Task 9 amendment:** Remove `SemanticModel` from `Analyze()` signature.

### Finding 9.6: GetMembers misses inherited properties

**Task 9 amendment:** Walk type hierarchy:
```csharp
private static IEnumerable<IPropertySymbol> GetAllPublicProperties(INamedTypeSymbol type)
{
    var current = type;
    var seen = new HashSet<string>();
    while (current is not null && current.SpecialType != SpecialType.System_Object)
    {
        foreach (var member in current.GetMembers().OfType<IPropertySymbol>())
        {
            if (member.DeclaredAccessibility == Accessibility.Public
                && !member.IsStatic && !member.IsIndexer
                && seen.Add(member.Name))
            {
                yield return member;
            }
        }
        current = current.BaseType;
    }
}
```

### Finding 9.8: Tests are comments only

**Task 9 amendment:** Provide concrete test implementations for: simple flat type, nested complex type, self-referential type, List<T> collection, enum property, Nullable<int> property, inherited properties.

### Finding 10.1: Null-unsafe GetHashCode + nullable indices

**Task 10 amendment:** Emitter generates null-safe hashing:
```csharp
hashCode = (hashCode * 397) ^ (Name?.GetHashCode() ?? 0);
```
For nullable source properties (e.g., `Address? WorkAddress`), generate `int? WorkAddressIndex` (nullable int). `null` means "no object", not an index lookup.

### Finding 10.2: Array equality

**Task 10 amendment:** Emitter generates for `int[]` properties:
- Equals: `PhoneNumberIndices.AsSpan().SequenceEqual(other.PhoneNumberIndices)`
- GetHashCode: iterate and combine each element

### Finding 10.4: Missing [GeneratedCode] attribute

**Task 10 amendment:** Emitter produces `[System.CodeDom.Compiler.GeneratedCode("DataNormalizer", "1.0.0")]` on every generated class.

### Finding 11.2: Denormalizer underspecified for collections

**Task 13 (denormalizer emitter) amendment:** Specify collection reconstruction:
- `List<T>` source → create `new List<T>()`, iterate index array, resolve each, add
- `T[]` source → create `new T[indices.Length]`, iterate and resolve
- `IReadOnlyList<T>` / `ICollection<T>` → create `List<T>` and assign

### Finding 11.3: Nullable property handling

**Task 12 (normalizer emitter) amendment:** When source property is null:
- Don't call `GetOrAddIndex`
- Set `dto.WorkAddressIndex = null` (for `int?`)
- Denormalizer: if index is null, set source property to null

### Finding 12.1/12.2: Pipeline architecture

**Task 14 (wire pipeline) amendment:** Restructure the pipeline:
1. `ForAttributeWithMetadataName` extracts only equatable data (strings, SyntaxReference) in `transform`
2. Combine with `context.CompilationProvider` using `.Combine()`
3. In `RegisterSourceOutput`, reconstruct SemanticModel from SyntaxReference + Compilation
4. Call parser → analyzer → emitters inside the output callback

### Finding 12.3: Multiple root types sharing types

**Task 14 amendment:** Deduplicate DTO emission. Track emitted types in a `HashSet<string>`. If two graphs discover the same type, emit the DTO only once. File naming: `Normalized{TypeName}.g.cs`. Normalize methods are overloads on the config class.

### Finding 12.4: Compilation verification

**Task 14 amendment:** End-to-end tests verify generated code compiles:
```csharp
var newCompilation = driver.RunGenerators(compilation).Compilation;
var errors = newCompilation.GetDiagnostics()
    .Where(d => d.Severity == DiagnosticSeverity.Error);
Assert.That(errors, Is.Empty);
```

### Findings 13.1-13.6: Integration test gaps

**Task 15 (integration tests) amendment:**
- Use type-based `GetCollection<T>()` overload (add convenience overload per 6.9)
- Add value-equality dedup test (different references, same values)
- Provide concrete circular reference test code
- Create a `DeepEqualityComparer<T>` test utility (reflection-based)
- Add null property normalization/denormalization tests
- Add empty collection test

---

## Summary

| Action | Count | Status |
|--------|-------|--------|
| Amend Plan | 22 | Applied |
| Ask User | 2 | Resolved (user decided) |
| Accept | 28 | No action needed |

The plan requires rewriting with TDD ordering and all amendments applied before execution.
