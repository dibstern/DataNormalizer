# DataNormalizer v1.0 Implementation Plan

> **For Agent:** REQUIRED SUB-SKILL: Use executing-plans to implement this plan task-by-task.

**Goal:** Build a .NET source generator NuGet package that normalizes nested object graphs into flat, deduplicated representations using compile-time code generation.

**Architecture:** Pure source generator approach. A Roslyn IIncrementalGenerator analyzes fluent configuration (NormalizationConfig subclasses) to discover type graphs, then emits normalized DTO classes, IEquatable implementations, and Normalize/Denormalize methods. Zero runtime dependencies. The generator is bundled inside the DataNormalizer NuGet package.

**Tech Stack:** C# 12, .NET 8/9/10, netstandard2.0 (generator), Roslyn Microsoft.CodeAnalysis, NUnit 4, Verify.SourceGenerators, CSharpier, GitHub Actions, Central Package Management.

**Design Doc:** `docs/plans/2026-03-19-datanormalizer-design.md`
**Audit Doc:** `docs/plans/2026-03-19-datanormalizer-audit.md`

---

## Phase 1: Scaffolding + Skills

Skills and AGENTS.md are created first so they guide every subsequent phase.

### Task 1: Create Solution and Project Structure

**Files:**
- Create: `DataNormalizer.sln`
- Create: `src/DataNormalizer/DataNormalizer.csproj`
- Create: `src/DataNormalizer.Generators/DataNormalizer.Generators.csproj`
- Create: `tests/DataNormalizer.Tests/DataNormalizer.Tests.csproj`
- Create: `tests/DataNormalizer.Generators.Tests/DataNormalizer.Generators.Tests.csproj`
- Create: `tests/DataNormalizer.Integration.Tests/DataNormalizer.Integration.Tests.csproj`
- Create: `samples/DataNormalizer.Samples/DataNormalizer.Samples.csproj`
- Create: `Directory.Build.props`
- Create: `Directory.Packages.props`

**Step 1: Create the solution file**

Run: `dotnet new sln -n DataNormalizer`

**Step 2: Create Directory.Build.props**

```xml
<Project>
  <PropertyGroup>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <LangVersion>12.0</LangVersion>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
  </PropertyGroup>
</Project>
```

**Step 3: Create Directory.Packages.props**

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
    <CentralPackageTransitivePinningEnabled>true</CentralPackageTransitivePinningEnabled>
  </PropertyGroup>

  <ItemGroup Label="Code Generation">
    <PackageVersion Include="Microsoft.CodeAnalysis.CSharp" Version="4.12.0" />
    <PackageVersion Include="Microsoft.CodeAnalysis.Analyzers" Version="3.3.4" />
  </ItemGroup>

  <ItemGroup Label="Polyfills">
    <PackageVersion Include="PolySharp" Version="1.14.1" />
  </ItemGroup>

  <ItemGroup Label="Testing">
    <PackageVersion Include="NUnit" Version="4.3.2" />
    <PackageVersion Include="NUnit3TestAdapter" Version="4.6.0" />
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
    <PackageVersion Include="Verify.NUnit" Version="28.5.0" />
    <PackageVersion Include="Verify.SourceGenerators" Version="2.5.0" />
  </ItemGroup>
</Project>
```

Note: Verify exact latest package versions against NuGet at implementation time.

**Step 4: Create the runtime library project**

```xml
<!-- src/DataNormalizer/DataNormalizer.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFrameworks>net8.0;net9.0;net10.0</TargetFrameworks>
    <PackageId>DataNormalizer</PackageId>
    <Description>Source generator that normalizes nested object graphs into flat, deduplicated representations.</Description>
    <Authors>David Stern</Authors>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <PackageTags>normalization;source-generator;dto;deduplication;serialization</PackageTags>
    <PackageReadmeFile>README.md</PackageReadmeFile>
    <PublishRepositoryUrl>true</PublishRepositoryUrl>
    <EmbedUntrackedSources>true</EmbedUntrackedSources>
    <IncludeSymbols>true</IncludeSymbols>
    <SymbolPackageFormat>snupkg</SymbolPackageFormat>
    <Deterministic>true</Deterministic>
    <ContinuousIntegrationBuild Condition="'$(CI)' == 'true'">true</ContinuousIntegrationBuild>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\DataNormalizer.Generators\DataNormalizer.Generators.csproj"
                      OutputItemType="Analyzer"
                      ReferenceOutputAssembly="false" />
  </ItemGroup>

  <ItemGroup>
    <None Include="..\DataNormalizer.Generators\bin\$(Configuration)\netstandard2.0\DataNormalizer.Generators.dll"
          Pack="true" PackagePath="analyzers/dotnet/cs" Visible="false" />
    <None Include="..\..\README.md" Pack="true" PackagePath="\" />
  </ItemGroup>
</Project>
```

Note: The generator DLL path assumes `dotnet build` runs before `dotnet pack`. The release workflow always builds first.

**Step 5: Create the source generator project**

```xml
<!-- src/DataNormalizer.Generators/DataNormalizer.Generators.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <LangVersion>12.0</LangVersion>
    <Nullable>enable</Nullable>
    <EnforceExtendedAnalyzerRules>true</EnforceExtendedAnalyzerRules>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp" PrivateAssets="all" />
    <PackageReference Include="Microsoft.CodeAnalysis.Analyzers" PrivateAssets="all" />
    <PackageReference Include="PolySharp" PrivateAssets="all" />
  </ItemGroup>
</Project>
```

PolySharp provides `IsExternalInit`, nullable attributes, and other polyfills for C# 12 on netstandard2.0.

**Step 6: Create test projects**

```xml
<!-- tests/DataNormalizer.Tests/DataNormalizer.Tests.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="NUnit" />
    <PackageReference Include="NUnit3TestAdapter" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\DataNormalizer\DataNormalizer.csproj" />
  </ItemGroup>
