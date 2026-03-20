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
- `Generator_WithNoPublicProperties_ProducesDN0003Error` — type with zero public properties emits DN0003 **(audit amendment)**

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
- **`string` property → `PropertyKind.Simple`, NOT detected as `IEnumerable<char>` collection (audit amendment)**
- **`byte[]` property → `PropertyKind.Simple`, NOT detected as normalizable collection (audit amendment)**
- **`Dictionary<K,V>` property → `PropertyKind.Simple` or inlined, NOT a normalizable collection (audit amendment)**
- Nested complex type → property `PropertyKind.Normalized`, child appears before parent in result (DFS post-order)
- **Multi-level nesting (A → B → C) → all three types in graph, correct DFS order (audit amendment)**
- `List<ChildType>` → `IsCollection = true`, `CollectionElementType` set
- `ImmutableList<T>`, `IReadOnlyList<T>` → detected as collections
- **`T[]` array of complex type → `IsCollection = true`, `CollectionElementType` set (audit amendment)**
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
- `graph.UseJsonNaming(JsonNamingPolicy.CamelCase)` → naming policy recorded **(audit amendment)**
- `graph.ForType<Person>(p => { p.IgnoreProperty(x => x.InternalId); })` → nested ForType on GraphBuilder **(audit amendment)**
- **Chained calls:** `builder.ForType<Person>().IgnoreProperty(x => x.Name).IgnoreProperty(x => x.Age)` → both properties ignored
- **Local variable pattern:** `var graph = builder.NormalizeGraph<Person>(); graph.Inline<Metadata>();` → correctly associated
- **Parenthesized lambda:** `builder.NormalizeGraph<Person>((graph) => { graph.Inline<Metadata>(); })` → works same as simple lambda
- **Empty Configure body:** `protected override void Configure(NormalizeBuilder builder) { }` → empty model, no errors **(audit amendment)**
- **Multiple NormalizeGraph calls:** `builder.NormalizeGraph<Person>(); builder.NormalizeGraph<Order>();` → both root types captured **(audit amendment)**

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
- **DateTime/Guid property** — verifies well-known type handling in generated code **(audit amendment 3)**
- **Enum property** — verifies enum type reference in generated DTO **(audit amendment 3)**
- **Null-safe Equals/GetHashCode** — uses `IsReferenceType` flag: ref types get `?.GetHashCode() ?? 0`, value types get `.GetHashCode()` directly. Verify no compiler warnings with TreatWarningsAsErrors **(audit amendment 3)**
- **Correct namespace/usings in generated file** — snapshot includes `namespace`, `using` directives **(audit amendment 3)**
- **Property order preserved** — generated DTO properties match source type order **(audit amendment 3)**

**Step 2: Run snapshot tests, verify RED (no verified files yet)**

**Step 3: Implement DtoEmitter**

Key audit fixes:
- Emits `[System.CodeDom.Compiler.GeneratedCode("DataNormalizer", "1.0.0")]`
- Nullable source properties produce `int?` index properties
- `GetHashCode`: null-safe — uses `AnalyzedProperty.IsReferenceType`: ref types get `(Name?.GetHashCode() ?? 0)`, value types get `Age.GetHashCode()` directly **(audit amendment 3)**
- `Equals`: array properties use `SequenceEqual` (or loop), not `==`
- Uses `NormalizationModel.ConfigClassName`/`ConfigNamespace` for generated partial class **(audit amendment 3)**

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
- **Multi-level nesting (A → B → C) → correct recursive normalization (audit amendment)**
- Nullable nested property (null check → set `int? Index = null`)
- **Null collection property** (`List<T>? Phones = null` → empty `int[]` or null, defined behavior) **(audit amendment)**
- **Empty collection** (`List<T> Phones = new()` → `int[]` with length 0) **(audit amendment)**
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
- **`T[]` source property → snapshot for `new T[indices.Length]` reconstruction (audit amendment)**
- **`IReadOnlyList<T>` source property → snapshot for `List<T>` construction assigned to interface (audit amendment)**
- Circular references (two-pass: create all objects first, then resolve references)

