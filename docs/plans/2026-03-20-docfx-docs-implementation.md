# DocFX Documentation Site Implementation Plan

> **For Agent:** REQUIRED SUB-SKILL: Use executing-plans to implement this plan task-by-task.

**Goal:** Add auto-generated API reference documentation and conceptual articles using DocFX, deployed to GitHub Pages on release tags.

**Architecture:** DocFX generates static HTML from XML doc comments (on `src/DataNormalizer/`) and markdown articles. A GitHub Actions workflow builds and deploys to GitHub Pages on `v*` tag pushes. The documentation lives in `docs/` alongside existing internal planning docs (which are excluded from the published site).

**Tech Stack:** DocFX (dotnet local tool), GitHub Actions, GitHub Pages

---

### Task 1: Add DocFX as a local dotnet tool

**Files:**
- Modify: `.config/dotnet-tools.json`

**Step 1: Check current tool manifest**

Run: `cat .config/dotnet-tools.json`
Note current contents so we can add DocFX alongside any existing tools (CSharpier is already there).

**Step 2: Install DocFX as a local tool**

Run: `dotnet tool install docfx --version 2.78.3`

This adds DocFX to the `.config/dotnet-tools.json` manifest so all developers (and CI) can restore it with `dotnet tool restore`. The version is pinned for reproducible builds, matching the convention already set by CSharpier.

**Step 3: Verify installation**

Run: `dotnet tool list --local`
Expected: Both `csharpier` and `docfx` listed with their pinned versions.

Run: `dotnet docfx --version`
Expected: `2.78.3`

**Step 4: Commit**

```bash
git add .config/dotnet-tools.json
git commit -m "chore: add docfx as local dotnet tool"
```

---

### Task 2: Enable XML documentation file generation

**Files:**
- Modify: `src/DataNormalizer/DataNormalizer.csproj`

**Step 1: Add `GenerateDocumentationFile` to DataNormalizer.csproj**

Add to the `<PropertyGroup>` in `src/DataNormalizer/DataNormalizer.csproj`:

```xml
<GenerateDocumentationFile>true</GenerateDocumentationFile>
```

This is required for DocFX to extract XML doc comments. Add it to `DataNormalizer.csproj` only — NOT to `Directory.Build.props`, which would trigger CS1591 warnings (treated as errors via `TreatWarningsAsErrors`) in test projects that don't have doc comments.

**Step 2: Verify the project still builds**

Run: `dotnet build src/DataNormalizer/DataNormalizer.csproj`
Expected: Successful build. If there are CS1591 warnings for undocumented public members, add missing XML docs or suppress selectively.

**Step 3: Commit**

```bash
git add src/DataNormalizer/DataNormalizer.csproj
git commit -m "chore: enable XML documentation file generation for DocFX"
```

---

### Task 3: Create DocFX configuration

**Files:**
- Create: `docs/docfx.json`
- Create: `docs/toc.yml`

**Step 1: Create `docs/docfx.json`**

```json
{
  "metadata": [
    {
      "src": [
        {
          "files": ["src/DataNormalizer/DataNormalizer.csproj"],
          "src": ".."
        }
      ],
      "dest": "api",
      "properties": {
        "TargetFramework": "net8.0"
      },
      "includePrivateMembers": false,
      "disableGitFeatures": false,
      "disableDefaultFilter": false
    }
  ],
  "build": {
    "content": [
      {
        "files": ["api/**.yml", "api/index.md"]
      },
      {
        "files": ["articles/**.md", "articles/**/toc.yml", "toc.yml", "*.md"],
        "exclude": ["plans/**"]
      }
    ],
    "resource": [
      {
        "files": ["images/**"]
      }
    ],
    "dest": "_site",
    "globalMetadataFiles": [],
    "fileMetadataFiles": [],
    "template": ["default", "modern"],
    "globalMetadata": {
      "_appTitle": "DataNormalizer",
      "_appFooter": "DataNormalizer — MIT License",
      "_enableSearch": true,
      "_gitContribute": {
        "repo": "https://github.com/dibstern/DataNormalizer",
        "branch": "main",
        "apiSpecFolder": "docs/api"
      }
    }
  }
}
```

Notes:
- `"properties": {"TargetFramework": "net8.0"}` pins metadata extraction to a single TFM, avoiding the need for net9.0/net10.0 SDKs during docs builds.
- `"exclude": ["plans/**"]` prevents internal planning docs from being published if the glob is ever changed to recursive.

**Step 2: Create `docs/toc.yml`**

```yaml
- name: Articles
  href: articles/
- name: API Reference
  href: api/
  topicHref: api/index.md
```

Note: `topicHref` replaces the deprecated `homepage` key (removed in DocFX v2.60+).

**Step 3: Verify DocFX can parse the config (metadata generation)**

Run (from repo root): `dotnet docfx metadata docs/docfx.json`
Expected: Creates `docs/api/` with `.yml` files for each public type. Look for `DataNormalizer.Runtime.NormalizationContext.yml`, `DataNormalizer.Attributes.NormalizeConfigurationAttribute.yml`, etc.

