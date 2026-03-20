# Audit: Task 1 — Add DocFX as a local dotnet tool

**Summary:** Task 1 is straightforward and low-risk, but has a version-pinning gap and a minor command-flag ambiguity that should be addressed to ensure reproducible builds across environments.

## Findings

### Finding 1: No version specified for DocFX installation

**Action:** Amend Plan

**Details:** The plan says `dotnet tool install docfx` without a `--version` flag. This will install whatever the latest version of DocFX is at execution time. While `dotnet tool install` does record the resolved version in `dotnet-tools.json`, the plan should specify a target version explicitly to:

1. Ensure reproducibility — if the plan is executed weeks later, a different (possibly breaking) version could be installed.
2. Match the plan's own expectations — Task 1 Step 3 says "Expected: DocFX version output (e.g., `2.78.x` or similar)" which suggests DocFX v2, but the current latest is in the `2.7x` range. A future major version bump could break the `docfx.json` schema used in Task 2.
3. Follow the existing project convention — CSharpier in `.config/dotnet-tools.json` (line 6) is pinned to `"version": "1.2.6"`.

**Amendment:** Change the install command to specify a version, e.g.:
```
dotnet tool install docfx --version 2.78.3
```
(Use whatever the current latest stable is at plan execution time.)

---

### Finding 2: Missing `--local` flag is implicit but correct

**Action:** Accept

**Details:** The command `dotnet tool install docfx` relies on the implicit behavior that when a `.config/dotnet-tools.json` manifest exists and neither `--global` nor `--tool-path` is specified, the tool is installed locally. This is correct behavior since .NET 6+ and the manifest file exists (`.config/dotnet-tools.json`, line 1). However, for clarity and to avoid confusion with the plan header which says "Tech Stack: DocFX (dotnet global tool)" (implementation plan line 9), the command could use the explicit `--local` flag: `dotnet tool install --local docfx`. This is a readability improvement, not a correctness issue.

---

### Finding 3: Plan header says "global tool" but Task 1 installs local

**Action:** Amend Plan

**Details:** The implementation plan line 9 states:
> **Tech Stack:** DocFX (dotnet global tool), GitHub Actions, GitHub Pages

But Task 1 installs DocFX as a **local** tool (into `.config/dotnet-tools.json`), and Task 7's CI workflow correctly uses `dotnet tool restore` (line 376) which is the local tool restore pattern. The header text is misleading.

**Amendment:** Change plan line 9 from "DocFX (dotnet global tool)" to "DocFX (dotnet local tool)".

---

### Finding 4: Verification command `dotnet docfx --version` may not work as expected

**Action:** Amend Plan

**Details:** DocFX's version flag behavior has changed across versions. In recent DocFX v2 releases, the `--version` flag is supported, but the canonical invocation for a local dotnet tool is `dotnet docfx --version`. Some older guides show `docfx --version` (global install). The plan's command is correct for the local tool scenario, but a more robust verification would also confirm the tool appears in the manifest:

```bash
dotnet tool list --local
```

This also verifies the tool was registered correctly in `dotnet-tools.json`, not just that it's executable.

**Amendment:** Add a secondary verification step: `dotnet tool list --local` and confirm `docfx` appears in the output with the expected version.

---

### Finding 5: No rollback guidance if install fails

**Action:** Accept

**Details:** The plan doesn't mention what to do if `dotnet tool install docfx` fails (e.g., network issues, package source restrictions). The `nuget.config` (line 4) clears all sources and only adds `nuget.org`, which is correct for DocFX. This is unlikely to be an issue but worth noting — if the install fails, the `dotnet-tools.json` won't be modified, so there's no cleanup needed. This is informational only.

---

## Summary Table

| # | Category | Action | Issue | File:Line | Amendment / Question |
|---|----------|--------|-------|-----------|----------------------|
| 1 | Fragile Code | Amend Plan | No version pinned for DocFX — risks non-reproducible installs | `.config/dotnet-tools.json:6` (convention reference) | Add `--version 2.78.3` (or current stable) to the install command |
| 2 | Implicit Assumptions | Accept | Missing `--local` flag relies on implicit behavior — correct but implicit | `.config/dotnet-tools.json:1` | -- |
| 3 | Incorrect Code | Amend Plan | Plan header says "global tool" but installs local | `docs/plans/2026-03-20-docfx-docs-implementation.md:9` | Change "dotnet global tool" to "dotnet local tool" |
| 4 | Fragile Code | Amend Plan | Verification step could be more robust | Plan Task 1, Step 3 | Add `dotnet tool list --local` as secondary verification |
| 5 | Implicit Assumptions | Accept | No rollback guidance if install fails — but no cleanup needed anyway | `nuget.config:4` | -- |

**No issues found in:** (all assigned categories had at least minor findings)
