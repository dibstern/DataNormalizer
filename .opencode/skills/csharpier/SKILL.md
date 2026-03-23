---
name: csharpier
description: CSharpier code formatting rules for the DataNormalizer project. Covers configuration (.csharpierrc.yaml), local tool manifest, format/check commands (v1.2.6 subcommand syntax), CI integration, and the "don't fight the formatter" philosophy.
---

# CSharpier Formatting

## Overview

CSharpier is an opinionated C# code formatter. This project uses CSharpier v1.2.6 which requires **subcommand syntax** (`dotnet csharpier format .` instead of the older `dotnet csharpier .`).

## Configuration

### .csharpierrc.yaml

Located at the solution root:

```yaml
printWidth: 120
```

This is the only configuration. CSharpier is intentionally opinionated — there are very few knobs.

### .config/dotnet-tools.json

CSharpier is installed as a local .NET tool:

```json
{
  "version": 1,
  "isRoot": true,
  "tools": {
    "csharpier": {
      "version": "1.2.6",
      "commands": ["dotnet-csharpier"]
    }
  }
}
```

## Commands

### IMPORTANT: v1.2.6 Subcommand Syntax

CSharpier v1.2.6 uses **subcommand syntax**. The older `dotnet csharpier .` syntax is deprecated.

```bash
# Format all files (modifies in place)
dotnet csharpier format .

# Check formatting (no modifications, exits non-zero if unformatted)
dotnet csharpier check .

# Format a specific file
dotnet csharpier format src/DataNormalizer/Runtime/NormalizationContext.cs

# Check a specific file
dotnet csharpier check src/DataNormalizer/Runtime/NormalizationContext.cs
```

### Restore Tools First

Before running CSharpier (especially in CI or fresh clones):

```bash
dotnet tool restore
```

## CI Integration

In `.github/workflows/ci.yml`:

```yaml
- name: Check formatting
  run: dotnet tool restore && dotnet csharpier check .
```

This step fails the CI build if any files are not formatted correctly.

## Workflow Rules

### Always Format Before Committing

```bash
# Before every commit
dotnet csharpier format .
git add -A
git commit -m "your message"
```

### Don't Fight the Formatter

If CSharpier reformats your code in a way you don't prefer, **accept it**. The value of CSharpier is consistency, not personal preference.

```csharp
// You wrote this:
var result = someObject.Method1().Method2().Method3().Method4();

// CSharpier reformats to:
var result = someObject
    .Method1()
    .Method2()
    .Method3()
    .Method4();

// Accept it. Don't try to work around the formatter.
```

### What CSharpier Does NOT Format

- Comments content (it preserves comment text)
- String content
- `.csproj` / `.props` / `.targets` XML files
- YAML / JSON files
- Markdown files

CSharpier only formats `.cs` files.

## Common Scenarios

### Long Lines

CSharpier wraps lines that exceed `printWidth: 120`:

```csharp
// Before (too long)
public static NormalizedPersonResult Normalize(Person source, NormalizationContext context, CancellationToken cancellationToken)

// After (CSharpier wraps it)
public static NormalizedPersonResult Normalize(
    Person source,
    NormalizationContext context,
    CancellationToken cancellationToken
)
```

### Trailing Commas

CSharpier adds trailing commas in multi-line constructs:

```csharp
var person = new Person
{
    Name = "Alice",
    Age = 30,      // trailing comma added by CSharpier
};
```

### Method Chains

```csharp
// Short chain - stays on one line
var names = items.Select(x => x.Name).ToList();

// Long chain - wrapped
var results = typeGraph
    .Where(static node => node.Kind == PropertyKind.Normalized)
    .Select(static node => node.FullyQualifiedName)
    .Distinct()
    .OrderBy(static name => name)
    .ToList();
```

## Troubleshooting

### "dotnet csharpier" Not Found

```bash
# Restore local tools
dotnet tool restore

# If that doesn't work, install it
dotnet tool install csharpier
```

### Format Check Fails in CI

The most common cause is forgetting to run `dotnet csharpier format .` before committing. Fix:

```bash
dotnet csharpier format .
git add -A
git commit --amend --no-edit  # or new commit
```

### Generated Code

CSharpier formats all `.cs` files, including generated ones in `obj/`. This is fine — generated files in `obj/` are not committed to source control (they're in `.gitignore`).

For generated files that ARE committed (e.g., Verify snapshot `.verified.cs` files), CSharpier will format them too. This is expected and desired for consistency.