**Step 2: Run tests, verify RED**

**Step 3: Implement DenormalizerEmitter**

Two-pass approach:
- Pass 1: Create all source objects from DTOs, populate simple properties
- Pass 2: Resolve all index references to actual object references

Collection reconstruction (uses `AnalyzedProperty.CollectionKind` to determine pattern) **(audit amendment 3)**:
- `CollectionTypeKind.List` → `new List<T>()`, iterate index array, resolve each, add
- `CollectionTypeKind.Array` → `new T[indices.Length]`, iterate and resolve
- `CollectionTypeKind.IReadOnlyList` / `CollectionTypeKind.ICollection` → create `List<T>` and assign to interface
- `CollectionTypeKind.HashSet` → `new HashSet<T>()`, iterate and add
- `CollectionTypeKind.ImmutableList` / `CollectionTypeKind.ImmutableArray` → build via `ImmutableList.CreateBuilder<T>()`

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
- **Unmapped complex type → DN0004 info diagnostic (audit amendment)**
- **Multiple config classes in same assembly → both generate code correctly (audit amendment)**
- **Config class with empty Configure body → no generated output, no errors (audit amendment)**
- **Cross-namespace: domain types in `MyApp.Models`, config in `MyApp.Config` → correct usings/FQN in generated code (audit amendment 3)**
- **Conflicting short type names in different namespaces → generated code uses FQN to disambiguate (audit amendment 3)**
- **Multiple root types → overloaded `Normalize()`/`Denormalize()` for each root type (audit amendment 4)**
- **Ignored property → property excluded from generated DTO (audit amendment 4)**
- **ExplicitOnly mode + IncludedProperties → only included properties in DTO (audit amendment 4)**
- **Normalizer + Denormalizer partial class files compile together without conflicts (audit amendment 4)**
- Verify generated code compiles: `newCompilation.GetDiagnostics().Where(d => d.Severity == Error)` is empty
- Verify generated code has no errors from missing references (System.Linq, MemoryExtensions) **(audit amendment 4)**

**Step 2: Run tests, verify RED**

**Step 3: Wire the pipeline in NormalizeGenerator**

Inside `RegisterSourceOutput` (combined with `CompilationProvider`):
1. Reconstruct `ClassDeclarationSyntax` from `ConfigInfo.ClassSyntaxReference` + `Compilation` **(audit amendment 4)**
2. `ConfigurationParser.Parse(configClass, semanticModel)` → `NormalizationModel`
3. For each root type: `TypeGraphAnalyzer.Analyze(rootType, model.InlinedTypes, model.ExplicitTypes, model.TypeConfigurations, model.AutoDiscover)` → `List<TypeGraphNode>` **(audit amendment 4: pass TypeConfigurations for property filtering)**
4. Deduplicate across multiple graphs (`HashSet<string>` of emitted types)
5. For each unique node: `DtoEmitter.Emit(node)` → source, `context.AddSource("{Namespace}.Normalized{TypeName}.g.cs", source)` **(audit amendment 4: FQN-based hint names to prevent collisions)**
6. `NormalizerEmitter.Emit(model, nodes)` → config partial source with Normalize overloads per root
7. `DenormalizerEmitter.Emit(model, nodes)` → config partial source with Denormalize overloads per root
8. `context.AddSource("{ConfigNamespace}.{ConfigClassName}.Normalizer.g.cs", normalizerSource)` + `context.AddSource("{ConfigNamespace}.{ConfigClassName}.Denormalizer.g.cs", denormalizerSource)` **(audit amendment 4: separate files for normalizer/denormalizer)**
9. Report diagnostics (DN0001 for cycles, DN0004 for inlined complex types)

**v1 Limitations (documented, not implemented):**
- `CopySourceAttributes()` — flag is parsed but emitters do not copy source attributes to generated DTOs **(audit amendment 4)**
- `UseJsonNaming()` — runtime API exists but `NormalizationModel` has no field to store the policy; emitters do not generate `[JsonPropertyName]` attributes **(audit amendment 4)**

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

