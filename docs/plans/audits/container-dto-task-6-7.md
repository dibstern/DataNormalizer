# Task 6 & 7: Update Integration Tests & Samples -- Audit Report

**Summary:** The plan has a critical architectural gap: all integration test configs use entity types directly as roots (e.g., `NormalizeGraph<Person>()`), not wrapper DTOs. The plan's design assumes a wrapper DTO pattern (like `PeopleDto`) where root properties are distinct from entity properties. With entity-as-root, the container replaces the per-type DTO but the root type's entities still appear in the graph and need collection storage. Self-referential types (TreeNode, LocationTreeNode) make this particularly problematic. The plan's "before/after" examples in Task 6 are based on the wrapper DTO pattern and don't address the actual test patterns at all. Task 7 has similar issues plus many specific API references that need updating.

---

## Findings

| # | Category | Action | Issue | File:Line | Amendment / Question |
|---|----------|--------|-------|-----------|----------------------|
| 1 | Implicit Assumptions | **Ask User** | **All integration test root types are entity types, not wrapper DTOs.** Every config uses `NormalizeGraph<Person>()`, `NormalizeGraph<TreeNode>()`, `NormalizeGraph<Universe>()`, etc. -- the root IS an entity type that also appears in the normalization graph. The plan's design example uses a wrapper DTO (`PeopleDto`) that wraps entity types, where root properties are collection-of-entity references. But none of the integration tests use this pattern. When Person is both root and entity, the container `NormalizedPerson` replaces the per-type `NormalizedPerson` DTO. But the plan (architecture line 7) says "Root type is no longer emitted as a per-type DTO" -- which means there's no `NormalizedPerson` DTO for use in `PersonList` arrays on other containers. **This is a fundamental design question: how does the container model work when the root type IS an entity type?** | BasicNormalizationConfig.cs:12, CycleConfig.cs:13-17, DeepNestingConfig.cs:12, IgnorePropertyConfig.cs:12 | Design decision needed: Does the container absorb the per-type DTO when root=entity? If so, how are self-referential collections stored? Does the container get its own `{TypeName}List` pointing to itself? |
| 2 | Implicit Assumptions | **Ask User** | **Self-referential root types (TreeNode, LocationTreeNode) need their own type in entity lists.** `TreeNode` has `Parent: TreeNode?` and `Children: List<TreeNode>`. When normalizing a tree, all TreeNode instances in the graph need to be stored in a flat collection. Currently: `result.GetCollection<NormalizedTreeNode>("TreeNode")` returns all of them, and `result.Root` is a `NormalizedTreeNode` that happens to be one of them. In the container model: `NormalizedTreeNode` is the container with root properties (Label, ParentIndex, ChildrenIndices). But all non-root TreeNode instances need per-type DTOs too -- they have the exact same properties. The plan says "per-type DTOs for non-root types only" but the non-root TreeNodes are the same type as the root. **Does the container include a `TreeNodeList` of its own type?** | CycleConfig.cs:13, TreeNode.cs:5-10 | Need design decision: When root type IS a self-referential entity, should the container include a `{TypeName}List` of the container type itself? Or should the root type still get a separate per-type DTO in addition to the container? |
| 3 | Implicit Assumptions | **Amend Plan** | **DeepNestingTests expects `GetCollection<NormalizedUniverse>("Universe")` -- but Universe is the root.** The test at line 20 calls `result.GetCollection<NormalizedUniverse>("Universe")` and asserts it has >= 1 entry. In the container model, Universe is the root type, so per the plan it does NOT get a per-type DTO and does NOT appear in entity lists. This test has no valid container equivalent -- there's no `UniverseList` on the container. The plan's Task 6 doesn't mention this case at all. | DeepNestingTests.cs:20 | Amend Task 6 to explicitly address: (1) Remove the `GetCollection<NormalizedUniverse>` assertion since Universe is the root and won't have a list, or (2) reconsider the architecture for entity-as-root cases. |
| 4 | Insufficient Test Coverage | **Amend Plan** | **Task 6 pattern changes are incomplete.** The plan shows only the `result.Root.X` -> `result.X` and `result.GetCollection<T>(key)` -> `result.{Type}List` transformations. But the tests also use: (a) `result.Root` as a whole object for null checks (SmokeTests:27, CircularReferenceTests:22, DeepNestingTests:16, PerformanceTests:64) -- these don't have an equivalent since `result` IS the container; (b) `result.GetCollection<T>(key)` with `.Count` vs `.Length` -- arrays use `.Length` not `.Count`; (c) `result.Root.GetType()` reflection (ConfigFeatureTests:24). The plan needs to address each of these patterns. | SmokeTests.cs:27, CircularReferenceTests.cs:22, ConfigFeatureTests.cs:24, PerformanceTests.cs:64 | Amend Task 6: Add transformation rules for (a) `result.Root` null checks -> `result` null checks (trivially true for struct-like containers); (b) `Has.Count.X` -> `Has.Length.X` for array properties; (c) `result.Root.GetType()` -> `result.GetType()`. |
| 5 | Insufficient Test Coverage | **Amend Plan** | **`result.Root` null check pattern in CircularReferenceTests is semantically different.** Tests like `Assert.That(result.Root, Is.Not.Null)` at CircularReferenceTests:22 were testing that normalization produced a valid root DTO. In the container model, `result` is always non-null if `Normalize()` returns. The null-check tests become trivially true and no longer test anything meaningful. The plan should specify what these should be replaced with. | CircularReferenceTests.cs:22, SmokeTests.cs:27, DeepNestingTests.cs:16 | Amend Task 6: Replace `result.Root` null checks with meaningful assertions on the container, e.g., `Assert.That(result, Is.Not.Null)` or assert specific root properties exist. |
| 6 | Insufficient Test Coverage | **Amend Plan** | **Plan doesn't enumerate which tests use `GetCollection<T>(key)` and need `{Type}List` replacement.** The plan provides a generic pattern but doesn't catalog all occurrences. Here's the exhaustive list: SimpleNormalizationTests lines 50, 83, 111, 202; DeepNestingTests lines 20-26 (7 calls for 7 types including root type Universe). The plan needs to specify the exact property names for each: `AddressList`, `PhoneNumberList`, and for DeepNesting: `GalaxyList`, `SolarSystemList`, `PlanetList`, `ContinentList`, `CountryList`, `CityList` (but NOT `UniverseList` -- see finding #3). | SimpleNormalizationTests.cs:50,83,111,202; DeepNestingTests.cs:20-26 | Amend Task 6: Enumerate all `GetCollection` calls and their container property replacements. Flag that `Has.Count.X` must become `Has.Length.X` for arrays. |
| 7 | Missing Wiring | **Amend Plan** | **ConfigFeatureTests uses `result.Root.GetType()` for reflection check.** At line 24, the test does `var dtoType = result.Root.GetType()` then checks `dtoType.GetProperty("Age")` is null. In the container model, the "root DTO" properties are on the container itself. This needs to become `result.GetType()`. However, the container class will also have `{Type}List` properties, which didn't exist on the old root DTO. The reflection test still works (checking Age is absent) but the semantic context changes -- the type being inspected is now the container, not a per-type DTO. | ConfigFeatureTests.cs:24 | Amend Task 6: Note that `result.Root.GetType()` becomes `result.GetType()` and that the assertion still works since Age won't be on the container either. |
| 8 | Insufficient Test Coverage | **Amend Plan** | **PerformanceTests uses `result.Root` in hot loop.** At line 64, inside a 10,000-iteration loop: `Assert.That(result.Root, Is.Not.Null)`. This becomes trivially true with container model. Should be replaced with a meaningful assertion to prevent dead-code elimination (the stated purpose). | PerformanceTests.cs:64 | Amend Task 6: Replace `result.Root` with a meaningful container assertion like `Assert.That(result.Name, Is.Not.Null)` to prevent DCE while validating the container. |
| 9 | Missing Wiring | **Amend Plan** | **Task 7 (Samples) -- Program.cs has 12 NormalizedResult API references.** The plan's sample "after" code only shows a generic pattern. The actual Program.cs uses: `result.Root.OrderId` (line 45), `result.Root.CustomerIndex` (line 46), `result.Root.ShippingAddressIndex` (line 47), `result.Root.LinesIndices` (line 48), `result.CollectionNames` (line 52), `result.GetCollection<NormalizedAddress>("Address")` (line 59), `result.GetCollection<NormalizedProduct>("Product")` (line 64), `result.RootIndex` (line 74), and for corporate: `corpResult.CollectionNames` (line 217), `corpResult.GetCollection<NormalizedEmployee>("Employee")` (line 225), `corpResult.GetCollection<NormalizedCertification>("Certification")` (line 227), `corpResult.GetCollection<NormalizedTeam>("Team")` (line 231). All need specific transformations. | samples/DataNormalizer.Samples/Program.cs:45-74,217-231 | Amend Task 7: Enumerate all 12+ API references in Program.cs with their specific replacements. Note that `CollectionNames` and `RootIndex` have no container equivalent -- those lines/sections must be removed or rewritten. |
| 10 | Missing Wiring | **Amend Plan** | **`result.CollectionNames` has no container equivalent.** Program.cs lines 52-55 iterate `result.CollectionNames` to print collection names. Lines 217-219 do the same for `corpResult.CollectionNames`. The container DTO has no `CollectionNames` property -- its entity lists are statically typed properties. This code must be rewritten (e.g., use reflection, hardcode names, or remove the section entirely). | samples/DataNormalizer.Samples/Program.cs:52-55,217-219 | Amend Task 7: Specify replacement for `CollectionNames` iteration. Options: (a) remove the section, (b) hardcode list names, or (c) use reflection. Recommend (a) or (b) since the container's typed arrays are self-documenting. |
| 11 | Missing Wiring | **Amend Plan** | **`result.RootIndex` has no container equivalent.** Program.cs line 74 prints `result.RootIndex`. In the container model, the root is not stored in a collection -- its properties are inlined on the container. There is no root index. This line must be removed. | samples/DataNormalizer.Samples/Program.cs:74 | Amend Task 7: Remove the `RootIndex` output line. |
| 12 | Missing Wiring | **Amend Plan** | **Sample `GetCollection` calls use `.Count` (IReadOnlyList) but container arrays use `.Length`.** Program.cs lines 60, 65, 225, 227, 231 all use `.Count` on `GetCollection<T>()` results. Container's `{Type}List` properties are arrays, which use `.Length`. | samples/DataNormalizer.Samples/Program.cs:60,65,225,227,231 | Amend Task 7: Note that all `.Count` on collections become `.Length` on arrays. |
| 13 | Implicit Assumptions | **Ask User** | **Multi-root config (BasicNormalizationConfig) -- do root types share entity collections?** `BasicNormalizationConfig` has `NormalizeGraph<Person>()` AND `NormalizeGraph<Order>()`. Both `Person` and `Order` reference `Address`. Currently, `Normalize(person)` and `Normalize(order)` produce separate `NormalizedResult` instances with independent contexts. In the container model: `NormalizedPerson` container has `AddressList`, `NormalizedOrder` container has `AddressList` -- this is fine since each normalization call is independent. But the test at SimpleNormalizationTests:231-261 tests both root types in the same test. **The plan should confirm that multi-root configs generate separate container types and separate `Normalize()` overloads.** | SimpleNormalizationTests.cs:231-261 | Confirm that multi-root configs produce: `NormalizedPerson Normalize(Person)` and `NormalizedOrder Normalize(Order)` as separate overloads with separate container types. |
| 14 | Fragile Code | **Accept** | **Plan's "before/after" example in Task 6 uses generic patterns, not actual test code.** The plan shows `result.Root.NameIndex` in the "before" but no actual test uses `.NameIndex` -- they use `.Name`, `.HomeAddressIndex`, `.PhoneNumbersIndices`, etc. The example is illustrative but could mislead an agent about the actual patterns. | Plan Task 6 lines 637-651 | Informational -- the examples are illustrative but the agent will need to read actual test files. |