</Project>
```

```xml
<!-- tests/DataNormalizer.Generators.Tests/DataNormalizer.Generators.Tests.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="NUnit" />
    <PackageReference Include="NUnit3TestAdapter" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="Verify.NUnit" />
    <PackageReference Include="Verify.SourceGenerators" />
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\DataNormalizer.Generators\DataNormalizer.Generators.csproj" />
    <ProjectReference Include="..\..\src\DataNormalizer\DataNormalizer.csproj" />
  </ItemGroup>
</Project>
```

```xml
<!-- tests/DataNormalizer.Integration.Tests/DataNormalizer.Integration.Tests.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="NUnit" />
    <PackageReference Include="NUnit3TestAdapter" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\DataNormalizer\DataNormalizer.csproj" />
  </ItemGroup>
</Project>
```

```xml
<!-- samples/DataNormalizer.Samples/DataNormalizer.Samples.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\DataNormalizer\DataNormalizer.csproj" />
  </ItemGroup>
</Project>
```

**Step 7: Add all projects to the solution**

Run:
```bash
dotnet sln add src/DataNormalizer/DataNormalizer.csproj
dotnet sln add src/DataNormalizer.Generators/DataNormalizer.Generators.csproj
dotnet sln add tests/DataNormalizer.Tests/DataNormalizer.Tests.csproj
dotnet sln add tests/DataNormalizer.Generators.Tests/DataNormalizer.Generators.Tests.csproj
dotnet sln add tests/DataNormalizer.Integration.Tests/DataNormalizer.Integration.Tests.csproj
dotnet sln add samples/DataNormalizer.Samples/DataNormalizer.Samples.csproj
```

**Step 8: Verify the solution builds**

Run: `dotnet build`
Expected: Build succeeded with 0 errors.

**Step 9: Commit**

```bash
git add -A && git commit -m "feat: scaffold solution with project structure, CPM, and build config"
```

---

### Task 2: Tooling Configuration

**Files:**
- Create: `.editorconfig`
- Create: `.csharpierrc.yaml`
- Create: `.gitignore`
- Create: `LICENSE`
- Create: `.config/dotnet-tools.json` (via tool manifest)

**Step 1: Create .editorconfig**

```ini
root = true

[*]
indent_style = space
indent_size = 4
end_of_line = lf
charset = utf-8
trim_trailing_whitespace = true
insert_final_newline = true

[*.{csproj,props,targets,xml}]
indent_size = 2

[*.{json,yml,yaml}]
indent_size = 2

[*.cs]
csharp_style_var_when_type_is_apparent = true:suggestion
csharp_style_var_for_built_in_types = true:suggestion
csharp_style_var_elsewhere = true:suggestion
csharp_style_prefer_primary_constructors = true:suggestion
csharp_style_namespace_declarations = file_scoped:warning
csharp_style_prefer_pattern_matching = true:suggestion
csharp_style_prefer_switch_expression = true:suggestion
```

**Step 2: Create .csharpierrc.yaml**

```yaml
printWidth: 120
```

**Step 3: Create .gitignore**

Run: `dotnet new gitignore`

**Step 4: Create LICENSE**

MIT license, author "David Stern", year 2026.

**Step 5: Create tool manifest and install CSharpier**

Run: `dotnet new tool-manifest && dotnet tool install csharpier`

**Step 6: Run formatter**

Run: `dotnet csharpier .`

**Step 7: Commit**

```bash
git add -A && git commit -m "chore: add editorconfig, csharpier, gitignore, and MIT license"
```

---

### Task 3: GitHub Actions CI/CD

**Files:**
- Create: `.github/workflows/ci.yml`
- Create: `.github/workflows/release.yml`

**Step 1: Create CI workflow**

All SDKs installed in a single job (no matrix) to support multi-TFM build:

```yaml
name: CI
on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

jobs:
  build-and-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: |
            8.0.x
            9.0.x
            10.0.x

      - name: Restore
        run: dotnet restore

      - name: Build
        run: dotnet build --no-restore

      - name: Check formatting
        run: dotnet tool restore && dotnet csharpier --check .

      - name: Test
        run: dotnet test --no-build --logger "trx;LogFileName=results.trx"

      - name: Upload test results
        uses: actions/upload-artifact@v4
        if: always()
        with:
          name: test-results
          path: '**/results.trx'
```

**Step 2: Create release workflow**

```yaml
name: Release
on:
  push:
    tags: ['v*']

