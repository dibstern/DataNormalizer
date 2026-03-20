# Audit: Tasks 6-7 — Local Build Verification and GitHub Actions Docs Workflow

**Summary:** Task 7 has several significant issues that will cause CI failures: DocFX is not currently in the tool manifest (dependency on Task 1 completion), the workflow does not restore NuGet packages before DocFX metadata generation (which requires building the project), `GenerateDocumentationFile` is not enabled in the project so XML docs won't be produced, and the workflow only installs .NET 8.0.x while the project multi-targets `net8.0;net9.0;net10.0`. Task 6 is largely a manual verification step and is lower risk, but inherits the XML docs issue.

## Findings

### Finding 1: DocFX not in `.config/dotnet-tools.json` — `dotnet tool restore` will fail

**Action:** Accept

**Details:** The current `.config/dotnet-tools.json` (lines 4-12) only contains `csharpier`. The `dotnet tool restore` step in the workflow will not restore DocFX unless Task 1 has been completed first, adding DocFX to the manifest. This is a sequencing dependency, not a plan defect — Task 1 explicitly addresses this. However, the plan should be clear that Tasks 6 and 7 are strictly dependent on Task 1 completion. If Task 1 is skipped or fails, both Task 6 and Task 7 will break.

**File:** `.config/dotnet-tools.json:4-12`

---

### Finding 2: Missing `dotnet restore` step — DocFX metadata generation will fail in CI

**Action:** Amend Plan

