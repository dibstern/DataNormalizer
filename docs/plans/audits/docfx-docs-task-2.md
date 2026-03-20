# Audit: Task 2 — Create DocFX configuration

**Summary:** The DocFX configuration is mostly well-structured, but there is a critical missing prerequisite (`GenerateDocumentationFile`), a potential multi-targeting build issue with `net10.0`, and a deprecated toc.yml key (`homepage`). Additionally, the `docs/plans/` internal files won't be accidentally published (the `*.md` glob is non-recursive), which is correct but worth noting as an implicit assumption.

## Findings

| # | Category | Action | Issue | File:Line | Amendment / Question |
|---|----------|--------|-------|-----------|----------------------|
| 1 | Missing Wiring | Amend Plan | `GenerateDocumentationFile` is not enabled — DocFX API pages will have no XML doc descriptions | `src/DataNormalizer/DataNormalizer.csproj` (missing property), `Directory.Build.props` (missing property) | Add a prerequisite step: set `<GenerateDocumentationFile>true</GenerateDocumentationFile>` in `Directory.Build.props` or `src/DataNormalizer/DataNormalizer.csproj` so DocFX can extract XML documentation comments |
| 2 | Implicit Assumptions | Ask User | Multi-targeting `net8.0;net9.0;net10.0` — DocFX metadata extraction builds the project and will attempt to resolve all target frameworks; `net10.0` requires .NET 10 SDK which may not be available in all environments (CI, contributor machines) | `src/DataNormalizer/DataNormalizer.csproj:3` | Should the metadata config specify a single `TargetFramework` property (e.g., `"properties": {"TargetFramework": "net8.0"}`) to avoid requiring all SDKs? Or is the assumption that all three SDKs are always present? |
| 3 | Incorrect Code | Amend Plan | `homepage` key in `toc.yml` is deprecated in DocFX v2.60+ in favor of `topicHref` | `docs/toc.yml` (planned file) | Change `homepage: api/index.md` to `topicHref: api/index.md` in the toc.yml definition |
| 4 | Implicit Assumptions | Accept | Path resolution: `"src": ".."` in the metadata config resolves relative to `docs/docfx.json`'s location, so `..` points to the repo root; `src/DataNormalizer/DataNormalizer.csproj` then resolves correctly to the actual project file | `docs/docfx.json` (planned), `src/DataNormalizer/DataNormalizer.csproj` | Verified correct — repo root is one level up from `docs/` |
| 5 | Implicit Assumptions | Accept | The `*.md` glob in build content is non-recursive and only matches markdown files directly in `docs/` (where `docfx.json` lives), so `docs/plans/*.md` internal files will NOT be published to the site | `docs/docfx.json` (planned) | This is correct behavior but depends on an implicit understanding that `*.md` is not `**/*.md` — worth documenting in a comment if DocFX supported them |
| 6 | Fragile Code | Accept | Template names `"default"` and `"modern"` are valid for DocFX v2.61+; the plan installs latest DocFX in Task 1 via `dotnet tool install docfx` without a version pin | `.config/dotnet-tools.json` (modified by Task 1) | Not a Task 2 issue per se — Task 1 pins the version via the tool manifest. The `modern` template is stable and unlikely to be removed. Acceptable risk. |
| 7 | Fragile Code | Accept | `_gitContribute.repo` is hardcoded to `https://github.com/dibstern/DataNormalizer` which matches the actual git remote (`https://github.com/dibstern/DataNormalizer.git`), but differs from the `.csproj` URLs (`https://github.com/dstern/DataNormalizer`) | `.git/config:9`, `src/DataNormalizer/DataNormalizer.csproj:11-12` | Task 8 in the plan corrects the `.csproj` URLs — no issue for Task 2 specifically, but if Task 2 is executed in isolation and the "Edit on GitHub" links are tested, they will work (Task 2's URL is correct) |
| 8 | Missing Wiring | Accept | The `DataNormalizer.Generators` project (source generator, `netstandard2.0`) is not included in the DocFX metadata sources — only `DataNormalizer.csproj` is | `docs/docfx.json` (planned), `src/DataNormalizer.Generators/DataNormalizer.Generators.csproj` | This is likely intentional since the generator is an internal analyzer/source generator and its API is not user-facing. The runtime library in `DataNormalizer.csproj` is the public API surface. Acceptable. |

## Detailed Analysis

### Finding 1: Missing `GenerateDocumentationFile` (Critical)

The source files have excellent XML doc comments (e.g., `NormalizationContext.cs` has `<summary>`, `<param>`, `<typeparam>`, `<returns>` tags throughout). However, neither `src/DataNormalizer/DataNormalizer.csproj` nor `Directory.Build.props` sets `<GenerateDocumentationFile>true</GenerateDocumentationFile>`.

Without this property, the C# compiler will not emit the `.xml` documentation file alongside the assembly during build. DocFX's `metadata` command builds the project and reads this XML file to populate API documentation descriptions. Without it, the generated API pages will show type/method signatures but **all description text will be empty**.

**Recommended amendment:** Add a step before or within Task 2:
```xml
<!-- In Directory.Build.props or DataNormalizer.csproj -->
<GenerateDocumentationFile>true</GenerateDocumentationFile>
```

Note: Enabling this project-wide (in `Directory.Build.props`) will surface `CS1591` warnings (missing XML doc) for any public members without doc comments in ALL projects, and `TreatWarningsAsErrors` is enabled (`Directory.Build.props:6`). This may cause build failures. Consider either:
- Adding it only to `src/DataNormalizer/DataNormalizer.csproj`, or
- Adding `<NoWarn>CS1591</NoWarn>` alongside it, or  
- Adding it to `Directory.Build.props` with a condition

### Finding 2: Multi-targeting and DocFX metadata extraction

`DataNormalizer.csproj` targets `net8.0;net9.0;net10.0`. When DocFX runs `metadata`, it effectively builds the project. With multi-targeting, it may attempt to build for all three TFMs (or pick one). If `net10.0` SDK isn't installed, the build will fail.

DocFX metadata supports a `properties` field to override MSBuild properties:
```json
{
  "src": [...],
  "dest": "api",
  "properties": {
    "TargetFramework": "net8.0"
  }
}
```

This would ensure DocFX only builds for one framework, avoiding SDK availability issues.

### Finding 3: Deprecated `homepage` key

In DocFX v2.60+, the `homepage` key in `toc.yml` was replaced by `topicHref`. While `homepage` may still work as a fallback, it generates deprecation warnings and may be removed in future versions. The fix is straightforward:

```yaml
- name: API Reference
  href: api/
  topicHref: api/index.md
```

## No issues found in:

All assigned categories were investigated. No additional issues were found beyond those listed above.
