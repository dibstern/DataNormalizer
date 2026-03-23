---
name: dotnet-architecture
description: Project structure rules, MSBuild linting, and Clean Architecture principles for the DataNormalizer repository. Covers solution layout, project references, Directory.Build.props/Directory.Packages.props linting rules, and the relationship between runtime library, source generator, tests, and samples.
---

# Project Architecture

## MSBuild Linting Rules

These rules ensure consistent MSBuild configuration across the solution.

### RULE_A: No Hardcoded Versions in Directory.Packages.props

Package versions in `Directory.Packages.props` should use meaningful version values. If the project defines version variables (e.g., in `Version.props`), use them consistently rather than hardcoding the same version in multiple places.

```xml
<!-- WRONG - same version repeated -->
<PackageVersion Include="Microsoft.CodeAnalysis.CSharp" Version="4.12.0" />
<PackageVersion Include="Microsoft.CodeAnalysis.Analyzers" Version="4.12.0" />

<!-- CORRECT - use a variable if versions are shared -->
<PackageVersion Include="Microsoft.CodeAnalysis.CSharp" Version="$(RoslynVersion)" />
<PackageVersion Include="Microsoft.CodeAnalysis.Analyzers" Version="$(RoslynVersion)" />
```

### RULE_B: Version.props Import Locations

If the project uses a `Version.props` file, it should only be imported in `Directory.Build.props` or `Directory.Packages.props` — never in individual `.csproj` files.

### RULE_G: No Package Versions in .csproj

`<PackageReference>` elements in `.csproj` files must NEVER include a `Version` attribute when CPM is enabled. All versions belong in `Directory.Packages.props`.

```xml
<!-- WRONG -->
<PackageReference Include="NUnit" Version="4.3.2" />

<!-- CORRECT -->
<PackageReference Include="NUnit" />
```

## Repository Structure

```
DataNormalizer/
├── src/
│   ├── DataNormalizer/                    # Runtime library (net8.0;net9.0)
│   │   ├── Attributes/                   # Marker attributes for source generator
│   │   ├── Configuration/                # Fluent builder API (no-op at runtime)
│   │   ├── Runtime/                      # NormalizationContext
│   │   └── DataNormalizer.csproj
│   └── DataNormalizer.Generators/         # Roslyn source generator (netstandard2.0)
│       ├── Analysis/                      # ConfigurationParser, TypeGraphAnalyzer
│       ├── Emitters/                      # DtoEmitter, NormalizerEmitter, etc.
│       ├── Models/                        # Immutable data models for pipeline
│       ├── Diagnostics/                   # DiagnosticDescriptors (DN0001-DN0004)
│       ├── NormalizeGenerator.cs          # IIncrementalGenerator entry point
│       └── DataNormalizer.Generators.csproj
├── tests/
│   ├── DataNormalizer.Tests/              # Runtime unit tests (NUnit 4)
│   ├── DataNormalizer.Generators.Tests/   # Generator snapshot tests (Verify)
│   └── DataNormalizer.Integration.Tests/  # End-to-end tests with real generated code
├── samples/
│   └── DataNormalizer.Samples/            # Example usage (console app)
├── docs/
│   └── plans/                             # Design and implementation docs
├── .opencode/                             # AI agent skills and context
├── DataNormalizer.sln
├── Directory.Build.props                  # Shared build settings
├── Directory.Packages.props               # Central Package Management
├── .csharpierrc.yaml                      # CSharpier config
├── .editorconfig                          # Editor settings
└── .github/workflows/                     # CI/CD
```

## Project Dependency Rules

```
DataNormalizer (runtime)
  └── References: DataNormalizer.Generators (as Analyzer, not assembly)

DataNormalizer.Generators (source generator)
  └── References: Microsoft.CodeAnalysis.CSharp, PolySharp (all PrivateAssets)

DataNormalizer.Tests
  └── References: DataNormalizer (runtime only)

DataNormalizer.Generators.Tests
  └── References: DataNormalizer.Generators + DataNormalizer + Microsoft.CodeAnalysis.CSharp

DataNormalizer.Integration.Tests
  └── References: DataNormalizer (which includes the generator via Analyzer)

DataNormalizer.Samples
  └── References: DataNormalizer (which includes the generator via Analyzer)
```

### Critical: Generator as Analyzer Reference

The runtime library references the generator project as an analyzer, not a regular assembly reference. This is what makes the generator run at compile time for consumers:

