# Audit: Tasks 8-9 — URL Fix and README Update

**Summary:** The RepositoryUrl fix is correct and necessary. The PackageProjectUrl change to the docs site is a valid choice but deviates from NuGet convention and should be a deliberate decision. The README links assume GitHub Pages is already deployed, which won't be true until after the first `v*` tag push with the docs workflow enabled.

## Findings

### Finding 1: RepositoryUrl fix is correct
**Category:** Incorrect Code  
**Action:** Accept  
**Details:** The current `RepositoryUrl` at `src/DataNormalizer/DataNormalizer.csproj:12` is `https://github.com/dstern/DataNormalizer`, but the actual git remote (`.git/config:9`) is `https://github.com/dibstern/DataNormalizer.git`. The plan correctly fixes this to `https://github.com/dibstern/DataNormalizer`. No issue here.

### Finding 2: PackageProjectUrl pointing to docs site instead of GitHub repo diverges from NuGet convention
**Category:** Implicit Assumptions  
**Action:** Ask User  
**Details:** The plan changes `PackageProjectUrl` from the (wrong) GitHub URL to `https://dibstern.github.io/DataNormalizer` (the docs site). NuGet's convention for `PackageProjectUrl` is to point to the project's home — which is typically the GitHub repository page, not a documentation site. Many popular packages (e.g., Newtonsoft.Json, Serilog) use the GitHub repo URL for `PackageProjectUrl`.

The docs site *is* a legitimate project URL, but this is a design choice:
- **Option A:** Set `PackageProjectUrl` to `https://github.com/dibstern/DataNormalizer` (same as `RepositoryUrl`) — standard convention, always available even if GitHub Pages isn't set up yet.
- **Option B:** Set `PackageProjectUrl` to `https://dibstern.github.io/DataNormalizer` — better UX for consumers who want docs, but the URL will 404 until GitHub Pages is enabled and the first deployment succeeds.

**Question:** Should `PackageProjectUrl` point to the GitHub repo (convention) or the docs site (better UX after Pages is live)? Note that a NuGet package published before GitHub Pages is enabled will have a broken project URL.

### Finding 3: GitHub Pages URL will 404 until manually enabled and first deployment runs
**Category:** Implicit Assumptions  
**Action:** Amend Plan  
**Details:** Both Task 8 and Task 9 reference `https://dibstern.github.io/DataNormalizer/` as the docs site URL. However, per the plan's own "Post-Implementation" section (line 458-466), GitHub Pages must be manually enabled in repo settings *after* all code is merged, and the first `v*` tag push must succeed before the site is live.

This means:
1. Any NuGet package published between the csproj change (Task 8) and the first successful Pages deployment will have a broken `PackageProjectUrl`.
2. The README links added in Task 9 will point to a 404 until Pages is deployed.

**Amendment:** Add a note to both tasks warning that these URLs will be broken until GitHub Pages is enabled and the first docs deployment completes. Consider ordering Task 8 and 9 to be done *after* confirming Pages is live, or at minimum noting this temporal dependency clearly.

### Finding 4: GitHub username `dibstern` is verified correct
**Category:** Incorrect Code  
**Action:** Accept  
**Details:** The git remote at `.git/config:9` confirms the actual GitHub username is `dibstern`, not `dstern`. The plan correctly uses `dibstern` in all new URLs. The GitHub Pages URL format `https://{username}.github.io/{repo}/` is the standard format for project sites, which is correct.

### Finding 5: README link placement instruction is vague
**Category:** Fragile Code  
**Action:** Amend Plan  
**Details:** Task 9 says to add links "near the top" after badges. The current README (`README.md:5-6`) has two badge lines. The plan should specify the exact insertion point — after line 6 (the License badge) and before line 8 (the "## What It Does" heading). Without this precision, the links could be placed inconsistently.

**Amendment:** Change Task 9 Step 1 from "Near the top of the README (after the badges, before 'What It Does')" to "Insert on line 7 (after the `[![License: MIT]...]` badge on line 6 and before the blank line preceding `## What It Does`)."

### Finding 6: Hardcoded URLs are acceptable here
**Category:** Fragile Code  
**Action:** Accept  
**Details:** The URLs `https://dibstern.github.io/DataNormalizer/` and `https://dibstern.github.io/DataNormalizer/api/` are hardcoded in both the csproj and README. This is inherent to these file types — there's no templating mechanism for csproj `PackageProjectUrl` or README badge links. These will need manual updates if the repo is transferred or renamed, but that's unavoidable.

## Summary Table

| # | Category | Action | Issue | File:Line | Amendment / Question |
|---|----------|--------|-------|-----------|----------------------|
| 1 | Incorrect Code | Accept | RepositoryUrl fix is correct — `dibstern` matches git remote | `.git/config:9`, `DataNormalizer.csproj:12` | — |
| 2 | Implicit Assumptions | Ask User | PackageProjectUrl → docs site diverges from NuGet convention | `DataNormalizer.csproj:11` | Should it point to GitHub repo or docs site? Broken URL risk before Pages is live. |
| 3 | Implicit Assumptions | Amend Plan | URLs will 404 until GitHub Pages is manually enabled + first deploy | `DataNormalizer.csproj:11`, `README.md` | Add dependency note: Tasks 8-9 should ideally execute after Pages is confirmed live, or document the broken-link window. |
| 4 | Incorrect Code | Accept | `dibstern` username and Pages URL format are verified correct | `.git/config:9` | — |
| 5 | Fragile Code | Amend Plan | README link insertion point is vague ("near the top") | `README.md:5-8` | Specify exact insertion: after line 6, before the blank line on line 7. |
| 6 | Fragile Code | Accept | Hardcoded URLs are unavoidable in csproj/README | — | — |

**No issues found in:** (all assigned categories had findings or clean verifications as noted above)
