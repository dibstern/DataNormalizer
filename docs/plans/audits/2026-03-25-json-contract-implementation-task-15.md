# Task 15: Final build and format check — Audit Report

**Summary:** The task is a lightweight CI-like verification step, but has several concrete gaps compared to the project's actual CI workflow (`ci.yml`). The most important finding is a missing `dotnet restore` step (the `--no-restore` flag requires a prior restore that no step in Task 15 performs), and the plan's step 4 ("commit formatting fixes") contradicts step 2 (which only _checks_ formatting without fixing it). Several additional verification steps present in CI/release pipelines are absent.

---

## Findings

| # | Category | Action | Issue | File:Line | Amendment / Question |
|---|----------|--------|-------|-----------|----------------------|
| 1 | Implicit Assumptions | **Amend Plan** | **Missing `dotnet restore` before `--no-restore` build.** Steps 1 and 3 both use `--no-restore`, which assumes packages are already restored. The project's CI workflow (`.github/workflows/ci.yml:24-25`) explicitly runs `dotnet restore DataNormalizer.sln` before building. Task 15 has no restore step, so if the implementer is working from a clean state or new branch, the build will fail with missing package errors. | `.github/workflows/ci.yml:24-25` | Add Step 0: `dotnet restore DataNormalizer.sln` before the build step. Alternatively, remove `--no-restore` from step 1 and let build handle restore implicitly. |
| 2 | Implicit Assumptions | **Amend Plan** | **Missing `dotnet tool restore` before CSharpier.** CSharpier v1.2.6 is configured as a local tool in `.config/dotnet-tools.json:5-10`. The CI workflow runs `dotnet tool restore && dotnet csharpier check .` (ci.yml:31). Task 15 step 2 says `dotnet csharpier check .` without first restoring the local tool. On a fresh clone or CI runner, this will fail with "No executable found matching command 'csharpier'". | `.config/dotnet-tools.json:5-10`, `.github/workflows/ci.yml:31` | Amend Step 2 to: `dotnet tool restore && dotnet csharpier check .` (matching CI). |
| 3 | Fragile Code | **Amend Plan** | **Step 4 says "Commit any formatting fixes" but Step 2 only runs `check`, not `format`.** `dotnet csharpier check .` is a read-only operation that returns a non-zero exit code if files are unformatted — it does NOT modify files. To actually fix formatting, you must run `dotnet csharpier .` (without `check`) first, THEN re-run `dotnet csharpier check .` to verify. The plan has a logical contradiction: step 2 checks, step 4 commits fixes that were never applied. | Plan lines 865-867 | Amend to: Step 2a: `dotnet csharpier .` (apply formatting). Step 2b: `dotnet csharpier check .` (verify all formatted). Step 4 then correctly commits the fixes from 2a. |
| 4 | Insufficient Test Coverage | **Amend Plan** | **CI uses `dotnet test --no-build` but plan uses `dotnet test --no-restore`.** The CI workflow (ci.yml:34) runs `dotnet test DataNormalizer.sln --no-build` to verify the _same_ build artifacts pass tests. The plan's `--no-restore` flag still triggers a rebuild, meaning tests could pass against freshly compiled (potentially different) artifacts than step 1 built. Using `--no-build` ensures the exact artifacts from step 1 are tested. | `.github/workflows/ci.yml:34` | Amend Step 3 to: `dotnet test DataNormalizer.sln --no-build` to match CI and verify the same build artifacts. |
| 5 | Insufficient Test Coverage | **Amend Plan** | **Missing: `dotnet pack` verification.** The release pipeline (release.yml:40-41) runs `dotnet pack src/DataNormalizer/DataNormalizer.csproj -c Release --no-build`. NuGet packaging can fail even when build/test pass (e.g., missing `PackageId`, `PackageLicenseExpression`, broken `.nuspec`, or source generator packaging issues with `IncludeBuildOutput` / analyzer path mismatches). Since this plan adds significant new code to the source generator, verifying the package still packs correctly is a meaningful final check. | `.github/workflows/release.yml:40-41` | Add Step 3.5: `dotnet pack src/DataNormalizer/DataNormalizer.csproj -c Release --no-build -o ./nupkgs` to verify package creation. |
| 6 | Implicit Assumptions | **Accept** | **`TreatWarningsAsErrors` is already `true` in `Directory.Build.props:6`.** The plan says "0 errors" for the build but doesn't explicitly mention warnings. This is fine because `TreatWarningsAsErrors` is globally enabled, so any warning IS an error. The "0 errors" criterion automatically covers warnings. No plan change needed, but worth noting for the implementer's awareness. | `Directory.Build.props:6` | No change needed. The existing `TreatWarningsAsErrors=true` setting means the "0 errors" check implicitly covers warnings. |
| 7 | Insufficient Test Coverage | **Ask User** | **Missing: test coverage report for new code.** The plan's TDD approach (user wants 100% confidence) would benefit from a coverage check to verify all new code paths introduced by Tasks 1-14 are exercised. Should the final check include a coverage step (e.g., `dotnet test --collect:"XPlat Code Coverage"` with a threshold)? No coverage tooling is currently configured in the project. | — | Decision needed: Should Task 15 include a code coverage collection/threshold step, or is the TDD approach (tests written before code) considered sufficient assurance? |
| 8 | Fragile Code | **Accept** | **Plan doesn't specify build configuration (`-c Release` vs default `Debug`).** Step 1 uses `dotnet build --no-restore` which defaults to Debug configuration. CI builds both Debug (ci.yml:28) and Release (release.yml:35). For a final verification task, Debug is fine since CI will catch Release-specific issues. | `.github/workflows/ci.yml:28`, `.github/workflows/release.yml:35` | No change strictly needed, but the implementer could optionally build both configurations. |

---

## No issues found in:

- **State Issues**: Not applicable — Task 15 is a verification/check task with no shared state modifications.
- **Non-Strict Typing**: Not applicable — no code changes in this task.
- **Missing Wiring**: Not applicable — no new components to register.
- **Incorrect Code**: Not applicable — no code snippets to validate (only shell commands).

---

## Summary of Critical Findings

**Must fix before implementation (Amend Plan):**

1. **Finding #1** — Missing `dotnet restore` will cause `--no-restore` build to fail.
2. **Finding #2** — Missing `dotnet tool restore` will cause CSharpier to not be found.
3. **Finding #3** — `check` doesn't fix files; need `dotnet csharpier .` before `dotnet csharpier check .` for step 4 to make sense.
4. **Finding #4** — Use `--no-build` instead of `--no-restore` on test step to match CI and test the same build artifacts.
5. **Finding #5** — Add `dotnet pack` verification to catch packaging regressions.

**Needs user decision:**

6. **Finding #7** — Whether to add code coverage reporting to the final check.

**Recommended amended Task 15:**

```
### Task 15: Final build and format check

**Step 0:** `dotnet restore DataNormalizer.sln`
**Step 1:** `dotnet build DataNormalizer.sln --no-restore` → 0 errors
**Step 2a:** `dotnet tool restore && dotnet csharpier .` (apply formatting)
**Step 2b:** `dotnet csharpier check .` → all formatted (verify)
**Step 3:** `dotnet test DataNormalizer.sln --no-build` → all pass
**Step 4:** `dotnet pack src/DataNormalizer/DataNormalizer.csproj --no-build -o ./nupkgs` → pack succeeds
**Step 5:** Commit any formatting fixes

    style: format code with CSharpier
```