```xml
<!-- src/DataNormalizer/DataNormalizer.csproj -->
<ItemGroup>
  <ProjectReference Include="..\DataNormalizer.Generators\DataNormalizer.Generators.csproj"
                    OutputItemType="Analyzer"
                    ReferenceOutputAssembly="false" />
</ItemGroup>
```

The generator DLL is also packed into the NuGet package under `analyzers/dotnet/cs/`:

```xml
<ItemGroup>
  <None Include="..\DataNormalizer.Generators\bin\$(Configuration)\netstandard2.0\DataNormalizer.Generators.dll"
        Pack="true" PackagePath="analyzers/dotnet/cs" Visible="false" />
</ItemGroup>
```

## Directory.Build.props

Shared settings applied to ALL projects in the repo:

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

**Rules:**
- Never override these settings in individual `.csproj` files unless absolutely necessary
- The generator project may need additional settings (e.g., `EnforceExtendedAnalyzerRules`)
- Test projects may need `<IsPackable>false</IsPackable>`

## Clean Architecture Principles

### Layer Separation

1. **Runtime Library** (`DataNormalizer`): Contains only the public API surface — attributes, configuration types, and runtime containers. Zero external dependencies. This is what consumers interact with at runtime.

2. **Source Generator** (`DataNormalizer.Generators`): All code analysis and generation logic. Never referenced at runtime. Targets netstandard2.0 per Roslyn requirements. Uses PolySharp for C# 12 features on netstandard2.0.

3. **Tests**: Each test project tests a specific layer. Generator tests use Verify for snapshots. Integration tests exercise the full pipeline with real generated code.

### Dependency Direction

Dependencies point inward:
- Tests → Runtime / Generator
- Samples → Runtime
- Generator → Runtime (conceptually: it knows about runtime types to generate code that uses them)
- Runtime → nothing

### No Cross-Contamination

- Runtime types NEVER reference `Microsoft.CodeAnalysis`
- Generator code NEVER references test frameworks
- Test utilities are in test projects, not runtime
- Sample code demonstrates consumer usage, not internal APIs

## MSBuild Standards

### Project File Organization

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <!-- 1. Core properties -->
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <!-- Additional properties -->
  </PropertyGroup>

  <!-- 2. Package references (no versions — CPM) -->
  <ItemGroup>
    <PackageReference Include="NUnit" />
    <PackageReference Include="NUnit3TestAdapter" />
  </ItemGroup>

  <!-- 3. Project references -->
  <ItemGroup>
    <ProjectReference Include="..\..\src\DataNormalizer\DataNormalizer.csproj" />
  </ItemGroup>
</Project>
```

### Rules

- `PropertyGroup` comes first, then `ItemGroup`s
- Group `ItemGroup`s logically: packages, then project references, then content items
- Never include package versions in `.csproj` — they belong in `Directory.Packages.props`
- Use relative paths with backslashes for `ProjectReference` (MSBuild convention)
- Test projects: always set `<IsPackable>false</IsPackable>`

## Folder Organization Within Projects

### Runtime Library

```
DataNormalizer/
├── Attributes/          # [NormalizeConfiguration], [NormalizeIgnore], etc.
├── Configuration/       # NormalizationConfig, NormalizeBuilder, etc.
├── Runtime/             # NormalizationContext
└── DataNormalizer.csproj
```

### Generator

```
DataNormalizer.Generators/
├── Analysis/            # Parsing and analysis (ConfigurationParser, TypeGraphAnalyzer)
├── Emitters/            # Code generation (DtoEmitter, NormalizerEmitter, etc.)
├── Models/              # Immutable pipeline data (records, never SemanticModel)
├── Diagnostics/         # DiagnosticDescriptors
├── NormalizeGenerator.cs
└── DataNormalizer.Generators.csproj
```

### Test Projects

```
DataNormalizer.Tests/
├── Attributes/          # Mirror runtime structure
├── Configuration/
├── Runtime/
└── DataNormalizer.Tests.csproj

DataNormalizer.Generators.Tests/
├── Analysis/
├── Emitters/
├── Snapshots/           # .verified.cs files for Verify
└── DataNormalizer.Generators.Tests.csproj
```

## Adding New Types Checklist

1. Create the file in the correct folder matching the namespace
2. Use file-scoped namespace
3. Filename matches type name exactly
4. Add `sealed` unless designed for inheritance
5. Add appropriate access modifier (`public` for API surface, `internal` for implementation)
6. If it needs a package reference, add to `Directory.Packages.props` first
7. Run `dotnet build` to verify
8. Run `dotnet csharpier format .` before committing
