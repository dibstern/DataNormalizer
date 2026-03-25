# README & Docs Overhaul Plan Audit Synthesis

**Date:** 2026-03-25
**Plan:** `docs/plans/2026-03-25-docs-overhaul-implementation.md`
**Auditors dispatched:** 2 (benchmark script, docs accuracy)

---

## Amend Plan (10)

### Task 1: Benchmark Script

1. **Missing `nearbyCity` expansion** -- 21 places have `nearbyCity` (int) references that need expanding to full place objects. The plan description mentions this but the code doesn't implement it. Add expansion of `nearbyCity` in the places expansion step (with cycle guard -- don't recurse into the expanded place's own `nearbyCity`).

2. **Missing `hotelInfo.centerPlace` expansion** -- Routes have `hotelInfo.centerPlace` integer references (6 instances) that reference places. Add expansion of `hotelInfo.centerPlace` in the route expansion step.

3. **Direct array access without bounds checking** -- 5 call sites use `array[index]` directly instead of the bounds-checked `Lookup()` helper. Replace with `Lookup()` calls to prevent crashes on unexpected data.

### Tasks 2-9: Documentation Accuracy

4. **Task 5 (getting-started)**: Explicitly note that `TeamList` property is removed entirely from the container (Team is the root and is not referenced by other types, so `NeedsList = false`). The plan says to replace `TeamList[0]` with `Result` but doesn't mention removing `TeamList` references from the example.

5. **Task 5 (getting-started)**: Explicitly call out updating the `Normalized{TypeName}` bullet point (line 85 of current file) to use `{TypeName}Dto`.

6. **Task 6 (configuration)**: Enumerate ALL stale `Normalized{TypeName}` and `{X}List` references in configuration.md (lines 50, 87-88, 96, 100-105). Plan should explicitly list each line to update.

7. **Task 6 (configuration)**: Add explanation of root property behavior in Container Result API section (root gets `Result` but only gets a list array if referenced by other types).

8. **Task 6 (configuration)**: Update "Multiple root types" section comment from `NormalizedTeamResult` to `TeamResultDto` and `NormalizedOrderResult` to `OrderResultDto`.

9. **Missing task**: `docs/api/index.md` line 9 says `Normalized{RootType}Result` -- no existing task updates this file. Add to Task 8 (landing page update) or create a subtask.

10. **Task 7 (diagnostics)**: After adding DN1001/DN1002, update the diagnostics range references in getting-started.md ("DN0001-DN0004" → "DN0001-DN1002") and configuration.md if referenced. Plan should add this to Task 5 or Task 7.

## Ask User (1)

11. **"How It Works" section omitted** -- The current README has a detailed technical explanation (lines 125-135) describing generated DTOs, container results, and `Normalize`/`Denormalize` methods. The new README structure doesn't include this. Should it be moved to the docs site only, or kept as a brief section in the README?

## Ask User (resolved)

11. **"How It Works" section** -- User decided: omit from README, expand the existing "What the source generator produces" section in getting-started.md into a fuller "How It Works" explanation. → Amended Task 5.

## Amendments Applied

| Finding | Task | Amendment |
|---------|------|-----------|
| Missing `nearbyCity` expansion | Task 1 | Added places expansion step before carriers/lines, using `Lookup()` with no recursion |
| Missing `hotelInfo.centerPlace` expansion | Task 1 | Added `hotelInfo.centerPlace` expansion in route expansion step |
| Direct array access without bounds checking | Task 1 | Replaced all direct `array[index]` with `Lookup()` calls |
| `TeamList` removal not mentioned | Task 5 | Explicitly noted that `TeamList` is removed entirely (root NeedsList=false) |
| `Normalized{TypeName}` bullet not updated | Task 5 | Listed specific line numbers and old→new values for all stale references |
| Stale refs in configuration.md not enumerated | Task 6 | Enumerated all stale lines (50, 87-88, 96, 100-105) with specific replacements |
| Root property behavior explanation missing | Task 6 | Added requirement to explain root gets Result, only gets list if referenced |
| `NormalizedTeamResult` in multiple roots | Task 6 | Included in enumerated stale references (lines 87-88) |
| `docs/api/index.md` not updated | Task 8 | Added `docs/api/index.md` to Task 8 files and added Step 2 for API index update |
| DN range references stale | Task 5 | Added update of "DN0001–DN0004" to "DN0001–DN1002" in getting-started.md |
| "How It Works" omitted from README | Task 5 | Expanded "What the source generator produces" into full "How It Works" section in getting-started.md |

## Accept (3)

12. All JSON property names, type suffixes, container property names, and reference index names in the plan's examples are verified correct against the actual source generator code.

13. The benchmark script's unnormalized result will understate the true unnormalized size slightly because it only expands entity references visible at the top-level normalized structure. Some deeply nested structures (scheduleInfo, bookingInfo) are already inlined and won't be further expanded. This is acceptable -- the numbers still demonstrate the core argument convincingly.

14. The `tools/GzipBenchmark` project won't be in the solution file, which is fine -- it's a standalone tool not meant to be built in CI.