Must include (full test matrix from audit amendment 5):

**Group A: Basic normalization**
1. Simple flat type — name, age preserved
2. One level nested — Person→Address, index + resolution
3. Value-equality dedup — two `Address` instances with same values → one collection entry
4. Different values → different indices
5. Reference dedup — same `Address` reference → same index
6. Null property — `WorkAddress = null` → `WorkAddressIndex = null`
7. Null collection — `PhoneNumbers = null` → empty array
8. Empty collection — `PhoneNumbers = new()` → empty indices

**Group B: Deep nesting (audit amendment 5)**
9. **Deep chain (7 levels)** — Universe→Galaxy→SolarSystem→Planet→Continent→Country→City. All 7 types in graph. Full roundtrip. **(audit amendment 5)**
10. **Diamond shared leaf** — Person has HomeAddress and WorkAddress (both Address type), both reference same City. City deduped to 1 entry. **(audit amendment 5)**
11. **Shared references at different depths** — Department→Address→City, Employee→Address→City (same City). Verify cross-depth dedup. **(audit amendment 5)**

**Group C: Circular references at multiple depths (audit amendment 5)**
12. Self-referential (1-hop) — TreeNode.Parent → no infinite loop, correct roundtrip
13. **Mutual reference (2-hop)** — Person↔Company: `person.Employer = company`, `company.Ceo = person`. Both survive roundtrip. **(audit amendment 5)**
14. **Triangle cycle (3-hop)** — A→B→C→A. All three types correctly normalized + denormalized. **(audit amendment 5)**
15. **Deep cycle (4-hop)** — Org→Project→Team→Member→Org. Full roundtrip. **(audit amendment 5)**
16. **Cycle + non-cyclic branch** — TreeNode (self-ref) also has `Location: Address`. Both cycle and branch survive roundtrip. **(audit amendment 5)**
17. Tree structure 3+ levels — root→child→grandchild, with parent back-refs

**Group D: Collections**
18. Collection with duplicates → dedup
19. `List<T>` roundtrip (standard)
20. `T[]` roundtrip **(audit amendment)**
21. `IReadOnlyList<T>` roundtrip **(audit amendment)**

**Group E: Configuration features**
22. `IgnoreProperty` — property excluded from DTO
23. `PropertyMode.ExplicitOnly` + `[NormalizeInclude]` — only included properties **(audit amendment)**
24. Multiple root types — both `Normalize(Person)` and `Normalize(Order)` work **(audit amendment 5)**

**Group F: Roundtrip verification**
25. **Full roundtrip** — Normalize → Denormalize → deep-equals original (simple, nested, nullable, collection)
26. **Reference identity after roundtrip** — if two Persons share Address, `person1.HomeAddress` should be same reference as `person2.HomeAddress` after denormalization **(audit amendment 5)**
27. **Deep nesting roundtrip** — 7-level chain survives normalize→denormalize
28. **Circular roundtrip** — each cycle scenario (tests 12-17) verifies normalize→denormalize→values match

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
- Modify: `samples/DataNormalizer.Samples/DataNormalizer.Samples.csproj` **(audit amendment 6: add generator analyzer reference)**
- Create: `samples/DataNormalizer.Samples/Models/` (sample domain types)
- Create: `samples/DataNormalizer.Samples/SampleNormalization.cs`
- Modify: `samples/DataNormalizer.Samples/Program.cs`

**Step 0: Fix .csproj** — Add explicit generator analyzer reference (same fix as integration tests): **(audit amendment 6)**
```xml
<ProjectReference Include="..\..\src\DataNormalizer.Generators\DataNormalizer.Generators.csproj"
    OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
```
Add `<NoWarn>$(NoWarn);DN0001</NoWarn>` if circular types are used.

**Step 1: Create sample domain model** — e-commerce with 3-level nesting: `Order` → `Customer` → `Address`, plus `Product`, `OrderLine`. Customer and Order share Address (demonstrates dedup). **(audit amendment 6)**

