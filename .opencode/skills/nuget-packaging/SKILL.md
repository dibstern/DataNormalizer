---
name: nuget-packaging
description: NuGet library packaging best practices for the DataNormalizer project. Covers bundling the source generator as an analyzer, package metadata, SourceLink, deterministic builds, semantic versioning, dotnet pack, and snupkg symbol packages.
---

# NuGet Packaging

## Package Architecture

DataNormalizer ships as a **single NuGet package** that bundles both the runtime library and the source generator. Consumers install one package and get compile-time code generation automatically.

### How It Works

```
DataNormalizer.nupkg
├── lib/
│   ├── net6.0/DataNormalizer.dll         # Runtime types
│   ├── net7.0/DataNormalizer.dll
│   ├── net8.0/DataNormalizer.dll
│   ├── net9.0/DataNormalizer.dll
│   └── net10.0/DataNormalizer.dll
├── analyzers/
│   └── dotnet/
│       └── cs/
│           └── DataNormalizer.Generators.dll  # Source generator
└── README.md
```

The generator DLL is placed in `analyzers/dotnet/cs/` — the standard location for Roslyn analyzers and source generators. The .NET SDK automatically loads it during compilation.

## Source Generator Bundling

### Project Reference Setup

In `src/DataNormalizer/DataNormalizer.csproj`:

```xml
<!-- Reference the generator as an Analyzer (not a runtime dependency) -->
<ItemGroup>
  <ProjectReference Include="..\DataNormalizer.Generators\DataNormalizer.Generators.csproj"
                    OutputItemType="Analyzer"
                    ReferenceOutputAssembly="false" />
</ItemGroup>

<!-- Pack the generator DLL into the analyzers folder -->
<ItemGroup>
  <None Include="..\DataNormalizer.Generators\bin\$(Configuration)\netstandard2.0\DataNormalizer.Generators.dll"
        Pack="true" PackagePath="analyzers/dotnet/cs" Visible="false" />
  <None Include="..\..\README.md" Pack="true" PackagePath="\" />
</ItemGroup>
```

Key attributes:
- `OutputItemType="Analyzer"`: Tells MSBuild this is an analyzer, not a library reference
- `ReferenceOutputAssembly="false"`: Don't add the generator DLL to the runtime assembly list
- `Pack="true" PackagePath="analyzers/dotnet/cs"`: Include in the NuGet package at the correct path

## Package Metadata

All metadata goes in the `.csproj` `PropertyGroup`:

```xml
<PropertyGroup>
  <PackageId>DataNormalizer</PackageId>
  <Description>Source generator that normalizes nested object graphs into flat, deduplicated representations.</Description>
  <Authors>David Stern</Authors>
  <PackageLicenseExpression>MIT</PackageLicenseExpression>
  <PackageTags>normalization;source-generator;dto;deduplication;serialization</PackageTags>
  <PackageReadmeFile>README.md</PackageReadmeFile>
  <RepositoryUrl>https://github.com/dstern/DataNormalizer</RepositoryUrl>
  <RepositoryType>git</RepositoryType>
</PropertyGroup>
```

### README in Package

The README is included in the NuGet package and displayed on nuget.org:

```xml
<ItemGroup>
  <None Include="..\..\README.md" Pack="true" PackagePath="\" />
</ItemGroup>
```

## SourceLink

Enables step-through debugging from NuGet package consumers into your source code.

```xml
<PropertyGroup>
  <PublishRepositoryUrl>true</PublishRepositoryUrl>
  <EmbedUntrackedSources>true</EmbedUntrackedSources>
</PropertyGroup>
```

SourceLink is included automatically by the .NET SDK for GitHub-hosted projects.

## Deterministic Builds

Ensure builds produce identical output regardless of build machine:

```xml
<PropertyGroup>
  <Deterministic>true</Deterministic>
  <ContinuousIntegrationBuild Condition="'$(CI)' == 'true'">true</ContinuousIntegrationBuild>
</PropertyGroup>
```

- `Deterministic`: Normalizes file paths and timestamps in the output
- `ContinuousIntegrationBuild`: Additional determinism for CI (strips local paths from PDBs)