**Details:** The docs workflow runs `dotnet docfx docs/docfx.json` which includes a `metadata` section (from Task 2's `docfx.json`) that points to `src/DataNormalizer/DataNormalizer.csproj`. DocFX's metadata extraction step internally builds/loads the project to extract API information. This requires NuGet packages to be restored first.

The project `DataNormalizer.csproj` has a `ProjectReference` to `DataNormalizer.Generators.csproj` (line 23-27), which depends on `Microsoft.CodeAnalysis.CSharp` and `Microsoft.CodeAnalysis.Analyzers` (managed via Central Package Management in `Directory.Packages.props`). Without a `dotnet restore` step, the DocFX metadata step will fail because dependencies cannot be resolved.

The existing `release.yml` (line 27) and `ci.yml` (line 25) both include explicit `dotnet restore DataNormalizer.sln` steps. The docs workflow must do the same.

**Amendment:** Add a `dotnet restore` step in the workflow after "Setup .NET" and before "Build documentation":
```yaml
      - name: Restore NuGet packages
        run: dotnet restore src/DataNormalizer/DataNormalizer.csproj
```
(Or `dotnet restore DataNormalizer.sln` if DocFX needs transitive project references resolved.)

---

### Finding 3: `GenerateDocumentationFile` is not enabled — no XML docs will be produced

**Action:** Amend Plan

**Details:** DocFX's metadata extraction relies on XML documentation files (`.xml`) generated during build. These are produced when `<GenerateDocumentationFile>true</GenerateDocumentationFile>` is set in the project file or `Directory.Build.props`.

Neither `src/DataNormalizer/DataNormalizer.csproj` nor `Directory.Build.props` sets `GenerateDocumentationFile`. A search across all `.csproj` and `.props` files for `GenerateDocumentationFile` or `DocumentationFile` returns zero results.

Without this setting, DocFX will still generate API pages (it can extract type/member signatures from the compiled assembly), but **all XML doc comments** (`<summary>`, `<param>`, `<returns>`, `<remarks>`, etc.) will be missing from the generated documentation. The API reference pages will have correct type structure but no descriptions.

**Amendment:** This is a prerequisite that should be addressed before Task 6. Add a step (to an earlier task, or as a new prerequisite step in Task 6) to enable XML documentation:

Option A — In `Directory.Build.props`:
```xml
<GenerateDocumentationFile>true</GenerateDocumentationFile>
```

Option B — Only in `src/DataNormalizer/DataNormalizer.csproj`:
```xml
<GenerateDocumentationFile>true</GenerateDocumentationFile>
```

Note: Enabling this in `Directory.Build.props` will apply to ALL projects (including tests and generators), which will cause CS1591 warnings (missing XML comments) to become errors since `TreatWarningsAsErrors` is `true` (Directory.Build.props line 6). The safer approach is to enable it only on the main `DataNormalizer.csproj` or suppress CS1591 in test projects.

**File:** `Directory.Build.props:6`, `src/DataNormalizer/DataNormalizer.csproj` (absent property)

---

### Finding 4: Workflow installs only .NET 8.0.x but project multi-targets `net8.0;net9.0;net10.0`

**Action:** Amend Plan

**Details:** The `DataNormalizer.csproj` targets `net8.0;net9.0;net10.0` (line 3). The docs workflow only installs `dotnet-version: 8.0.x`. When DocFX's metadata step tries to build/analyze the project, it may fail because the `net9.0` and `net10.0` SDKs are not available.

Both `ci.yml` (lines 17-22) and `release.yml` (lines 15-20) install all five SDK versions (`6.0.x` through `10.0.x`). The docs workflow should install at minimum the SDKs needed by the target project.

DocFX typically only needs to build for one target framework (it can be configured with `properties.TargetFramework` in the metadata section), so there are two valid fixes:

**Amendment (Option A — match existing workflows):** Install all required .NET SDKs:
```yaml
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: |
            8.0.x
            9.0.x
            10.0.x
```

**Amendment (Option B — pin DocFX to single TFM):** Add `"properties": { "TargetFramework": "net8.0" }` to the `metadata` section of `docfx.json` (Task 2). This tells DocFX to only build for `net8.0`, avoiding the need for other SDKs.

---

### Finding 5: Race condition between `release.yml` and `docs.yml` on `v*` tag push

**Action:** Accept

**Details:** Both `release.yml` (line 3-4) and the proposed `docs.yml` trigger on `push: tags: ['v*']`. They will run concurrently. This is generally fine since they are independent workflows (one publishes to NuGet, the other deploys docs to GitHub Pages). There's no shared resource contention — they use different GitHub APIs and different artifact targets.

The only minor risk: if the release workflow fails (e.g., NuGet push rejected), the docs would still deploy for a version that wasn't actually released. This is a design choice, not a bug.

**File:** `.github/workflows/release.yml:3-4`

---

### Finding 6: GitHub Pages requires manual repo settings configuration

**Action:** Accept

**Details:** The workflow references `environment: name: github-pages` and uses `actions/deploy-pages@v4`, which requires GitHub Pages to be enabled in the repository settings with the source set to "GitHub Actions". The plan does address this in "Post-Implementation: Enable GitHub Pages" (implementation plan lines 458-466), which is correctly placed after all code tasks. This is informational — no plan change needed, but the implementer should be aware this is a manual step that cannot be automated.

---

### Finding 7: `actions/upload-pages-artifact@v3` — version check

**Action:** Accept

**Details:** The workflow uses `actions/upload-pages-artifact@v3` and `actions/deploy-pages@v4`. As of the plan's date (March 2026), these are current major versions. `actions/checkout@v4` and `actions/setup-dotnet@v4` also match the versions used in the existing `ci.yml` and `release.yml`. All action versions are consistent and current.

---

### Finding 8: Task 6 Step 1 expects specific API doc files that depend on project structure

**Action:** Accept

**Details:** Task 6 says to look for `docs/_site/api/DataNormalizer.Runtime.NormalizationContext.html`. This assumes the namespace and type names haven't changed. The exact filenames depend on the actual public API surface. This is a verification step, so minor filename discrepancies are expected to be caught and corrected during execution. No plan change needed — the intent is correct even if specific filenames vary.

---

### Finding 9: `concurrency` group "pages" with `cancel-in-progress: false`

**Action:** Accept

**Details:** The workflow uses `concurrency: group: "pages"` with `cancel-in-progress: false`. This means if two `v*` tags are pushed in quick succession, the second deployment will wait for the first to complete rather than canceling it. This is the correct behavior for deployments (you don't want a half-deployed site). Matches GitHub's recommended pattern for Pages deployments.

---

## Summary Table

| # | Category | Action | Issue | File:Line | Amendment / Question |
|---|----------|--------|-------|-----------|----------------------|
| 1 | Missing Wiring | Accept | DocFX not in tool manifest — depends on Task 1 | `.config/dotnet-tools.json:4-12` | Sequencing dependency; correct as-is |
| 2 | Missing Wiring | Amend Plan | No `dotnet restore` step — DocFX metadata will fail | Proposed `docs.yml` workflow | Add `dotnet restore` step before `dotnet docfx` |
| 3 | Missing Wiring | Amend Plan | `GenerateDocumentationFile` not enabled — XML doc comments won't appear | `Directory.Build.props`, `src/DataNormalizer/DataNormalizer.csproj` | Add `<GenerateDocumentationFile>true</GenerateDocumentationFile>` to csproj or props. Beware TreatWarningsAsErrors + CS1591 interaction |
| 4 | Fragile Code | Amend Plan | Only .NET 8.0.x installed but project targets net8.0/net9.0/net10.0 | `src/DataNormalizer/DataNormalizer.csproj:3` | Either install all required SDKs or pin DocFX metadata to single TFM |
| 5 | Implicit Assumptions | Accept | Both release.yml and docs.yml trigger on `v*` — concurrent but independent | `.github/workflows/release.yml:3-4` | Minor risk: docs deploy even if release fails |
| 6 | Missing Wiring | Accept | GitHub Pages requires manual repo settings — addressed in post-implementation | Plan lines 458-466 | Manual step, correctly documented |
| 7 | Fragile Code | Accept | Action versions are current (@v3, @v4) | Proposed `docs.yml` workflow | Versions consistent with existing workflows |
| 8 | Implicit Assumptions | Accept | Task 6 expected filenames depend on actual API surface | Plan Task 6 Step 1 | Verification step — discrepancies caught at runtime |
| 9 | Fragile Code | Accept | Concurrency config is correct for Pages deployments | Proposed `docs.yml` workflow | Correct pattern, no change needed |

**Critical findings requiring plan amendment:** #2, #3, #4

**No issues found in:** Incorrect Code (the YAML is syntactically correct and follows GitHub Actions patterns properly), State Issues (no shared mutable state concerns beyond the acceptable concurrent workflow finding)