---

## Detailed Analysis by Category

### 1. Insufficient Test Coverage

The plan lists all 7 integration test files correctly. However, it provides only a generic "before/after" pattern and doesn't enumerate the specific API changes needed in each file. The most critical gap is:

**Files with `GetCollection<T>(key)` calls that need `{Type}List` replacements:**
- `SimpleNormalizationTests.cs`: 4 calls (lines 50, 83, 111, 202) -- all use `NormalizedAddress` or `NormalizedPhoneNumber`
- `DeepNestingTests.cs`: 7 calls (lines 20-26) -- one for each of 7 types including the ROOT type `Universe`
- No `GetCollection` calls in: BasicRoundtripTests, CircularReferenceTests, ConfigFeatureTests, PerformanceTests, SmokeTests

**Files with `result.Root` accesses that need `.Root` removal:**
- `SimpleNormalizationTests.cs`: 15 occurrences of `result.Root.X`
- `SmokeTests.cs`: 1 occurrence (`result.Root`)
- `CircularReferenceTests.cs`: 1 occurrence (`result.Root`)
- `DeepNestingTests.cs`: 2 occurrences (`result.Root` and `result.Root.Name`)
- `PerformanceTests.cs`: 1 occurrence (`result.Root`)
- `ConfigFeatureTests.cs`: 4 occurrences (including `result.Root.GetType()`)

