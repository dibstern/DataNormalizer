---
name: centralized-packages
description: Central Package Management (CPM) rules for the DataNormalizer project using Directory.Packages.props. Covers version management, adding/updating packages, grouped ItemGroups, NU1008 troubleshooting, transitive pinning, PrivateAssets, and the complete package inventory.
---

# Central Package Management

## What is CPM?

Central Package Management (CPM) centralizes all NuGet package version declarations into a single `Directory.Packages.props` file at the solution root. Individual `.csproj` files reference packages without specifying versions.

### Traditional vs CPM Approach

```xml
<!-- Traditional: versions scattered across .csproj files -->
<!-- Project1.csproj -->
<PackageReference Include="NUnit" Version="4.3.2" />
<!-- Project2.csproj -->
<PackageReference Include="NUnit" Version="4.3.2" />  <!-- duplicated, can drift -->

<!-- CPM: versions in one place -->
<!-- Directory.Packages.props -->
<PackageVersion Include="NUnit" Version="4.3.2" />
<!-- All .csproj files -->
<PackageReference Include="NUnit" />  <!-- no version! -->
```

## Project Configuration

### Directory.Packages.props (Version Source of Truth)

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

### Key Properties

| Property | Purpose |
|----------|---------|
| `ManagePackageVersionsCentrally` | Enables CPM. Must be `true`. |
| `CentralPackageTransitivePinningEnabled` | Pins transitive dependencies to declared versions. Prevents version drift. |

### Directory.Build.props Integration

`Directory.Build.props` holds shared build settings (nullable, lang version, etc.) and is separate from `Directory.Packages.props`. Both files live at the solution root and are automatically imported by MSBuild.

## Adding a New Package

### Step-by-Step

1. **Add version to `Directory.Packages.props`** (in the appropriate labeled ItemGroup):

```xml
<ItemGroup Label="Testing">
  <PackageVersion Include="NewTestPackage" Version="1.0.0" />
</ItemGroup>
```

2. **Add reference to `.csproj`** (without version):

```xml
<ItemGroup>
  <PackageReference Include="NewTestPackage" />
</ItemGroup>
```

3. **Restore and verify**:

```bash
dotnet restore
dotnet build
```

### Common Mistakes

**Mistake 1: Adding to .csproj first** — If you add `<PackageReference Include="NewPackage" />` to a `.csproj` without first adding a `<PackageVersion>` to `Directory.Packages.props`, you'll get `NU1008`.

**Mistake 2: Including version in .csproj** — Never put `Version="..."` on a `<PackageReference>` when CPM is enabled.

**Mistake 3: Wrong ItemGroup** — Place the `<PackageVersion>` in the logically correct labeled group (Code Generation, Polyfills, Testing).

## Updating Package Versions

### Update a Single Package

1. Edit the version in `Directory.Packages.props`:
```xml
<PackageVersion Include="NUnit" Version="4.4.0" />  <!-- was 4.3.2 -->
```

2. Restore and test:
```bash
dotnet restore && dotnet build && dotnet test
```

### Check for Outdated Packages

```bash
# List all packages and their versions
dotnet list package

# Show outdated packages
dotnet list package --outdated

# Show including transitive packages
dotnet list package --include-transitive
```

### Version Ranges

```xml
<!-- Exact version (recommended for libraries) -->
<PackageVersion Include="NUnit" Version="4.3.2" />

<!-- Range (use sparingly) -->
<PackageVersion Include="SomePackage" Version="[1.0.0, 2.0.0)" />  <!-- >= 1.0.0, < 2.0.0 -->
```

## Package Structure and Grouping

### ItemGroup Labels

Organize packages into labeled groups for readability. Current groups in this project:

| Label | Contents |
|-------|----------|
| `Code Generation` | Roslyn APIs for the source generator |
| `Polyfills` | PolySharp for C# 12 on netstandard2.0 |
| `Testing` | NUnit 4, Verify, test SDK |

### Current Packages

| Package | Version | Used By | Purpose |
|---------|---------|---------|---------|
| `Microsoft.CodeAnalysis.CSharp` | 4.12.0 | Generator | Roslyn syntax/semantic APIs |
| `Microsoft.CodeAnalysis.Analyzers` | 3.3.4 | Generator | Analyzer development rules |
| `PolySharp` | 1.14.1 | Generator | C# 12 polyfills for netstandard2.0 |
| `NUnit` | 4.3.2 | Tests | Test framework |
| `NUnit3TestAdapter` | 4.6.0 | Tests | VS Test adapter |
| `Microsoft.NET.Test.Sdk` | 17.12.0 | Tests | Test host |
| `Verify.NUnit` | 28.5.0 | Generator Tests | Snapshot testing |
| `Verify.SourceGenerators` | 2.5.0 | Generator Tests | Generator snapshot helpers |

### Viewing All Packages

```bash
# All direct packages across all projects
dotnet list package

# Including transitive dependencies
dotnet list package --include-transitive

# Specific project only
dotnet list tests/DataNormalizer.Tests package
```

## Troubleshooting