jobs:
  release:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: |
            8.0.x
            9.0.x
            10.0.x

      - name: Extract version from tag
        id: version
        run: echo "VERSION=${GITHUB_REF_NAME#v}" >> $GITHUB_OUTPUT

      - name: Restore
        run: dotnet restore

      - name: Build
        run: dotnet build --no-restore -c Release

      - name: Test
        run: dotnet test --no-build -c Release

      - name: Pack
        run: dotnet pack src/DataNormalizer/DataNormalizer.csproj -c Release --no-build /p:Version=${{ steps.version.outputs.VERSION }} -o ./nupkgs

      - name: Push to NuGet
        run: dotnet nuget push ./nupkgs/*.nupkg --api-key ${{ secrets.NUGET_API_KEY }} --source https://api.nuget.org/v3/index.json

      - name: Create GitHub Release
        uses: softprops/action-gh-release@v2
        with:
          generate_release_notes: true
          files: ./nupkgs/*

      - name: Update major version tag
        uses: nowactions/update-majorver@v1
```

**Step 3: Commit**

```bash
git add -A && git commit -m "ci: add GitHub Actions for CI and NuGet release"
```

---

### Task 4: Create .opencode/skills Suite

**Files:**
- Create: `.opencode/skills/dotnet-guidelines/SKILL.md`
- Create: `.opencode/skills/dotnet-patterns/SKILL.md`
- Create: `.opencode/skills/dotnet-architecture/SKILL.md`
- Create: `.opencode/skills/dotnet-backend-patterns/SKILL.md`
- Create: `.opencode/skills/ddd-patterns/SKILL.md`
- Create: `.opencode/skills/dotnet-tdd/SKILL.md`
- Create: `.opencode/skills/centralized-packages/SKILL.md`
- Create: `.opencode/skills/nuget-packaging/SKILL.md`
- Create: `.opencode/skills/csharpier/SKILL.md`
- Create: `.opencode/skills/source-generator-dev/SKILL.md`
- Create: `.opencode/skills/legacy-normalizer/SKILL.md`
- Create: `.opencode/skills/testcontainers/SKILL.md`
- Create: `.opencode/skills/dotnet-versions/SKILL.md`
- Create: `.opencode/agents/AGENTS.md`

**Step 1: Create each skill file**

Each SKILL.md follows the standard skill format with YAML frontmatter (`name`, `description`), rules, code examples, and common pitfalls. Content is drawn from:

- **dotnet-guidelines**: Modern C# standards from the skill.fish references (primary constructors, records, sealed, var, collection initializers, static lambdas, nullable, file-scoped namespaces). Adapted to remove opinions that conflict with this project (e.g., we DO use `_` prefix for private fields in the generator project since it targets netstandard2.0 without primary constructors).
- **dotnet-patterns**: C# patterns from `c-net-development-patterns` (records, pattern matching, nullable, async, LINQ, DI, generics, error handling).
- **dotnet-architecture**: Clean Architecture and MSBuild standards from `net-architecture-linter` (CPM rules, project structure).
- **dotnet-backend-patterns**: DI patterns, IOptions, Result pattern from `net-backend-patterns-1`. Adapted for NUnit 4 (not xUnit).
- **ddd-patterns**: Aggregate roots, entities, repositories, CQRS from `ddd-patterns-for-net`.
- **dotnet-tdd**: Red-Green-Refactor, AAA pattern, test doubles from `net-tdd-workflow`. Adapted for NUnit 4 (`[Test]`/`[TestCase]` instead of `[Fact]`/`[Theory]`, `Assert.That` syntax).
- **centralized-packages**: Directory.Packages.props management from `centralized-net-package-management`.
- **nuget-packaging**: NuGet library best practices — packaging source generators as analyzers, .csproj metadata, SourceLink, deterministic builds, README in package, semantic versioning.
- **csharpier**: CSharpier formatting rules, `.csharpierrc.yaml` config, CI integration via `dotnet csharpier --check`.
- **source-generator-dev**: Writing Roslyn IIncrementalGenerators, `ForAttributeWithMetadataName`, incremental caching (never store SemanticModel/SyntaxNode in pipeline), Verify.SourceGenerators snapshot testing, debugging generators, common pitfalls.
- **legacy-normalizer**: Documents the original expression tree implementation from `data-normalization/`: type graph DFS traversal, `(hash * 397) ^ value` rolling hash, deferred serialization queue for circular refs, expression tree compilation + caching, NormalizerFactory + NormalizationContext pattern. This is reference material for understanding the design lineage.
- **testcontainers**: .NET Testcontainers patterns for integration tests.
- **dotnet-versions**: Multi-targeting .NET 8/9/10, netstandard2.0 for generators, conditional compilation `#if NET8_0_OR_GREATER`, TFM-specific APIs.

**Step 2: Create AGENTS.md**

```markdown
# DataNormalizer - Agent Context

## Project Overview
DataNormalizer is a .NET source generator NuGet package that normalizes nested
object graphs into flat, deduplicated representations.

## Repository Structure
- `src/DataNormalizer/` - Runtime library (net8.0;net9.0;net10.0)
- `src/DataNormalizer.Generators/` - Roslyn source generator (netstandard2.0)
- `tests/DataNormalizer.Tests/` - Runtime unit tests (NUnit 4)
- `tests/DataNormalizer.Generators.Tests/` - Generator snapshot tests (Verify)
- `tests/DataNormalizer.Integration.Tests/` - End-to-end tests
- `samples/DataNormalizer.Samples/` - Example usage
- `docs/plans/` - Design and implementation plans

## Build & Test Commands
- Build: `dotnet build`
- Test all: `dotnet test`
- Format check: `dotnet tool restore && dotnet csharpier --check .`
- Format fix: `dotnet csharpier .`
- Pack: `dotnet pack src/DataNormalizer/DataNormalizer.csproj -c Release`

## Key Architectural Decisions
1. Pure source generator (no runtime reflection/expression trees)
2. Fluent configuration API parsed syntactically by the generator
3. Zero runtime dependencies
4. Equality-based deduplication (not hash-only) for correctness
5. Two-pass denormalization for circular reference support
6. Central Package Management (Directory.Packages.props)
7. CSharpier for formatting
8. NUnit 4 for all tests
9. Incremental generator (never store SemanticModel in pipeline)

## Skills Available
Check `.opencode/skills/` for domain-specific guidance.
```

**Step 3: Verify skill files have valid YAML frontmatter**

**Step 4: Commit**

```bash
git add -A && git commit -m "feat: add .opencode/skills suite (13 skills) and AGENTS.md"
```

---

## Phase 2: Runtime Library (TDD)

Each task follows RED-GREEN-REFACTOR: write failing test, implement minimally, refactor.

### Task 5: Attribute Definitions

**Files:**
- Create: `src/DataNormalizer/Attributes/NormalizeConfigurationAttribute.cs`
- Create: `src/DataNormalizer/Attributes/NormalizeIgnoreAttribute.cs`
- Create: `src/DataNormalizer/Attributes/NormalizeIncludeAttribute.cs`
- Create: `src/DataNormalizer/Configuration/PropertyMode.cs`
- Test: `tests/DataNormalizer.Tests/Attributes/AttributeTests.cs`

**Step 1: Write failing tests** — verify `AttributeUsage` targets, `Inherited = false`, `AllowMultiple = false`, `PropertyMode` enum values.

**Step 2: Run tests, verify RED**

Run: `dotnet test tests/DataNormalizer.Tests`
Expected: FAIL (types don't exist yet)

**Step 3: Implement attributes**

```csharp
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class NormalizeConfigurationAttribute : Attribute;

[AttributeUsage(AttributeTargets.Property)]
public sealed class NormalizeIgnoreAttribute : Attribute;

[AttributeUsage(AttributeTargets.Property)]
public sealed class NormalizeIncludeAttribute : Attribute;

public enum PropertyMode { IncludeAll = 0, ExplicitOnly = 1 }
```

**Step 4: Run tests, verify GREEN**

**Step 5: Commit**

```bash
git add -A && git commit -m "feat: add attribute definitions and PropertyMode enum"
```

---

### Task 6: Configuration Base Types

**Files:**
- Create: `src/DataNormalizer/Configuration/NormalizationConfig.cs`
- Create: `src/DataNormalizer/Configuration/NormalizeBuilder.cs`
- Create: `src/DataNormalizer/Configuration/GraphBuilder.cs`
- Create: `src/DataNormalizer/Configuration/TypeBuilder.cs`
- Test: `tests/DataNormalizer.Tests/Configuration/ConfigurationTests.cs`

These types form the fluent API. Users call them but the source generator reads their syntax. Runtime behavior is intentionally a no-op (the generator reads the syntax tree, not runtime state).

**Step 1: Write failing tests** — verify API shape: `NormalizationConfig` is abstract with `Configure(NormalizeBuilder)`, `NormalizeBuilder.NormalizeGraph<T>()` returns `GraphBuilder`, `ForType<T>()` returns `TypeBuilder<T>`, builder methods return self for chaining, `GraphBuilder.CopySourceAttributes()`, `GraphBuilder.UseJsonNaming()`, `TypeBuilder.IgnoreProperty()`, `.NormalizeProperty()`, `.InlineProperty()`, `.IncludeProperty()`, `.UsePropertyMode()`, `.WithName()`.

**Step 2: Run tests, verify RED**

**Step 3: Implement** — all builder methods are no-op stubs returning `this` or `new T()`. `NormalizationConfig` is abstract with `protected abstract void Configure(NormalizeBuilder builder)`. `GraphBuilder.UseJsonNaming` accepts `System.Text.Json.JsonNamingPolicy` (inbox for net8.0+).

**Step 4: Run tests, verify GREEN**

**Step 5: Commit**

```bash
git add -A && git commit -m "feat: add fluent configuration API"
```

---

### Task 7: NormalizationContext

**Files:**
- Create: `src/DataNormalizer/Runtime/NormalizationContext.cs`
- Test: `tests/DataNormalizer.Tests/Runtime/NormalizationContextTests.cs`

**Critical audit fix (6.1):** Uses equality-based dedup, not hash-only. The `GetOrAddIndex` method accepts a DTO implementing `IEquatable<TDto>` and uses it for collision-safe deduplication.

**Step 1: Write failing tests**

```csharp
[TestFixture]
public sealed class NormalizationContextTests
{
    [Test]
    public void GetOrAddIndex_FirstObject_ReturnsZeroAndIsNew()
    {
        var ctx = new NormalizationContext();
        var dto = new TestDto("Alice", 30);
        var (index, isNew) = ctx.GetOrAddIndex("person", dto);
        Assert.That(index, Is.EqualTo(0));
        Assert.That(isNew, Is.True);
    }

    [Test]
    public void GetOrAddIndex_EqualObject_ReturnsSameIndexAndNotNew()
    {
        var ctx = new NormalizationContext();
        var dto1 = new TestDto("Alice", 30);
        var dto2 = new TestDto("Alice", 30); // different reference, same value
        ctx.GetOrAddIndex("person", dto1);
        var (index, isNew) = ctx.GetOrAddIndex("person", dto2);
        Assert.That(index, Is.EqualTo(0));
        Assert.That(isNew, Is.False);
    }

    [Test]
    public void GetOrAddIndex_DifferentObject_ReturnsDifferentIndex()
    {
        var ctx = new NormalizationContext();
        ctx.GetOrAddIndex("person", new TestDto("Alice", 30));
        var (index, _) = ctx.GetOrAddIndex("person", new TestDto("Bob", 25));
        Assert.That(index, Is.EqualTo(1));
    }

    [Test]
    public void GetOrAddIndex_DifferentTypeKeys_TrackSeparately()
    {
        var ctx = new NormalizationContext();
        var (i1, _) = ctx.GetOrAddIndex("person", new TestDto("Alice", 30));
        var (i2, _) = ctx.GetOrAddIndex("address", new TestDto("Alice", 30));
        Assert.That(i1, Is.EqualTo(0));
        Assert.That(i2, Is.EqualTo(0)); // separate namespace
    }

    [Test]
    public void AddToCollection_StoresAndRetrieves()
    {
        var ctx = new NormalizationContext();
        var obj = new TestDto("Alice", 30);
        ctx.AddToCollection("person", 0, obj);
        var collection = ctx.GetCollection<TestDto>("person");
        Assert.That(collection, Has.Count.EqualTo(1));
        Assert.That(collection[0], Is.SameAs(obj));
    }

    [Test]
    public void GetCollection_TypeOverload_DerivesKeyFromTypeName()
    {
        var ctx = new NormalizationContext();
        ctx.AddToCollection("TestDto", 0, new TestDto("Alice", 30));
        var collection = ctx.GetCollection<TestDto>();
        Assert.That(collection, Has.Count.EqualTo(1));
    }

    [Test]
    public void GetCollection_EmptyType_ReturnsEmpty()
    {
        var ctx = new NormalizationContext();
        Assert.That(ctx.GetCollection<TestDto>("person"), Is.Empty);
    }

    [Test]
    public void AddToCollection_NegativeIndex_Throws()
    {
        var ctx = new NormalizationContext();
        Assert.That(() => ctx.AddToCollection("person", -1, new TestDto("A", 1)),
            Throws.InstanceOf<ArgumentOutOfRangeException>());
    }

    [Test]
    public void IsVisited_NewObject_ReturnsFalse()
    {
        var ctx = new NormalizationContext();
        Assert.That(ctx.IsVisited(new object()), Is.False);
    }

    [Test]
    public void MarkVisited_ThenIsVisited_ReturnsTrue()
    {
        var ctx = new NormalizationContext();
        var obj = new object();
        ctx.MarkVisited(obj);
        Assert.That(ctx.IsVisited(obj), Is.True);
    }

    private sealed record TestDto(string Name, int Age) : IEquatable<TestDto>;
}
```

**Step 2: Run tests, verify RED**

**Step 3: Implement NormalizationContext**

Key design: `GetOrAddIndex<TDto>` uses `Dictionary<string, Dictionary<TDto, int>>` for equality-based dedup where `TDto : IEquatable<TDto>`. The visited set uses `ReferenceEqualityComparer.Instance` for cycle detection. `AddToCollection` guards against negative indices.

```csharp
public sealed class NormalizationContext
{
    private readonly Dictionary<string, object> _indexMaps = new(); // typeKey -> Dictionary<TDto, int>
    private readonly Dictionary<string, List<object>> _collections = new();
    private readonly HashSet<object> _visited = new(ReferenceEqualityComparer.Instance);

    public (int Index, bool IsNew) GetOrAddIndex<TDto>(string typeKey, TDto dto)
        where TDto : IEquatable<TDto>
    {
        if (!_indexMaps.TryGetValue(typeKey, out var mapObj))
        {
            mapObj = new Dictionary<TDto, int>();
            _indexMaps[typeKey] = mapObj;
        }
        var map = (Dictionary<TDto, int>)mapObj;
        if (map.TryGetValue(dto, out var existingIndex))
            return (existingIndex, false);
        var newIndex = map.Count;
        map[dto] = newIndex;
        return (newIndex, true);
    }

    public void AddToCollection(string typeKey, int index, object obj)
    {
        if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));
        if (!_collections.TryGetValue(typeKey, out var list))
        {
            list = new List<object>();
            _collections[typeKey] = list;
        }
        while (list.Count <= index) list.Add(null!);
        list[index] = obj;
    }

    public IReadOnlyList<T> GetCollection<T>(string typeKey) where T : class
        => _collections.TryGetValue(typeKey, out var list)
            ? list.Cast<T>().ToList() : Array.Empty<T>();

    public IReadOnlyList<T> GetCollection<T>() where T : class
        => GetCollection<T>(typeof(T).Name);

    public IEnumerable<string> CollectionNames => _collections.Keys;
    public bool IsVisited(object obj) => _visited.Contains(obj);
    public void MarkVisited(object obj) => _visited.Add(obj);
}
```

**Step 4: Run tests, verify GREEN**

**Step 5: Commit**

```bash
git add -A && git commit -m "feat: add NormalizationContext with equality-based dedup"
```

---

### Task 8: NormalizedResult

**Files:**
- Create: `src/DataNormalizer/Runtime/NormalizedResult.cs`
- Test: `tests/DataNormalizer.Tests/Runtime/NormalizedResultTests.cs`

**Step 1: Write failing tests** — `Root` returns provided root, `GetCollection<T>(typeKey)` returns stored objects, `GetCollection<T>()` type-based overload, empty collection returns empty list, `Resolve<T>(typeKey, index)` returns correct object, invalid index throws `ArgumentOutOfRangeException`, `CollectionNames` returns all keys.

**Step 2: Run tests, verify RED**

**Step 3: Implement**

```csharp
public sealed class NormalizedResult<TRoot>
{
    private readonly NormalizationContext _context;

    public NormalizedResult(TRoot root, NormalizationContext context)
    {
        Root = root;
        _context = context;
    }

    public TRoot Root { get; }

    public IReadOnlyList<T> GetCollection<T>(string typeKey) where T : class
        => _context.GetCollection<T>(typeKey);

    public IReadOnlyList<T> GetCollection<T>() where T : class
        => _context.GetCollection<T>();

    public T Resolve<T>(string typeKey, int index) where T : class
    {
        var collection = _context.GetCollection<T>(typeKey);
        if (index < 0 || index >= collection.Count)
            throw new ArgumentOutOfRangeException(nameof(index),
                $"Index {index} out of range for '{typeKey}' ({collection.Count} items).");
        return collection[index];
    }

    public IEnumerable<string> CollectionNames => _context.CollectionNames;
}
```

**Step 4: Run tests, verify GREEN**

**Step 5: Commit**

```bash
git add -A && git commit -m "feat: add NormalizedResult runtime type"
```

---

## Phase 3: Generator Analysis (Snapshot TDD)

Each task: write expected output (snapshot or assertions) first, then implement to match.

### Task 9: Diagnostic Descriptors + Generator Entry Point

**Files:**
- Create: `src/DataNormalizer.Generators/Diagnostics/DiagnosticDescriptors.cs`
- Create: `src/DataNormalizer.Generators/NormalizeGenerator.cs`
- Test: `tests/DataNormalizer.Generators.Tests/GeneratorTests.cs`

**Step 1: Write failing tests**

- `Generator_WithNoConfiguration_ProducesNoOutput` — no diagnostics, no generated trees
- `Generator_WithNonPartialConfigClass_ProducesDN0002Error` — emits DN0002

**Step 2: Run tests, verify RED**

**Step 3: Implement diagnostics (DN0001 through DN0004) and generator entry point**

Generator uses `ForAttributeWithMetadataName` to find `[NormalizeConfiguration]` classes. **Critical:** `ConfigInfo` only stores equatable primitives (strings, bool, `SyntaxReference?`), never `SemanticModel` or `SyntaxNode`:

```csharp
internal readonly record struct ConfigInfo(
    string ClassName,
    string FullyQualifiedName,
    string Namespace,
    bool IsPartial,
    Location Location,
    SyntaxReference? ConfigureMethodReference);
```

Actual parsing happens inside `RegisterSourceOutput` using `context.Compilation.GetSemanticModel(syntaxRef.SyntaxTree)`.

**Step 4: Run tests, verify GREEN**

**Step 5: Commit**

```bash
git add -A && git commit -m "feat: add generator entry point with incremental pipeline and DN0002 diagnostic"
```

---

### Task 10: Type Graph Analyzer

**Files:**
- Create: `src/DataNormalizer.Generators/Analysis/TypeGraphAnalyzer.cs`
- Create: `src/DataNormalizer.Generators/Models/TypeGraphNode.cs`
- Create: `src/DataNormalizer.Generators/Models/AnalyzedProperty.cs`
- Test: `tests/DataNormalizer.Generators.Tests/Analysis/TypeGraphAnalyzerTests.cs`

**Step 1: Write failing tests** — concrete test implementations for:

- Simple flat type (all primitives) → all properties `PropertyKind.Simple`
- Enum property → `PropertyKind.Simple`
- `Nullable<int>` property → `PropertyKind.Simple`
- Nested complex type → property `PropertyKind.Normalized`, child appears before parent in result (DFS post-order)
- `List<ChildType>` → `IsCollection = true`, `CollectionElementType` set
- `ImmutableList<T>`, `IReadOnlyList<T>` → detected as collections
- Self-referential type (`TreeNode.Parent: TreeNode`) → `IsCircularReference = true`, no infinite loop
- Inherited properties from base class → included in analysis
- Auto-discover mode vs explicit-only mode (respects `model.ExplicitTypes`)
- Inlined types → `PropertyKind.Inlined`

**Step 2: Run tests, verify RED**

**Step 3: Implement TypeGraphAnalyzer**

Key fixes from audit:
- `IsSimpleType` handles enums (`type.TypeKind == TypeKind.Enum`) and `Nullable<T>` (unwrap and check inner type)
- `TryGetCollectionElementType` checks `type.AllInterfaces` for `IEnumerable<T>` instead of string prefix matching
- `GetAllPublicProperties` walks the type hierarchy (`.BaseType`) to include inherited properties
- `Analyze` signature: `(INamedTypeSymbol rootType, NormalizationModel model)` — no SemanticModel parameter
- Respects `autoDiscover` flag: when false, only recurses into types in `model.ExplicitTypes`

**Step 4: Run tests, verify GREEN**

**Step 5: Commit**

```bash
git add -A && git commit -m "feat: add type graph analyzer with DFS, cycle detection, and inherited property support"
```

---

### Task 11: Configuration Parser

**Files:**
- Create: `src/DataNormalizer.Generators/Analysis/ConfigurationParser.cs`
- Create: `src/DataNormalizer.Generators/Models/NormalizationModel.cs`
- Create: `src/DataNormalizer.Generators/Models/TypeConfiguration.cs`
- Test: `tests/DataNormalizer.Generators.Tests/Analysis/ConfigurationParserTests.cs`

**Step 1: Write failing tests** — concrete tests for:

- `builder.NormalizeGraph<Person>()` → root type extracted, auto-discover true
- `builder.ForType<Person>()` → explicit type extracted
- `graph.Inline<Metadata>()` → type added to inlined set (fully qualified name)
- `p.IgnoreProperty(x => x.InternalId)` → property in ignored set
- `graph.CopySourceAttributes()` → flag set
- **Chained calls:** `builder.ForType<Person>().IgnoreProperty(x => x.Name).IgnoreProperty(x => x.Age)` → both properties ignored
- **Local variable pattern:** `var graph = builder.NormalizeGraph<Person>(); graph.Inline<Metadata>();` → correctly associated
- **Parenthesized lambda:** `builder.NormalizeGraph<Person>((graph) => { graph.Inline<Metadata>(); })` → works same as simple lambda

**Step 2: Run tests, verify RED**

**Step 3: Implement ConfigurationParser**

Key fixes from audit:
- `Parse(ClassDeclarationSyntax configClass, SemanticModel semanticModel)` — accepts already-identified class, no internal discovery
- Handles both `SimpleLambdaExpressionSyntax` and `ParenthesizedLambdaExpressionSyntax`
- Handles `LocalDeclarationStatementSyntax` — tracks variable-to-builder mappings
- Chain processing: walks to innermost invocation first, creates TypeConfiguration, then processes outward
- `InlinedTypes` uses fully qualified type names (`ToDisplayString()`)

**Step 4: Run tests, verify GREEN**

**Step 5: Commit**

```bash
git add -A && git commit -m "feat: add configuration parser with chain, local variable, and parenthesized lambda support"
```

---

## Phase 4: Generator Emitters (Snapshot TDD)

Each emitter is independently snapshot-tested. Write the `.verified.cs` expected output first (RED), then implement the emitter to match (GREEN).

### Task 12: DTO Emitter

**Files:**
- Create: `src/DataNormalizer.Generators/Emitters/DtoEmitter.cs`
- Test: `tests/DataNormalizer.Generators.Tests/Emitters/DtoEmitterTests.cs`

**Step 1: Write snapshot tests** — expected generated code for:

- Simple type (all primitives) → same properties, `partial class` with `IEquatable<T>`, `[GeneratedCode]` attribute
- Nested normalizable type → `int HomeAddressIndex` property
- Nullable nested type → `int? WorkAddressIndex` property
- Collection of normalizable type → `int[] PhoneNumberIndices` property
- Inlined complex type → keeps original type
- Mixed properties → correct combination

**Step 2: Run snapshot tests, verify RED (no verified files yet)**

**Step 3: Implement DtoEmitter**

Key audit fixes:
- Emits `[System.CodeDom.Compiler.GeneratedCode("DataNormalizer", "1.0.0")]`
- Nullable source properties produce `int?` index properties
- `GetHashCode`: null-safe — `(hash * 397) ^ (Name?.GetHashCode() ?? 0)`
- `Equals`: array properties use `SequenceEqual` (or loop), not `==`

**Step 4: Accept verified snapshots, run tests, verify GREEN**

**Step 5: Commit**

```bash
git add -A && git commit -m "feat: add DTO emitter with IEquatable, nullable indices, and null-safe hashing"
```

---

### Task 13: Normalizer Emitter

**Files:**
- Create: `src/DataNormalizer.Generators/Emitters/NormalizerEmitter.cs`
- Test: `tests/DataNormalizer.Generators.Tests/Emitters/NormalizerEmitterTests.cs`

Generates the static `Normalize(TSource source)` method and per-type normalize helpers.

**Step 1: Write snapshot tests** — expected generated `Normalize` method for:

- Simple flat type (no nested objects)
- Nested normalizable type (hashes, dedup via `GetOrAddIndex<TDto>(typeKey, dto)`, stores index)
- Nullable nested property (null check → set `int? Index = null`)
- Collection of normalizable type (iterate, normalize each, produce `int[]`)
- Circular reference (visited check via `context.IsVisited`/`MarkVisited`)

**Step 2: Run tests, verify RED**

**Step 3: Implement NormalizerEmitter**

Flow per object:
1. Check `context.IsVisited(source)` for cycles
2. `context.MarkVisited(source)`
3. Create DTO, populate simple properties
4. For nullable nested: null check, skip if null
5. Recursively normalize nested objects
6. Call `context.GetOrAddIndex(typeKey, dto)` for dedup
7. If new: `context.AddToCollection(typeKey, index, dto)`
8. Return index

**Step 4: Run tests, verify GREEN**

**Step 5: Commit**

```bash
git add -A && git commit -m "feat: add normalizer emitter with equality-based dedup and cycle handling"
```

---

### Task 14: Denormalizer Emitter

**Files:**
- Create: `src/DataNormalizer.Generators/Emitters/DenormalizerEmitter.cs`
- Test: `tests/DataNormalizer.Generators.Tests/Emitters/DenormalizerEmitterTests.cs`

Generates the static `Denormalize(NormalizedResult<TDto> result)` method.

**Step 1: Write snapshot tests** — expected generated `Denormalize` for:

- Simple type roundtrip
- Nested objects (resolve indices)
- Nullable properties (null index → null property)
- Collections (`int[]` → `List<T>`, `T[]`, etc.)
- Circular references (two-pass: create all objects first, then resolve references)

**Step 2: Run tests, verify RED**

**Step 3: Implement DenormalizerEmitter**

Two-pass approach:
- Pass 1: Create all source objects from DTOs, populate simple properties
- Pass 2: Resolve all index references to actual object references

Collection reconstruction:
- `List<T>` source → `new List<T>()`, iterate index array, resolve each, add
- `T[]` source → `new T[indices.Length]`, iterate and resolve
- `IReadOnlyList<T>` / `ICollection<T>` → create `List<T>` and assign

Nullable handling: if index is `null`, set source property to `null`.

**Step 4: Run tests, verify GREEN**

**Step 5: Commit**

```bash
git add -A && git commit -m "feat: add denormalizer emitter with two-pass reconstruction and collection support"
```

---

## Phase 5: Pipeline Wiring (E2E TDD)

### Task 15: Wire Complete Generator Pipeline

**Files:**
- Modify: `src/DataNormalizer.Generators/NormalizeGenerator.cs`
- Test: `tests/DataNormalizer.Generators.Tests/GeneratorEndToEndTests.cs`

**Step 1: Write E2E tests** that feed complete source code through the generator:

- Simple auto-discover: `Person` with `Address` → `NormalizedPerson`, `NormalizedAddress` generated
- With opt-out: `Metadata` inlined
- With ignored property
- Circular reference: `TreeNode` with `Parent` → DN0001 warning, correct output
- Multiple root types → shared types emitted once
- Non-partial class → DN0002 error
- Verify generated code compiles: `newCompilation.GetDiagnostics().Where(d => d.Severity == Error)` is empty

**Step 2: Run tests, verify RED**

**Step 3: Wire the pipeline in NormalizeGenerator**

Inside `RegisterSourceOutput` (combined with `CompilationProvider`):
1. Reconstruct `SemanticModel` from `ConfigInfo.ConfigureMethodReference` + `Compilation`
2. `ConfigurationParser.Parse(configClass, semanticModel)` → `NormalizationModel`
3. For each root type: `TypeGraphAnalyzer.Analyze(rootType, model)` → `List<TypeGraphNode>`
4. Deduplicate across multiple graphs (`HashSet<string>` of emitted types)
5. For each unique node: `DtoEmitter.Emit(node)` → source, `context.AddSource("Normalized{TypeName}.g.cs", source)`
6. `NormalizerEmitter.Emit(model, nodes)` + `DenormalizerEmitter.Emit(model, nodes)` → config partial source
7. Report diagnostics (DN0001 for cycles, DN0004 for inlined complex types)

**Step 4: Run tests, iterate until GREEN**

**Step 5: Run all tests across all projects**

Run: `dotnet test`
Expected: All pass

**Step 6: Commit**

```bash
git add -A && git commit -m "feat: wire complete generator pipeline (parse → analyze → emit)"
```

---

## Phase 6: Integration Tests (TDD)

### Task 16: Integration Tests with Real Types

**Files:**
- Create: `tests/DataNormalizer.Integration.Tests/TestTypes/Person.cs`
- Create: `tests/DataNormalizer.Integration.Tests/TestTypes/Address.cs`
- Create: `tests/DataNormalizer.Integration.Tests/TestTypes/PhoneNumber.cs`
- Create: `tests/DataNormalizer.Integration.Tests/TestTypes/TreeNode.cs`
- Create: `tests/DataNormalizer.Integration.Tests/TestTypes/TestNormalization.cs`
- Create: `tests/DataNormalizer.Integration.Tests/TestUtils/DeepEqualityComparer.cs`
- Create: `tests/DataNormalizer.Integration.Tests/SimpleNormalizationTests.cs`
- Create: `tests/DataNormalizer.Integration.Tests/CollectionNormalizationTests.cs`
- Create: `tests/DataNormalizer.Integration.Tests/CircularReferenceTests.cs`
- Create: `tests/DataNormalizer.Integration.Tests/RoundtripTests.cs`

The integration test project has the source generator active via ProjectReference to DataNormalizer. Tests use *real* generated code.

**Step 1: Create test domain types and normalization config**

```csharp
public sealed class Person
{
    public string Name { get; set; } = "";
    public int Age { get; set; }
    public Address HomeAddress { get; set; } = new();
    public Address? WorkAddress { get; set; }
    public List<PhoneNumber> PhoneNumbers { get; set; } = new();
}

public sealed class TreeNode
{
    public string Label { get; set; } = "";
    public TreeNode? Parent { get; set; }
    public List<TreeNode> Children { get; set; } = new();
}

[NormalizeConfiguration]
public partial class TestNormalization : NormalizationConfig
{
    protected override void Configure(NormalizeBuilder builder)
    {
        builder.NormalizeGraph<Person>();
        builder.NormalizeGraph<TreeNode>();
    }
}
```

**Step 2: Create DeepEqualityComparer<T>** — reflection-based recursive property comparison for roundtrip tests.

**Step 3: Write integration tests**

Must include:
- Simple normalization (name, age preserved)
- **Value-equality dedup** (two different `Address` instances with same values → one entry)
- Reference-equality dedup (same Address reference used twice → one entry, same index)
- Different addresses → two entries, different indices
- **Null property** normalization (`WorkAddress = null` → `WorkAddressIndex = null`)
- **Empty collection** (`PhoneNumbers = new()` → `PhoneNumberIndices` is empty array)
- Collection with duplicates → deduplication
- Circular reference: parent-child cycle → doesn't infinite loop, produces valid result
- Tree structure: 3+ levels → all nodes normalized
- **Roundtrip**: Normalize → Denormalize → deep-equals original (simple, shared addresses, nullable, circular)

**Step 4: Run integration tests**

Run: `dotnet test tests/DataNormalizer.Integration.Tests`
Expected: All PASS

**Step 5: Run all tests**

Run: `dotnet test`
Expected: All pass across all projects

**Step 6: Commit**

```bash
git add -A && git commit -m "test: add integration tests for normalization, dedup, nulls, collections, cycles, and roundtrips"
```

---

## Phase 7: Docs & Samples

### Task 17: Sample Application

**Files:**
- Create: `samples/DataNormalizer.Samples/Program.cs`
- Create: `samples/DataNormalizer.Samples/Models/` (sample domain types)
- Create: `samples/DataNormalizer.Samples/SampleNormalization.cs`

**Step 1: Create sample domain model** — e-commerce: `Order`, `Customer`, `Product`, `Address`.

**Step 2: Create normalization config** — demonstrate auto-discovery, opt-out, custom equality.

**Step 3: Write Program.cs** — create objects with shared references, normalize, inspect flat collections, denormalize, verify roundtrip.

**Step 4: Verify**

Run: `dotnet run --project samples/DataNormalizer.Samples`
Expected: Prints normalization results showing dedup

**Step 5: Commit**

```bash
git add -A && git commit -m "docs: add sample project demonstrating DataNormalizer usage"
```

---

### Task 18: README and NuGet Package Finalization

**Files:**
- Create/Update: `README.md`

**Step 1: Write README** — what it does (before/after example), installation, quick start (3 steps), configuration options, serialization attributes, circular reference support, API reference.

**Step 2: Verify package**

Run: `dotnet pack src/DataNormalizer/DataNormalizer.csproj -c Release`
Expected: .nupkg created with lib/net8.0, lib/net9.0, lib/net10.0, analyzers/dotnet/cs/DataNormalizer.Generators.dll, README.md

**Step 3: Commit**

```bash
git add -A && git commit -m "docs: add README with usage examples and API reference"
```

---

### Task 19: Final Verification

**Step 1: Clean build**

Run: `dotnet clean && dotnet build`
Expected: 0 errors

**Step 2: All tests**

Run: `dotnet test --verbosity normal`
Expected: All pass

**Step 3: Format check**

Run: `dotnet tool restore && dotnet csharpier --check .`
Expected: All formatted

**Step 4: Package**

Run: `dotnet pack src/DataNormalizer/DataNormalizer.csproj -c Release`
Expected: Success

**Step 5: Sample runs**

Run: `dotnet run --project samples/DataNormalizer.Samples`
Expected: No errors

**Step 6: Final commit if needed**

```bash
git add -A && git commit -m "chore: final formatting and cleanup"
```

---

## Task Dependency Graph

```
Task 1 (Solution)
├── Task 2 (Tooling)
├── Task 3 (CI/CD)
└── Task 4 (Skills + AGENTS.md)
     └── Task 5 (Attributes)
          └── Task 6 (Config types)
               ├── Task 7 (NormalizationContext)
               │    └── Task 8 (NormalizedResult)
               └─────────┐
                    Task 9 (Generator entry point)
                         ├── Task 10 (Type graph analyzer)
                         └── Task 11 (Config parser)
                              └── Task 12 (DTO emitter)
                                   └── Task 13 (Normalizer emitter)
                                        └── Task 14 (Denormalizer emitter)
                                             └── Task 15 (Wire pipeline)
                                                  └── Task 16 (Integration tests)
                                                       ├── Task 17 (Samples)
                                                       └── Task 18 (README)
                                                            └── Task 19 (Final verification)
```

Tasks 2, 3, 4 can be parallelized after Task 1.
Tasks 7 and 8 can be parallelized.
Tasks 17 and 18 can be parallelized after Task 16.