**Step 2: Create normalization config** — demonstrate ONLY working v1 features: **(audit amendment 6)**
- Auto-discovery via `NormalizeGraph<Order>()`
- Opt-out via `graph.Inline<T>()`
- `IgnoreProperty` via `ForType<T>(p => p.IgnoreProperty(...))`
- Do NOT demonstrate `CopySourceAttributes`, `UseJsonNaming`, or `PropertyMode.ExplicitOnly` (v1 limitations)

**Step 3: Write Program.cs** — self-verifying demo that: **(audit amendment 6)**
1. Creates objects with shared references (same Address on Customer and Order)
2. Calls `Normalize()`, prints the normalized structure:
   - `result.Root` properties
   - `result.GetCollection<T>()` counts and contents
   - `result.CollectionNames` listing
   - `result.RootIndex` value
3. Calls `Denormalize()`, verifies roundtrip (assert values match, print confirmation)
4. Exits with non-zero code if any assertion fails (so `dotnet run` fails if broken)

**Step 4: Verify**

Run: `dotnet run --project samples/DataNormalizer.Samples`
Expected: Prints normalization results showing dedup, ends with "All assertions passed."

**Step 5: Commit**

---

### Task 18: README and NuGet Package Finalization

**Files:**
- Create/Update: `README.md`
- Modify: `src/DataNormalizer/DataNormalizer.csproj` (add RepositoryUrl, PackageProjectUrl) **(audit amendment 6)**
- Add XML documentation comments to all public API types **(audit amendment 6)**

**Step 1: Write README** with these sections: **(audit amendment 6 — expanded)**

1. **Header + Badges** — NuGet version, build status, license
2. **What It Does** — before/after code example showing nested object graph → flat normalized DTOs with index references
3. **Installation** — `dotnet add package DataNormalizer`
4. **Quick Start** (3 steps) — define types, create config, call Normalize/Denormalize
5. **Target Frameworks** — net8.0, net9.0, net10.0 **(audit amendment 6)**
6. **Configuration Options**:
   - Auto-discovery: `builder.NormalizeGraph<T>()`
   - Opt-out (Inline): `graph.Inline<T>()`
   - IgnoreProperty: `builder.ForType<T>(p => p.IgnoreProperty(x => x.Prop))`
   - ExplicitOnly mode: `builder.ForType<T>(p => { p.UsePropertyMode(PropertyMode.ExplicitOnly); p.IncludeProperty(...); })` **(audit amendment 6)**
   - Multiple root types: multiple `NormalizeGraph<T>()` calls
   - Attribute-based: `[NormalizeIgnore]`, `[NormalizeInclude]` **(audit amendment 6)**
7. **Generated Code Conventions** — **(audit amendment 6)**
   - DTO class naming: `Normalized{TypeName}`
   - Property naming: `{Name}Index`, `{Name}Indices`
   - `IEquatable<T>` implementation for dedup
   - DTOs are `partial` — extensible by users
8. **NormalizedResult<T> API Reference** — **(audit amendment 6)**
   - `Root` — the root normalized DTO
   - `RootIndex` — index of the root in its type collection
   - `GetCollection<T>(string typeKey)` / `GetCollection<T>()`
   - `Resolve<T>(string typeKey, int index)`
   - `CollectionNames`
9. **Circular Reference Support** — DN0001 warning, `<NoWarn>DN0001</NoWarn>`, how cycles are handled **(audit amendment 6)**
10. **Diagnostics Reference** — **(audit amendment 6)**
    - DN0001 (Warning): Circular reference detected — add `<NoWarn>DN0001</NoWarn>` if intentional
    - DN0002 (Error): Config class must be `partial`
    - DN0003 (Error): Type has no public properties
    - DN0004 (Info): Unmapped complex type will be inlined
11. **v1 Limitations** — **(audit amendment 6)**
    - `CopySourceAttributes()` — parsed but not yet implemented
    - `UseJsonNaming()` — runtime API exists, no effect on generated code
    - `WithName()` — not fully integrated
    - Circular type dedup uses simple properties only
12. **License** — MIT