**Step 4: Commit**

```bash
git add docs/docfx.json docs/toc.yml
git commit -m "chore: add docfx configuration for API docs and articles"
```

---

### Task 4: Create landing page and API index

**Files:**
- Create: `docs/index.md`
- Create: `docs/api/index.md`

**Step 1: Create `docs/index.md` (landing page)**

This is adapted from the README. It should include:
- Title and description
- The "What It Does" before/after example
- Installation instructions
- Links to Getting Started article and API Reference
- License note

```markdown
---
_layout: landing
---

# DataNormalizer

A .NET source generator that normalizes nested object graphs into flat, deduplicated representations.

[![NuGet](https://img.shields.io/nuget/v/DataNormalizer.svg)](https://www.nuget.org/packages/DataNormalizer)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://github.com/dibstern/DataNormalizer/blob/main/LICENSE)

## What It Does

**Before** — nested objects with shared references:

```csharp
var sharedAddress = new Address { City = "Seattle", Zip = "98101" };

var people = new[]
{
    new Person { Name = "Alice", Home = sharedAddress },
    new Person { Name = "Bob",   Home = sharedAddress },
};
```

**After** — flat, deduplicated DTOs with integer index references:

```csharp
// NormalizedPerson { Name = "Alice", HomeIndex = 0 }
// NormalizedPerson { Name = "Bob",   HomeIndex = 0 }  ← same index, deduplicated
//
// Address collection: [ NormalizedAddress { City = "Seattle", Zip = "98101" } ]
```

Shared `Address` instances are stored once. References become integer indices into flat collections.

## Get Started

```
dotnet add package DataNormalizer
```

- [Getting Started](articles/getting-started.md) — Quick tutorial
- [Configuration Guide](articles/configuration.md) — All configuration options
- [Diagnostics Reference](articles/diagnostics.md) — Compiler diagnostics DN0001–DN0004
- [API Reference](api/index.md) — Full API documentation

## Target Frameworks

The runtime library targets `net8.0`, `net9.0`, and `net10.0`. The source generator targets `netstandard2.0` (a Roslyn requirement) and is bundled in the same NuGet package.

## License

MIT — see [LICENSE](https://github.com/dibstern/DataNormalizer/blob/main/LICENSE).
```

**Step 2: Create `docs/api/index.md`**

```markdown
# API Reference

Browse the auto-generated API documentation for the DataNormalizer library.

## Namespaces

- **DataNormalizer.Attributes** — Attributes for configuring normalization (`[NormalizeConfiguration]`, `[NormalizeIgnore]`, `[NormalizeInclude]`)
- **DataNormalizer.Configuration** — Fluent configuration API (`NormalizationConfig`, `NormalizeBuilder`, `GraphBuilder`, `TypeBuilder`, `PropertyMode`)
- **DataNormalizer.Runtime** — Runtime types (`NormalizationContext`, `NormalizedResult<T>`)
```

**Step 3: Commit**

```bash
git add docs/index.md docs/api/index.md
git commit -m "docs: add landing page and API reference index"
```

---

### Task 5: Create conceptual articles

**Files:**
- Create: `docs/articles/toc.yml`
- Create: `docs/articles/getting-started.md`
- Create: `docs/articles/configuration.md`
- Create: `docs/articles/diagnostics.md`

**Step 1: Create `docs/articles/toc.yml`**

```yaml
- name: Getting Started
  href: getting-started.md
- name: Configuration Guide
  href: configuration.md
- name: Diagnostics Reference
  href: diagnostics.md
```

**Step 2: Create `docs/articles/getting-started.md`**

Extract from README sections: "Quick Start" (lines 39–81) and "Generated Code" (lines 163–171). Include:
- Installation (`dotnet add package DataNormalizer`)
- Define domain types
- Create configuration class
- Normalize and denormalize
- What the source generator produces
- Brief mention of `NormalizedResult` API with link to API docs

**Step 3: Create `docs/articles/configuration.md`**

Extract from README sections: "Configuration Options" (lines 86–161). Include:
- Auto-discovery (`NormalizeGraph<T>()`)
- Opt-out (Inline)
- Ignore a property (fluent + attribute)
- ExplicitOnly mode (fluent + attribute)
- Multiple root types
- NormalizedResult API (lines 173–187)
- Circular references (lines 189–193)

**Step 4: Create `docs/articles/diagnostics.md`**

Extract from README sections: "Diagnostics" (lines 197–203) and "Known Constraints" (lines 205–207). Include:
- Full diagnostics table with ID, severity, description, resolution
- Expanded explanations for each diagnostic
- Known constraints for circular references

**Step 5: Commit**

```bash
git add docs/articles/
git commit -m "docs: add getting-started, configuration, and diagnostics articles"
```

---

### Task 6: Add `_site` to `.gitignore`

**Files:**
- Modify: `.gitignore`

**Step 1: Add DocFX output directory to `.gitignore`**

Append to `.gitignore`:

```
# DocFX generated site
docs/_site/
docs/api/*.yml
docs/api/.manifest
```

