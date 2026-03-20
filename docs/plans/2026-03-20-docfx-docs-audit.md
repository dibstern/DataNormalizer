# DocFX Documentation — Plan Audit Synthesis

**Date:** 2026-03-20
**Plan:** `docs/plans/2026-03-20-docfx-docs-implementation.md`
**Auditors dispatched:** 5 (covering 9 tasks)

---

## Amend Plan (6)

### 1. Pin DocFX version (Task 1)
`dotnet tool install docfx` without `--version` risks non-reproducible installs. Should match the convention set by CSharpier (pinned at `1.2.6`).
**Fix:** Add `--version X.Y.Z` to install command.

### 2. Missing `GenerateDocumentationFile` (Task 2 / Task 7)
Neither `.csproj` nor `Directory.Build.props` enables XML doc file generation. Without this, DocFX produces API pages with empty descriptions. Must be added to `DataNormalizer.csproj` only (not `Directory.Build.props`, which would trigger CS1591 errors in test projects via `TreatWarningsAsErrors`).
**Fix:** Add `<GenerateDocumentationFile>true</GenerateDocumentationFile>` to `src/DataNormalizer/DataNormalizer.csproj`.

### 3. Missing `dotnet restore` in CI workflow (Task 7)
DocFX metadata generation builds the project internally, which requires NuGet packages. The workflow has no restore step.
**Fix:** Add `dotnet restore src/DataNormalizer/DataNormalizer.csproj` before `dotnet docfx`.

### 4. Multi-targeting breaks DocFX with single SDK (Task 2 / Task 7)
Project targets `net8.0;net9.0;net10.0` but the docs workflow only installs .NET 8. DocFX metadata will fail.
**Fix:** Pin metadata extraction to single TFM: add `"properties": {"TargetFramework": "net8.0"}` to `docfx.json` metadata config.

### 5. Deprecated `homepage` in toc.yml (Task 2)
`homepage: api/index.md` is deprecated in DocFX v2.60+.
**Fix:** Change to `topicHref: api/index.md`.

### 6. Explicit `plans/` exclusion from DocFX (Task 2)
The `*.md` glob in docfx.json doesn't currently pick up `plans/` (non-recursive), but if anyone later changes it to `**/*.md`, internal planning docs would be published.
**Fix:** Add `"exclude": ["plans/**"]` to the build content entry.

## Ask User (1)

### 7. PackageProjectUrl destination (Task 8)
The plan changes `PackageProjectUrl` to point to the docs site (`dibstern.github.io/DataNormalizer`). NuGet convention is typically the GitHub repo URL. The docs URL will also 404 until Pages is enabled and first deployment succeeds.
**Question:** Should `PackageProjectUrl` point to the docs site or the GitHub repo?

## Accept (informational, no action needed)

- Path resolution `"src": ".."` in docfx.json verified correct
- Templates `default` and `modern` are valid
- `docs/api/*.yml` gitignore pattern correctly preserves hand-written `index.md`
- README section references are accurate
- Action versions (@v3, @v4) are current
- Concurrent release/docs workflows are independent (no conflict)
- Hardcoded URLs in `.csproj`/README are unavoidable

---

## Amendments Applied

| Finding | Task | Amendment |
|---------|------|-----------|
| Pin DocFX version | Task 1 | Changed install command to `dotnet tool install docfx --version 2.78.3`, added `dotnet tool list --local` verification |
| Missing GenerateDocumentationFile | New Task 2 | Added new task to enable XML doc generation in DataNormalizer.csproj only |
| Multi-targeting TFM pin | Task 3 (was 2) | Added `"properties": {"TargetFramework": "net8.0"}` to metadata config |
| Deprecated homepage | Task 3 (was 2) | Changed `homepage` to `topicHref` in toc.yml |
| Explicit plans exclusion | Task 3 (was 2) | Added `"exclude": ["plans/**"]` to build content entry |
| Missing dotnet restore | Task 8 (was 7) | Added `dotnet restore src/DataNormalizer/DataNormalizer.csproj` step to workflow |
| PackageProjectUrl destination | Task 9 (was 8) | User decided: keep pointing to GitHub repo (NuGet convention) |
| Global vs local tool wording | Header | Fixed "global tool" to "local tool" |
| README insertion point | Task 10 (was 9) | Specified exact insertion point: after line 6 (License badge) |
| Task renumbering | All | Tasks renumbered 1-10 to accommodate new Task 2 |
