---
name: centralized-packages
description: Central Package Management (CPM) rules for the DataNormalizer project using Directory.Packages.props. Covers version management, grouped ItemGroups, adding packages, common errors (NU1008), and commands for listing and updating packages.
---

# Central Package Management

## Overview

This project uses .NET Central Package Management (CPM). All NuGet package versions are declared in `Directory.Packages.props` at the solution root. Individual `.csproj` files reference packages without specifying versions.

## Directory.Packages.props Structure

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

## Key Properties

### ManagePackageVersionsCentrally

Enables CPM. Must be `true` in `Directory.Packages.props`.

### CentralPackageTransitivePinningEnabled

Pins transitive dependencies to the versions specified in `Directory.Packages.props`. Prevents version drift where a transitive dependency pulls in an older or newer version than expected.

## Rules

### 1. All Versions in Directory.Packages.props

Every package version must be declared in `Directory.Packages.props` using `<PackageVersion>`. Never put a `Version` attribute on `<PackageReference>` in a `.csproj` file.

```xml
<!-- Directory.Packages.props - CORRECT -->
<PackageVersion Include="NUnit" Version="4.3.2" />

<!-- .csproj - CORRECT -->
<PackageReference Include="NUnit" />

<!-- .csproj - WRONG (will cause NU1008) -->
<PackageReference Include="NUnit" Version="4.3.2" />
```

### 2. PackageVersion vs PackageReference

| Element | File | Has Version? |
|---------|------|-------------|
| `<PackageVersion>` | `Directory.Packages.props` | Yes |
| `<PackageReference>` | `*.csproj` | No |

### 3. Group with Labels

Organize `<PackageVersion>` entries into labeled `<ItemGroup>` blocks for readability:

```xml
<ItemGroup Label="Code Generation">
  <PackageVersion Include="Microsoft.CodeAnalysis.CSharp" Version="4.12.0" />
</ItemGroup>

<ItemGroup Label="Testing">
  <PackageVersion Include="NUnit" Version="4.3.2" />
</ItemGroup>
```

### 4. No VersionOverride Unless Absolutely Necessary

`VersionOverride` in a `.csproj` bypasses CPM for a specific package. Avoid it. If you must use it, add a comment explaining why:

```xml
<!-- AVOID THIS -->
<PackageReference Include="SomePackage" VersionOverride="1.0.0" />

<!-- If truly necessary, document it -->
<PackageReference Include="SomePackage" VersionOverride="1.0.0" />
<!-- VersionOverride: this project requires an older version due to API compatibility -->
```

### 5. PrivateAssets for Build-Only Packages

Packages that should not flow to consumers use `PrivateAssets="all"`:

```xml
<!-- In .csproj -->
<PackageReference Include="Microsoft.CodeAnalysis.CSharp" PrivateAssets="all" />
<PackageReference Include="PolySharp" PrivateAssets="all" />
```

This is independent of CPM — the version is still in `Directory.Packages.props`, but the asset control is in the `.csproj`.

## Adding a New Package

### Step-by-step:

1. **Add to `Directory.Packages.props`** first:
   ```xml
   <ItemGroup Label="Testing">
     <PackageVersion Include="NewPackage" Version="1.0.0" />
   </ItemGroup>
   ```

2. **Add to `.csproj`** (without version):
   ```xml
   <ItemGroup>
     <PackageReference Include="NewPackage" />
   </ItemGroup>
   ```

3. **Restore and verify**:
   ```bash
   dotnet restore
   dotnet build
   ```

### Common Mistake: Adding to .csproj First

If you add `<PackageReference Include="NewPackage" />` to a `.csproj` without first adding a `<PackageVersion>` to `Directory.Packages.props`, you'll get:

```
error NU1008: Projects that use central package version management
should not define the version on the PackageReference items...
```

Always add to `Directory.Packages.props` first.

## Common Error: NU1008

```
error NU1008: Projects that use central package version management
should not define the version on the PackageReference items but on
the PackageVersion items...
```

**Cause:** A `.csproj` file has `Version="..."` on a `<PackageReference>`.

**Fix:** Remove the `Version` attribute from the `<PackageReference>` in the `.csproj` and ensure the package has a `<PackageVersion>` entry in `Directory.Packages.props`.

## Updating Package Versions

### Check for Outdated Packages

```bash
# List all packages and their versions
dotnet list package

# Show outdated packages
dotnet list package --outdated

# Show including transitive packages
dotnet list package --include-transitive
```

### Update a Package

1. Edit the version in `Directory.Packages.props`:
   ```xml
   <PackageVersion Include="NUnit" Version="4.4.0" />  <!-- was 4.3.2 -->
   ```

2. Restore and test:
   ```bash
   dotnet restore
   dotnet build
   dotnet test
   ```

## Project-Specific Examples

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