The `api/*.yml` files are generated by `docfx metadata` and should not be committed. The `api/index.md` IS committed (we wrote it manually in Task 3).

**Step 2: Commit**

```bash
git add .gitignore
git commit -m "chore: gitignore docfx generated output"
```

---

### Task 7: Build the site locally to verify

**Step 1: Run full DocFX build**

Run (from repo root): `dotnet docfx docs/docfx.json`
Expected: Successful build with output in `docs/_site/`. Look for:
- `docs/_site/index.html` (landing page)
- `docs/_site/api/DataNormalizer.Runtime.NormalizationContext.html` (API doc)
- `docs/_site/articles/getting-started.html`
- `docs/_site/articles/configuration.html`
- `docs/_site/articles/diagnostics.html`

**Step 2: Verify no errors or warnings (besides expected ones)**

Review the build output. Common issues:
- Missing cross-references (e.g., `<see cref="...">` pointing to types DocFX can't resolve)
- Markdown formatting issues
- Missing files referenced in `toc.yml`

**Step 3: Optionally preview locally**

Run: `dotnet docfx docs/docfx.json --serve`
Preview at `http://localhost:8080`. Verify navigation, API docs, and articles render correctly.

**Step 4: Fix any issues found**

If there are build errors or rendering problems, fix them before proceeding.

**Step 5: Commit any fixes**

```bash
git add -A
git commit -m "fix: resolve docfx build issues"
```

---

### Task 8: Create GitHub Actions docs workflow

**Files:**
- Create: `.github/workflows/docs.yml`

**Step 1: Create the workflow file**

```yaml
name: Deploy Documentation

on:
  push:
    tags: ['v*']

permissions:
  actions: read
  pages: write
  id-token: write

concurrency:
  group: "pages"
  cancel-in-progress: false

jobs:
  deploy-docs:
    environment:
      name: github-pages
      url: ${{ steps.deployment.outputs.page_url }}
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 8.0.x

      - name: Restore tools
        run: dotnet tool restore

      - name: Restore NuGet packages
        run: dotnet restore src/DataNormalizer/DataNormalizer.csproj

      - name: Build documentation
        run: dotnet docfx docs/docfx.json

      - name: Upload Pages artifact
        uses: actions/upload-pages-artifact@v3
        with:
          path: docs/_site

      - name: Deploy to GitHub Pages
        id: deployment
        uses: actions/deploy-pages@v4
```

Notes:
- Uses `dotnet tool restore` (restores DocFX from local manifest) instead of global install — version-pinned and reproducible.
- `dotnet restore` is required before DocFX because metadata generation internally builds the project and needs NuGet packages resolved.
- Only .NET 8.x SDK is needed because `docfx.json` pins metadata extraction to `TargetFramework: net8.0`.

**Step 2: Verify the workflow YAML is valid**

Run: `cat .github/workflows/docs.yml | python3 -c "import sys, yaml; yaml.safe_load(sys.stdin.read()); print('Valid YAML')"` (or equivalent check)

**Step 3: Commit**

```bash
git add .github/workflows/docs.yml
git commit -m "ci: add GitHub Actions workflow to deploy DocFX docs on release"
```

---

### Task 9: Fix RepositoryUrl to match actual remote

**Files:**
- Modify: `src/DataNormalizer/DataNormalizer.csproj`

**Step 1: Update URLs in `.csproj`**

The `.csproj` currently has:
```xml
<PackageProjectUrl>https://github.com/dstern/DataNormalizer</PackageProjectUrl>
<RepositoryUrl>https://github.com/dstern/DataNormalizer</RepositoryUrl>
```

The actual remote is `https://github.com/dibstern/DataNormalizer.git`. Update both to:
```xml
<PackageProjectUrl>https://github.com/dibstern/DataNormalizer</PackageProjectUrl>
<RepositoryUrl>https://github.com/dibstern/DataNormalizer</RepositoryUrl>
```

Both point to the GitHub repo (NuGet convention). The `PackageProjectUrl` stays as the repo URL rather than the docs site — this is the standard NuGet convention and avoids a 404 before GitHub Pages is enabled.

**Step 2: Commit**

```bash
git add src/DataNormalizer/DataNormalizer.csproj
git commit -m "fix: correct PackageProjectUrl and RepositoryUrl to match actual GitHub remote (dibstern)"
```

---

### Task 10: Document GitHub Pages setup in README

**Files:**
- Modify: `README.md`

**Step 1: Add documentation link to README**

Insert after line 6 (the License badge), before the blank line preceding `## What It Does`. Add:

```markdown
[Documentation](https://dibstern.github.io/DataNormalizer/) | [API Reference](https://dibstern.github.io/DataNormalizer/api/)
```

**Step 2: Commit**

```bash
git add README.md
git commit -m "docs: add documentation site links to README"
```

---

### Post-Implementation: Enable GitHub Pages

After all code is merged, the repo owner must:

1. Go to **GitHub repo → Settings → Pages**
2. Under "Source", select **GitHub Actions**
3. The next `v*` tag push will trigger the docs deployment

The site will be available at `https://dibstern.github.io/DataNormalizer/`.