### 2. Implicit Assumptions (Entity-as-Root Problem)

This is the most architecturally significant finding. Every single integration test config uses entity types as roots:

| Config | Root Type | Has Nested Entities? | Self-Referential? |
|--------|-----------|---------------------|-------------------|
| BasicNormalizationConfig | Person | Yes (Address, PhoneNumber) | No |
| BasicNormalizationConfig | Order | Yes (Address) | No |
| IgnorePropertyConfig | Employee | No (all simple props) | No |
| CycleConfig | TreeNode | No external, but self | **Yes** (Parent, Children) |
| CycleConfig | CyclePerson | Yes (Company) | No (but Company->CyclePerson cycle) |
| CycleConfig | NodeA | Yes (NodeB, NodeC) | No (but 3-hop cycle) |
| CycleConfig | Org | Yes (Project, Team, Member) | No (but 4-hop cycle) |
| CycleConfig | LocationTreeNode | No external, but self | **Yes** (Parent, Children) |
| DeepNestingConfig | Universe | Yes (Galaxy...City) | No |

The plan's design doc uses `PeopleDto` (a wrapper DTO containing `Person[]`) as the example -- but no test or sample uses this pattern. The entire integration suite and the actual usage pattern is `NormalizeGraph<EntityType>()`.

**Key question:** When `NormalizeGraph<Person>()` is called and Person has `Address HomeAddress`:
- Container: `NormalizedPerson` (has `Name`, `Age`, `HomeAddressIndex`, etc. + `AddressList`)
- Per-type DTO for Address: `NormalizedAddress` (unchanged)
- Per-type DTO for Person: **not generated** (root type, per plan architecture)

This works for the non-self-referential case. But for `TreeNode`:
- Container: `NormalizedTreeNode` (has `Label`, `ParentIndex`, `ChildrenIndices`)
- All other TreeNode instances in the graph need to be stored... where? They have the same structure as the root's properties. Does the container also get a `TreeNodeList`?

### 3. Missing Wiring

**Sample Program.cs** (Task 7) requires far more changes than the plan indicates. The plan provides 8 lines of sample "after" code, but the actual Program.cs is 275 lines with extensive use of `NormalizedResult` APIs. Specific removals needed:

- Lines 52-55: `CollectionNames` iteration -- no equivalent
- Line 74: `RootIndex` -- no equivalent
- Lines 217-219: `CollectionNames` iteration -- no equivalent

**No issues found in:** (all categories had findings)