### Issue 1: NU1008 — Version on PackageReference

```
error NU1008: Projects that use central package version management
should not define the version on the PackageReference items but on
the PackageVersion items...
```

**Fix:** Remove the `Version` attribute from the `<PackageReference>` in the `.csproj` and ensure the package has a `<PackageVersion>` entry in `Directory.Packages.props`.

### Issue 2: Package Not Found After Adding

**Cause:** Added `<PackageReference>` to `.csproj` but forgot `<PackageVersion>` in `Directory.Packages.props`.

**Fix:** Add `<PackageVersion Include="PackageName" Version="x.y.z" />` to `Directory.Packages.props`.

### Issue 3: Wrong Version Resolved

**Cause:** Transitive dependency pulling different version.

**Fix:** Ensure `CentralPackageTransitivePinningEnabled` is `true` and add the transitive package to `Directory.Packages.props` with the desired version.

### Issue 4: Build Fails After Version Update

**Cause:** Breaking changes in the updated package.

**Fix:**
1. Check the package's release notes/changelog
2. Roll back the version if needed: `git checkout -- Directory.Packages.props`
3. Address breaking changes, then re-update

### Issue 5: VersionOverride Needed

Some packages genuinely need a different version in one project. Use `VersionOverride` sparingly:

```xml
<!-- In .csproj - document the reason -->
<PackageReference Include="SomePackage" VersionOverride="1.0.0" />
<!-- VersionOverride: this project requires an older version due to netstandard2.0 API compatibility -->
```

### Issue 6: PrivateAssets Not Set

Generator packages should not flow to consumers:

```xml
<!-- In DataNormalizer.Generators.csproj -->
<PackageReference Include="Microsoft.CodeAnalysis.CSharp" PrivateAssets="all" />
<PackageReference Include="Microsoft.CodeAnalysis.Analyzers" PrivateAssets="all" />
<PackageReference Include="PolySharp" PrivateAssets="all" />
```

## Project-Specific Package Usage

### Runtime Library (DataNormalizer.csproj)

```xml
<!-- No PackageReference items - zero runtime dependencies! -->
<!-- Only the generator project reference as Analyzer -->
<ItemGroup>
  <ProjectReference Include="..\DataNormalizer.Generators\DataNormalizer.Generators.csproj"
                    OutputItemType="Analyzer"
                    ReferenceOutputAssembly="false" />
</ItemGroup>
```

### Generator (DataNormalizer.Generators.csproj)

```xml
<ItemGroup>
  <PackageReference Include="Microsoft.CodeAnalysis.CSharp" PrivateAssets="all" />
  <PackageReference Include="Microsoft.CodeAnalysis.Analyzers" PrivateAssets="all" />
  <PackageReference Include="PolySharp" PrivateAssets="all" />
</ItemGroup>
```

### Test Project (DataNormalizer.Tests.csproj)

```xml
<ItemGroup>
  <PackageReference Include="NUnit" />
  <PackageReference Include="NUnit3TestAdapter" />
  <PackageReference Include="Microsoft.NET.Test.Sdk" />
</ItemGroup>
```

### Generator Test Project (DataNormalizer.Generators.Tests.csproj)

```xml
<ItemGroup>
  <PackageReference Include="NUnit" />
  <PackageReference Include="NUnit3TestAdapter" />
  <PackageReference Include="Microsoft.NET.Test.Sdk" />
  <PackageReference Include="Verify.NUnit" />
  <PackageReference Include="Verify.SourceGenerators" />
  <PackageReference Include="Microsoft.CodeAnalysis.CSharp" />
</ItemGroup>
```

## Best Practices

1. **Always add to `Directory.Packages.props` first**, then `.csproj`
2. **Use labeled ItemGroups** to organize packages logically
3. **Pin transitive dependencies** with `CentralPackageTransitivePinningEnabled`
4. **Use `PrivateAssets="all"`** for build-only packages (analyzers, generators, polyfills)
5. **Never use `VersionOverride`** without a documented reason
6. **Update packages atomically** — update, restore, build, test before committing
7. **Keep zero runtime dependencies** for the main `DataNormalizer` library
8. **Review transitive dependencies** periodically with `dotnet list package --include-transitive`

## Quick Reference

### File Locations

| File | Purpose |
|------|---------|
| `Directory.Packages.props` | Package version declarations (CPM) |
| `Directory.Build.props` | Shared build settings (nullable, lang version) |
| `*.csproj` | Package references (no versions) |

### Commands

```bash
dotnet restore                          # Restore all packages
dotnet list package                     # Show all direct packages
dotnet list package --outdated          # Show packages with newer versions
dotnet list package --include-transitive # Show all packages including transitive
dotnet nuget locals all --clear         # Clear local NuGet cache (nuclear option)
```

### Error Reference

| Error | Cause | Fix |
|-------|-------|-----|
| `NU1008` | Version on PackageReference | Remove Version from .csproj |
| `NU1100` | Package not found | Add PackageVersion to Directory.Packages.props |
| `NU1608` | Detected version outside range | Check transitive pinning |