## Symbol Packages

Publish symbol packages (`.snupkg`) alongside the main package for debugging:

```xml
<PropertyGroup>
  <IncludeSymbols>true</IncludeSymbols>
  <SymbolPackageFormat>snupkg</SymbolPackageFormat>
</PropertyGroup>
```

When you `dotnet nuget push` the `.nupkg`, the `.snupkg` is automatically pushed to the NuGet symbol server.

## Semantic Versioning

This project uses tag-based versioning in CI:

### Version Format

```
Major.Minor.Patch[-Prerelease]
1.0.0
1.1.0-beta.1
2.0.0-rc.1
```

### When to Bump

| Change | Version Bump |
|--------|-------------|
| Bug fix, no API change | Patch (1.0.0 → 1.0.1) |
| New feature, backward compatible | Minor (1.0.0 → 1.1.0) |
| Breaking API change | Major (1.0.0 → 2.0.0) |

### CI Integration

The release workflow extracts the version from the git tag:

```yaml
- name: Extract version from tag
  id: version
  run: echo "VERSION=${GITHUB_REF_NAME#v}" >> $GITHUB_OUTPUT

- name: Pack
  run: dotnet pack src/DataNormalizer/DataNormalizer.csproj -c Release --no-build /p:Version=${{ steps.version.outputs.VERSION }}
```

## dotnet pack Command

### Local Development

```bash
# Pack with default version (1.0.0)
dotnet pack src/DataNormalizer/DataNormalizer.csproj -c Release

# Pack with specific version
dotnet pack src/DataNormalizer/DataNormalizer.csproj -c Release /p:Version=1.2.3

# Pack with prerelease suffix
dotnet pack src/DataNormalizer/DataNormalizer.csproj -c Release /p:Version=1.0.0-beta.1

# Output to specific directory
dotnet pack src/DataNormalizer/DataNormalizer.csproj -c Release -o ./nupkgs
```

### Verifying Package Contents

```bash
# List package contents (requires dotnet tool)
dotnet tool install -g dotnet-zip  # or use any zip explorer

# Or simply rename .nupkg to .zip and inspect
cp DataNormalizer.1.0.0.nupkg DataNormalizer.1.0.0.zip
unzip -l DataNormalizer.1.0.0.zip
```

Expected contents:
- `lib/net6.0/DataNormalizer.dll`
- `lib/net7.0/DataNormalizer.dll`
- `lib/net8.0/DataNormalizer.dll`
- `lib/net9.0/DataNormalizer.dll`
- `lib/net10.0/DataNormalizer.dll`
- `analyzers/dotnet/cs/DataNormalizer.Generators.dll`
- `README.md`
- `DataNormalizer.nuspec`

## Publishing

### To NuGet.org

```bash
dotnet nuget push ./nupkgs/DataNormalizer.1.0.0.nupkg \
  --api-key $NUGET_API_KEY \
  --source https://api.nuget.org/v3/index.json
```

### To Local Feed (for testing)

```bash
# Create local feed directory
mkdir -p ~/nuget-local

# Push to local feed
dotnet nuget push DataNormalizer.1.0.0.nupkg --source ~/nuget-local

# Add local feed to NuGet.config
dotnet nuget add source ~/nuget-local --name local
```

## Common Pitfalls

1. **Generator DLL not in package**: Verify the `Pack="true" PackagePath="analyzers/dotnet/cs"` item is correct and the path to the DLL exists at pack time (build before pack).

2. **Generator doesn't run for consumers**: Ensure `OutputItemType="Analyzer"` and `ReferenceOutputAssembly="false"` are set on the `ProjectReference`.

3. **Missing README on nuget.org**: Ensure `<PackageReadmeFile>README.md</PackageReadmeFile>` is set and the README file is included with `Pack="true"`.

4. **Version mismatch**: Always use `/p:Version=` when packing in CI to match the git tag.

5. **Transitive analyzer suppression**: If consumers don't see the generator, check that no `.props`/`.targets` in the package is suppressing analyzers.