**Step 1b: Add XML doc comments** to public API types: **(audit amendment 6)**
- `NormalizationConfig`, `NormalizeBuilder`, `GraphBuilder<T>`, `TypeBuilder<T>`
- `NormalizedResult<T>`, `NormalizationContext`
- `NormalizeConfigurationAttribute`, `NormalizeIgnoreAttribute`, `NormalizeIncludeAttribute`
- `PropertyMode`

**Step 1c: Add package metadata to .csproj**: **(audit amendment 6)**
```xml
<RepositoryUrl>https://github.com/TODO/DataNormalizer</RepositoryUrl>
<PackageProjectUrl>https://github.com/TODO/DataNormalizer</PackageProjectUrl>
```

**Step 2: Verify package**

Run: `dotnet pack src/DataNormalizer/DataNormalizer.csproj -c Release`
Verify .nupkg contains: **(audit amendment 6)**
- `lib/net8.0/DataNormalizer.dll`, `lib/net9.0/DataNormalizer.dll`, `lib/net10.0/DataNormalizer.dll`
- `analyzers/dotnet/cs/DataNormalizer.Generators.dll`
- `README.md` (populated, not stub)
- Correct metadata: PackageId, Description, Authors, License, RepositoryUrl

**Step 3: Commit**

---

### Task 19: Final Verification

**Step 1: Clean build**

Run: `dotnet clean && dotnet build`
Expected: 0 errors, 0 warnings (except DN0001 in integration tests)

**Step 2: All tests**

Run: `dotnet test --verbosity normal`
Expected: All pass (183+ tests across 3 test projects)

**Step 3: Format check**

Run: `dotnet tool restore && dotnet csharpier check .` **(audit amendment 6: corrected subcommand syntax)**
Expected: All formatted

**Step 4: Package**

Run: `dotnet pack src/DataNormalizer/DataNormalizer.csproj -c Release`
Expected: Success

**Step 5: Sample runs**

Run: `dotnet run --project samples/DataNormalizer.Samples`
Expected: Prints results and "All assertions passed."

**Step 6: Check for development artifacts** **(audit amendment 6)**

Run: `grep -r "TODO\|HACK\|FIXME\|TEMP" src/ tests/ samples/ --include="*.cs"`
Expected: No results (or only documented/intentional items)

**Step 7: Move AnalyzerReleases to Shipped** **(audit amendment 6)**

Move contents of `AnalyzerReleases.Unshipped.md` to `AnalyzerReleases.Shipped.md` for v1.0.0 release.

**Step 8: Update design doc to match implementation** **(audit amendment 6)**

Fix outdated sections in `docs/plans/2026-03-19-datanormalizer-design.md`:
- NormalizedResult constructor signature (now includes rootIndex)
- Cycle detection approach (value-equality two-DTO pattern, not reference-equality visited set)

**Step 9: Final commit if needed**

```bash
git add -A && git commit -m "chore: final formatting, cleanup, and release prep"
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

---

## Audit Amendment 2: Test Coverage Gaps (2026-03-19)

After completing Phase 2, a comprehensive test audit identified gaps in both existing tests and planned future tests. All gaps were categorized by severity and resolved.

### Phase 2 Fixes Applied (code + tests committed)

| ID | Severity | Gap | Resolution |
|----|----------|-----|------------|
| H1 | HIGH | `GraphBuilder.ForType<T>()` missing | Added method + 2 tests |
| H2 | HIGH | `GraphBuilder.UseJsonNaming()` missing | Added method + 2 tests |
| M5 | MEDIUM | `NormalizedResult` constructor null guards untested | Added 2 tests |
| M6 | MEDIUM | `NormalizeIgnore/Include` `Inherited=true` (wrong default) | Fixed to `Inherited=false` + 4 tests |
| M8 | MEDIUM | `AddToCollection` overwrite behavior untested | Added 1 test |
| L12 | LOW | `GetOrAddIndex` type mismatch under same key undocumented | Added 1 test (documents `InvalidCastException`) |
| L13 | LOW | `Resolve` with nonexistent typeKey untested | Added 1 test |

Total: 13 new tests added, 2 source files fixed, 1 source file extended. Test count: 48 → 61.

### Plan Amendments for Phase 3+ (test cases added inline above)

| ID | Severity | Task | Gap | Amendment |
|----|----------|------|-----|-----------|
| H3 | HIGH | 10 | `string`/`byte[]`/`Dictionary` detected as collections | Added 3 test cases to Task 10 |
| H4 | HIGH | 14, 16 | Only `List<T>` tested; `T[]`, `IReadOnlyList<T>` untested | Added snapshot tests to Task 14, integration tests to Task 16 |
| M7 | MEDIUM | 9, 15 | DN0003/DN0004 diagnostics never tested | Added DN0003 to Task 9, DN0004 to Task 15 |
| M9 | MEDIUM | 16 | `PropertyMode.ExplicitOnly` + `[NormalizeInclude]` never integration-tested | Added to Task 16 |
| M10 | MEDIUM | 11 | Empty `Configure` body not tested | Added to Task 11 |
| L11 | LOW | 15 | Multiple config classes in same assembly | Added to Task 15 |
| L14 | LOW | 10, 13 | Multi-level nesting (A→B→C) only shallow | Added to Task 10, Task 13 |
| L15 | LOW | 13, 16 | Null collection property behavior undefined | Added to Task 13, Task 16 |

### Key Design Decision Documented

`NormalizationContext.GetOrAddIndex<TDto>` stores `Dictionary<TDto, int>` as `object` per typeKey. Calling with different `TDto` types under the same key throws `InvalidCastException`. This is by design — generated code always uses consistent types per key. The L12 test documents this contract.

---

## Audit Amendment 3: Pre-Phase 4 Model + Test Gaps (2026-03-19)

After completing Phase 3, a comprehensive audit of the actual Phase 3 code against Phase 4+ test plans identified model structural gaps and missing test coverage.

### Phase 3 Model Fixes Applied (code + tests committed)

| ID | Severity | Gap | Resolution |
|----|----------|-----|------------|
| H1 | HIGH | `AnalyzedProperty` missing collection type kind for denormalizer | Added `CollectionTypeKind` enum + `CollectionKind` property, populated in `TypeGraphAnalyzer` + 3 tests |
| H2 | HIGH | `NormalizationModel` missing config class name/namespace for emitters | Added `ConfigClassName`/`ConfigNamespace` properties, populated in `ConfigurationParser` + 3 tests |
| H3 | HIGH | Nullable reference type (`Address?`) never tested on `IsNullable` flag | Added 1 test (passed — `UnwrapNullable` already handled NRT annotations) |
| M5 | MEDIUM | `AnalyzedProperty` missing `IsReferenceType` for Equals/GetHashCode generation | Added `IsReferenceType` property, populated in `TypeGraphAnalyzer` + 1 test |

Total: 8 new tests added, 4 source files modified, 1 new source file. Test count: 91 → 99.

### Plan Amendments for Phase 4+ (test cases added inline above)

| ID | Severity | Task | Gap | Amendment |
|----|----------|------|-----|-----------|
| M1 | MEDIUM | 12, 16 | `UseJsonNaming`/`CopySourceAttributes` parsed but never emitted/integration-tested | Added to Task 12 snapshots and Task 16 integration |
| M2 | MEDIUM | 16 | `WithName()` custom collection key never tested end-to-end | Added to Task 16 |
| M3 | MEDIUM | 16 | Serialization roundtrip (normalize → JSON → denormalize) never tested | Added to Task 16 |
| M4 | MEDIUM | 15 | Cross-namespace domain types vs config class never tested | Added to Task 15 |
| L1 | LOW | 15 | Conflicting short type names from different namespaces | Added to Task 15 |
| L2 | LOW | 16 | Read-only properties on source types (denormalizer can't set) | Added to Task 16 |
| L3 | LOW | 16 | Generated partial class user extensibility | Added to Task 16 |
| L4 | LOW | 16 | Multiple graphs sharing type — runtime dedup behavior | Added to Task 16 |
| L5 | LOW | 12 | Property order preservation, DateTime/Guid/enum handling, namespace in output | Added to Task 12 |

---

## Audit Amendment 4: Pre-Phase 5 Pipeline Gaps (2026-03-19)

After completing Phase 4, a comprehensive audit of the pipeline wiring plan identified critical implementation gaps and missing E2E test coverage.

### Critical Fixes Applied (code + tests committed)

| ID | Severity | Gap | Resolution |
|----|----------|-----|------------|
| C1 | CRITICAL | Property filtering not implemented — `IgnoreProperty`, `IncludeProperty`, `PropertyMode.ExplicitOnly` parsed but never applied | Added `typeConfigurations` parameter to `TypeGraphAnalyzer.Analyze()`, added `FilterProperties()` method + 3 tests. Updated all 21 existing analyzer tests. |
| C2 | CRITICAL | Multiple root types only generates Normalize/Denormalize for `RootTypes[0]` | Both `NormalizerEmitter` and `DenormalizerEmitter` now iterate all root types, generating overloaded methods per root + 4 tests |
| C3 | CRITICAL | `ConfigInfo` missing class syntax reference for pipeline wiring | Added `ClassSyntaxReference` to `ConfigInfo`, populated from `symbol.DeclaringSyntaxReferences` |

Total: 7 new tests, all existing tests updated. Test count: 133 → 139.

### Plan Amendments for Phase 5 (test cases and pipeline steps added inline above)

| ID | Severity | Task | Gap | Amendment |
|----|----------|------|-----|-----------|
| H1 | HIGH | 15 | Generated source file hint names could collide for same-named types in different namespaces | Changed `AddSource` hint names to FQN-based: `"{Namespace}.Normalized{TypeName}.g.cs"` |
| H2 | HIGH | 15 | Normalizer and Denormalizer partial class files must compile together without conflicts | Added E2E test verifying both files merge correctly |
| H3 | HIGH | 15 | Generated code uses `System.Linq.Enumerable` — E2E compilation must include System.Linq | Added to test verification criteria |
| H4 | HIGH | 15 | Generated code uses `AsSpan().SequenceEqual()` — requires net8.0+ MemoryExtensions | Added to test verification criteria |

### v1 Documented Limitations

| Feature | Status | Detail |
|---------|--------|--------|
| `CopySourceAttributes()` | Parsed, not emitted | `NormalizationModel.CopySourceAttributes` flag is set, but no emitter reads it. Generated DTOs do not copy `[JsonPropertyName]` etc. from source types. |
| `UseJsonNaming()` | Runtime API only | `GraphBuilder.UseJsonNaming()` exists in runtime API but `NormalizationModel` has no field for it. Parser does not extract it. Emitters do not generate naming-convention attributes. |

---

## Audit Amendment 5: Pre-Phase 6 Critical Bugs + Deep Nesting Test Matrix (2026-03-19)

After completing Phase 5, a deep audit of the circular reference handling revealed two critical runtime bugs that would crash the denormalizer. These were found by manually tracing the generated normalizer code through cycle scenarios.

### Critical Bug Fixes Applied (code + tests committed)

| ID | Severity | Bug | Resolution |
|----|----------|-----|------------|
| B1 | CRITICAL | **Phantom collection entries** — Circular reference guard calls `GetOrAddIndex` but NOT `AddToCollection`, creating an index with `null` in the collection. Denormalizer crashes with `NullReferenceException` accessing phantom entries. | Redesigned normalizer pattern for circular types: register DTO early (before recursion) with simple props only, then mutate in-place during recursion. Guard's lookupDto matches early registration → same index returned. Collection entry is the same object → automatically updated. + 1 test verifying registration ordering |
| B2 | CRITICAL | **Only back-edge discoverer gets `HasCircularReference`** — For multi-hop cycles (A→B→C→A), only C gets the flag. A and B lack `IsVisited` guards → normalizer re-enters A and B, creating phantom entries. | Added DFS path tracking with `List<string> dfsPath`, `HashSet<string> inProgressSet`, `HashSet<string> cycleParticipants`. When back-edge detected, ALL types from target to current position in path are marked as cycle participants. + 3 tests (2-hop, 3-hop, 4-hop cycles) |

Total: 4 new tests, normalizer emitter restructured. Test count: 148 → 152.

### Phase 6 Test Matrix (28 scenarios across 6 groups)

The integration test plan was expanded from ~15 scenarios to 28, with special emphasis on:
- **Deep nesting**: 7-level type chain (Universe→Galaxy→SolarSystem→Planet→Continent→Country→City)
- **Multiple cycle depths**: Self-referential (1-hop), mutual (2-hop), triangle (3-hop), deep (4-hop)
- **Combined patterns**: Circular types with non-cyclic branches, diamond shared leaves, cross-depth dedup
- **Roundtrip verification**: Every nesting and cycle scenario verifies normalize→denormalize→deep-equals

---

## Audit Amendment 6: Phase 7 Docs & Samples Gaps (2026-03-20)

After completing Phase 6, a comprehensive audit of the Phase 7 plan identified gaps in the sample application, README content, package metadata, and final verification steps.

### Plan Amendments Applied (inline above)

| ID | Severity | Task | Gap | Amendment |
|----|----------|------|-----|-----------|
| 1 | HIGH | 17 | Samples `.csproj` missing generator analyzer reference | Added explicit ProjectReference with `OutputItemType="Analyzer"` |
| 2 | MEDIUM | 17 | Plan says "demonstrate custom equality" but `CopySourceAttributes`/`UseJsonNaming` are v1 limitations | Revised to demonstrate only working features |
| 3 | MEDIUM | 17 | Sample doesn't show `NormalizedResult<T>` API (`Root`, `RootIndex`, `GetCollection`, `CollectionNames`) | Added to sample requirements |
| 4 | MEDIUM | 17 | Sample should be self-verifying (exit non-zero on failure) | Added assertion requirements |
| 5 | HIGH | 18 | README documents `CopySourceAttributes`/`UseJsonNaming` as features — they don't work | Removed; added "v1 Limitations" section instead |
| 6 | HIGH | 18 | README missing diagnostics documentation (DN0001-DN0004) | Added diagnostics reference section |
| 7 | HIGH | 18 | README missing attribute API docs (`[NormalizeConfiguration]`, `[NormalizeIgnore]`, `[NormalizeInclude]`) | Added to configuration options section |
| 8 | HIGH | 18 | README missing `NormalizedResult<T>` API reference | Added API reference section |
| 9 | MEDIUM | 18 | README missing generated code conventions (`Normalized{TypeName}`, `{Name}Index`, partial classes) | Added generated code section |
| 10 | MEDIUM | 18 | README missing v1 limitations | Added limitations section |
| 11 | MEDIUM | 18 | `.csproj` missing `RepositoryUrl`, `PackageProjectUrl` | Added to Task 18 |
| 12 | MEDIUM | 18 | No XML documentation comments on public API types | Added XML doc step |
| 13 | LOW | 19 | CSharpier command uses wrong syntax (`--check` vs `check`) | Fixed to `dotnet csharpier check .` |
| 14 | MEDIUM | 19 | Plan doesn't verify NuGet package contents | Added verification step |
| 15 | LOW | 19 | Plan doesn't check for TODO/FIXME artifacts | Added grep step |
| 16 | MEDIUM | 19 | Design doc outdated (NormalizedResult constructor, cycle detection approach) | Added update step |
| 17 | LOW | 19 | AnalyzerReleases.Unshipped.md not moved to Shipped for release | Added step |
| 18 | HIGH | 18 | NuGet README is just `# DataNormalizer` (stub) | Must be populated before publishing |

### Package Contents Verified

`dotnet pack` produces correct .nupkg with:
- `lib/net8.0/DataNormalizer.dll`, `lib/net9.0/DataNormalizer.dll`, `lib/net10.0/DataNormalizer.dll`
- `analyzers/dotnet/cs/DataNormalizer.Generators.dll`
- `README.md` (currently stub — must be populated in Task 18)
