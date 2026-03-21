# Task 2: Modify NormalizerEmitter to return container -- Audit Report

**Summary:** Task 2 has several significant issues: the plan's approach to skipping root helpers is underspecified for the self-referencing root case, the entity list population code uses a type key that may not match when `WithName()` is configured, the plan's inline root normalization pseudocode targets `result.{prop}` but the existing emitter methods all target `dto.{prop}`, and test coverage for the new behavior has critical gaps. The plan needs amendments before implementation.

---

## Findings

### Finding 1: Self-referencing root type breaks "skip root helper" logic
**Category:** Incorrect Code / Implicit Assumptions
**Action:** Amend Plan
**Details:**
The plan says (line 466): *"Also modify `Emit()` to **skip** emitting a `NormalizeXxx` helper for root type nodes."*

However, a root type can reference itself (e.g., `TreeNode` with a `Parent: TreeNode` property). In that case, the root type's `NormalizeXxx` helper is needed -- it will be called recursively from the inline root normalization code (e.g., `result.ParentIndex = NormalizeTreeNode(source.Parent, context)`). The root type can also appear as a referenced type from other non-root types.

Looking at the existing tests, `Emit_CircularReference_GeneratesTwoDtoPattern` (line 160) tests `TreeNode` as a root type with self-referencing properties. If we skip emitting `NormalizeTreeNode`, the generated code would call a method that doesn't exist.

More subtly, even in the design doc's `PeopleDto` example, the root type `PeopleDto` has a `Person[] People` collection. The root's inline normalization calls `NormalizePerson(...)`, which is a non-root helper. But if the root type IS the entity (e.g., `NormalizeGraph<Person>()` where Person references itself), then `NormalizePerson` would be the root helper and would be skipped -- breaking the code.

**Recommendation:** Amend the plan: Do NOT skip the root type's helper method unconditionally. Instead:
- Always emit the `NormalizeXxx` helper for every type (including root types) -- the helper is still needed for deduplication/indexing when the root type appears in collections or self-references.
- Only skip the root helper if the root type has NO properties of kind `Normalized` or `Collection` that reference itself AND no other type in the graph references the root type. This is complex enough that the simplest correct approach is: **always emit the helper, but inline the root's property normalization in the public method anyway** (the inline code calls the helpers, it doesn't replace them).

Alternatively: the root type's helper IS still needed to add root entities into the context's collection (for entity list population). The inline normalization in the public method handles root-level properties, but the root type may still appear in other types' properties. The safest approach: keep emitting all helpers; do not skip root.

---

### Finding 2: Entity list population uses `TypeName` but context uses `typeKey` (custom name mismatch)
**Category:** Incorrect Code
**Action:** Amend Plan
**Details:**
The plan's Step 3 pseudocode (line 463) for populating entity lists says:
```
context.GetCollection<{DtoFullName}>("{typeKey}")
```

But it also says the entity list property is named `{TypeName}List`. The `typeKey` in the normalizer uses `GetTypeKey()` (NormalizerEmitter.cs:449-457), which returns `config.CustomName` if set via `WithName()`, falling back to `node.TypeName`.

So when `WithName("People")` is configured for a `Person` type:
- The context stores the collection under key `"People"` (custom name)
- The entity list property on the container is `PersonList` (uses `TypeName`)

This means `context.GetCollection<NormalizedPerson>("People")` is correct for retrieval, but the assignment target `result.PersonList` uses `TypeName`, not the custom name.

Looking at the design doc (line 78): *"Always `{TypeName}List`. The `WithName("custom")` configuration overrides `TypeName`."* -- This is ambiguous. Does `WithName` override the entity list property name too? The ContainerEmitter (Task 1) uses `node.TypeName` for the list property name (line 381: `var listPropName = $"{node.TypeName}List";`). If `WithName` should override this, the ContainerEmitter also needs changing.

The key issue: the `typeKey` passed to `GetCollection` must match what the `NormalizeXxx` helpers use when calling `GetOrAddIndexAndStore`. Currently, helpers use `GetTypeKey(node, model)` which respects `CustomName`. The entity list population must use the same key for `GetCollection`. But the *property name* on the container (`PersonList` vs `PeopleList`) is a ContainerEmitter concern.

**Recommendation:** Amend Plan: Ensure the entity list population code uses `GetTypeKey(node, model)` for the `GetCollection` call (not `node.TypeName`). Clarify whether `WithName()` affects the container property name (which would require ContainerEmitter changes in Task 1 as well). The plan should explicitly state:
- `context.GetCollection<{DtoFullName}>("{typeKey}")` where `typeKey = GetTypeKey(node, model)` -- this is correct in the pseudocode but needs to be explicitly wired to the existing `GetTypeKey` method.

---

### Finding 3: Emit() signature change not specified -- needs `model` parameter for root type identification
**Category:** Missing Wiring
**Action:** Amend Plan
**Details:**
The current `NormalizerEmitter.Emit()` signature (line 9) is:
```csharp
public static string Emit(NormalizationModel model, IReadOnlyList<TypeGraphNode> allNodes)
```

The plan says (line 466): *"Add a `rootTypeFullNames` set parameter or check against `model.RootTypes` when iterating `allNodes`."*

The `NormalizationModel` already has `RootTypes`, so the emitter can build a `HashSet<string>` of root type full names from `model.RootTypes.Select(r => r.FullyQualifiedName)`. The plan should explicitly say to use `model.RootTypes` (which is already available) rather than suggesting a new parameter. Currently `EmitNormalizeHelper` iterates all nodes -- the plan needs to specify whether to skip root nodes in that loop or to keep them (see Finding 1).

**Recommendation:** Amend plan to be explicit: build `var rootTypeFullNames = new HashSet<string>(model.RootTypes.Select(r => r.FullyQualifiedName))` at the top of `Emit()`, and use it when determining entity list population (non-root nodes only). Do NOT use it to skip helper emission (per Finding 1).

---

### Finding 4: Inline root normalization doesn't handle `Inlined` properties explicitly
**Category:** Incorrect Code
**Action:** Amend Plan
**Details:**
The plan's Step 3 (line 458-461) describes inline root property normalization patterns:
- Simple/Inlined: `result.Name = source.Name;`
- Normalized: `result.{Name}Index = Normalize{Type}(source.{Name}, context);`
- Collection: for-loop building `int[]`, assigning to `result.{Name}Indices`

This is correct -- `Inlined` is listed alongside `Simple`. However, looking at the ContainerEmitter code from Task 1 (line 346-348), the `Inlined` case emits the property with the same type, but the plan doesn't verify that the ContainerEmitter actually handles `Inlined` properties (it does -- line 346-351 of the plan's ContainerEmitter shows `case PropertyKind.Inlined:` falling through to the same `Simple` logic).

The inline normalization for the root in the `EmitPublicNormalizeMethod` replacement must handle all 4 `PropertyKind` values. The plan's pseudocode lists 3 patterns (Simple/Inlined, Normalized, Collection) which covers all 4 enum values. This is actually correct.

**Recommendation:** Accept -- the plan does cover all 4 `PropertyKind` values. Worth noting that the implementer should handle the `switch` exhaustively (with a `default` case for future-proofing).

---

### Finding 5: Entity list population uses `context.GetCollection` which returns `IReadOnlyList<T>`, not indexable by `[i]`
**Category:** Incorrect Code
**Action:** Amend Plan
**Details:**
The plan's pseudocode for entity list population (line 463):
```
var __{camel}Col = context.GetCollection<{DtoFullName}>("{typeKey}");
var __{camel}Arr = new {DtoFullName}[__{camel}Col.Count];
for (var __i = 0; __i < __{camel}Col.Count; __i++)
    __{camel}Arr[__i] = __{camel}Col[__i];
result.{TypeName}List = __{camel}Arr;
```

`NormalizationContext.GetCollection<T>()` (line 167-169 of NormalizationContext.cs) returns `IReadOnlyList<T>`, which DOES support `Count` and `[i]` indexer. The `CastingReadOnlyList<T>` inner class (line 206-224) implements `IReadOnlyList<T>`. So this code is actually correct.

However, there's a simpler approach: since `IReadOnlyList<T>` supports LINQ's `ToArray()` via `System.Linq`, or can be manually copied. The plan's approach of manual for-loop is fine (avoids LINQ dependency in generated code).

**Recommendation:** Accept -- the code is correct. The manual for-loop is consistent with the existing pattern of avoiding LINQ in generated code.

---

### Finding 6: Root type IS in `allNodes` -- entity list population must exclude it
**Category:** Incorrect Code
**Action:** Amend Plan
**Details:**
Looking at `NormalizeGenerator.TransformConfig()` (lines 70-93), the root type IS included in `allNodes` -- the `TypeGraphAnalyzer.Analyze()` returns nodes including the root type itself, and it gets added to `allNodes`.

The plan says to populate entity lists "for each non-root node" (line 463). This is correct in intent. But the plan's pseudocode doesn't show the filtering logic. When iterating `allNodes` in the modified `EmitPublicNormalizeMethod`, the code must skip nodes whose `TypeFullName` matches a root type.

However, there's a subtlety: in the `PeopleDto` example from the design doc, `PeopleDto` is the root type but `Person` and `Address` are non-root. The root type `PeopleDto` will be in `allNodes`. So entity lists should be emitted for `Person` and `Address` but NOT for `PeopleDto`.

But wait -- what if `NormalizeGraph<Person>()` is called, and `Person` is both the root AND an entity type? In this case:
- Root inline normalization handles `Person`'s own properties
- But `Person` still needs to be in the entity list (because `Person` objects reference other `Person` objects, or the root's own normalization puts `Person` instances into the context)

Actually, looking at the design doc example more carefully: the root type `PeopleDto` has `Person[] People`, and the normalizer inlines `PeopleDto`'s properties. `Person` entities go into `PersonList`. But what happens with `NormalizeGraph<Person>()` where `Person` is the root AND an entity?

In that case, the inline root normalization would call `NormalizePerson(source.SomeProp, context)` for Person's Normalized properties. But the root `Person` itself is NOT added to the context's collection (since we're inlining, not calling `NormalizePerson(source, context)` for the root).

This means: if `Person` is both root and entity, and another `Person` references the root `Person`, the root won't be in the context's `Person` collection -- leading to missing data in `PersonList`.

**Recommendation:** Amend Plan: The plan must clarify the case where root type IS also an entity type (i.e., the root type appears in `allNodes` AND other types reference it). Options:
1. Still call `NormalizeRootType(source, context)` to register the root in the context, then extract root properties from the context (undoes the "inline" approach for this case)
2. Manually add the root to the context before inline normalization
3. Treat this as a separate case: if root type has references to itself, emit entity list for it too and call the helper

The plan should address: "When root type IS the entity type, do we include it in entity lists? If yes, how does the root get into the context?"

---

### Finding 7: `allNodes.Count` used for `typeCount` no longer includes root type's contribution
**Category:** Implicit Assumptions
**Action:** Amend Plan
**Details:**
Currently (NormalizerEmitter.cs:72):
```csharp
sb.AppendLine($"        var context = new DataNormalizer.Runtime.NormalizationContext({typeCount});");
```
Where `typeCount` is `allNodes.Count` (passed from line 37). This pre-allocates dictionaries.

After the change, if the root type is excluded from helpers, the context still needs capacity for all type keys that will be used (including the root type if it's also an entity that goes into the context). The `typeCount` should remain `allNodes.Count` regardless of whether we skip root helpers.

**Recommendation:** Accept -- the `typeCount` parameter to `NormalizationContext` is just a capacity hint. Even if slightly off, it won't cause bugs. The plan doesn't explicitly change this, which is fine.

---

### Finding 8: Plan's pseudocode for inline root normalization of `Collection` properties doesn't handle null collections
**Category:** Incorrect Code
**Action:** Amend Plan
**Details:**
The plan says (line 461): *"Collection: for-loop building `int[]`, assigning to `result.{Name}Indices`"*

But the existing `EmitCollectionForLoop` (NormalizerEmitter.cs:372-416) has null-safety: it checks `if ({sourceExpr} is null)` and assigns `System.Array.Empty<int>()`. The plan's terse pseudocode doesn't mention this. When implementing the inline root normalization, the implementer must include the null-safety pattern from the existing `EmitCollectionForLoop`.

Moreover, the existing method also handles non-indexable collections (HashSet, IEnumerable) with a different code path using `foreach` + `List<int>`. The inline root normalization for collections should reuse or call the existing `EmitCollectionForLoop` method rather than reimplementing it.

**Recommendation:** Amend Plan: Explicitly state that inline root collection normalization should reuse the existing `EmitCollectionForLoop` method (passing `result.{Name}Indices` as the target expression), not reimplement it. This ensures null-safety and non-indexable collection support are preserved.

---

### Finding 9: Inline root normalization of `Normalized` properties doesn't handle nullable
**Category:** Incorrect Code
**Action:** Amend Plan
**Details:**
The plan says (line 460): *"Normalized: `result.{Name}Index = Normalize{Type}(source.{Name}, context);`"*

But the existing `EmitNormalizedPropertyAssignment` (NormalizerEmitter.cs:253-268) handles nullable properties:
```csharp
if (prop.IsNullable)
    dto.{indexPropName} = source.{prop.Name} is null ? (int?)null : Normalize{nestedTypeName}(source.{prop.Name}, context);
else
    dto.{indexPropName} = Normalize{nestedTypeName}(source.{prop.Name}, context);
```

The plan's pseudocode omits the nullable case for inline root normalization. The root type can have nullable normalized properties (e.g., `OrderDto.ShippingAddress?`).

**Recommendation:** Amend Plan: Explicitly state that inline root normalization of `Normalized` properties must handle nullable using the same pattern as `EmitNormalizedPropertyAssignment`. Better yet, refactor to share code: extract the emit logic into a reusable method that takes the target prefix (`result.` vs `dto.`).

---

### Finding 10: Test coverage gaps -- missing tests for key scenarios
**Category:** Insufficient Test Coverage
**Action:** Amend Plan
**Details:**
The plan's Step 1 (line 436-443) says to "update assertions" but provides no specific test cases for the new behavior. Key scenarios that need explicit test coverage:

1. **Self-referencing root type** (e.g., `TreeNode` as root with `Parent: TreeNode`): The root's inline normalization calls `NormalizeTreeNode` for the Parent property. The helper must still be emitted. Current test `Emit_CircularReference_GeneratesTwoDtoPattern` uses TreeNode as root -- assertions need updating.

2. **Root type that is also an entity**: `NormalizeGraph<Person>()` where Person has nested Person references. Entity list must include the root type's entities.

3. **Root with nullable normalized property**: The root has `Address? ShippingAddress`. The inline normalization must emit `result.ShippingAddressIndex = source.ShippingAddress is null ? (int?)null : NormalizeAddress(...)`.

4. **Root with nullable collection property**: The root has `Person[]? People`. The inline normalization must handle null.

5. **Multiple roots**: Each root gets its own container type and `Normalize()` overload. The entity list population code must be correct for each.

6. **Root with custom type name (`WithName`)**: Ensure `GetCollection` calls use the custom name while property names on the container use `TypeName`.

7. **Empty root (no properties)**: Current code (NormalizerEmitter.cs:30) checks `if (rootNode != null)` but doesn't check for empty properties. The plan should verify this edge case.

8. **Root with only simple properties (no entities)**: Container has root properties but empty entity lists. Context should still work.

**Recommendation:** Amend Plan: Add explicit test case descriptions for at minimum scenarios 1, 3, 5, and 6. The existing test `Emit_PublicNormalizeMethodSignature_HasCorrectReturnTypeAndParameter` (line 351) needs major assertion changes and should be the template for the new behavior. The plan should list the specific assertion changes for each existing test.

---

### Finding 11: Plan doesn't specify how to compute container name/full name in EmitPublicNormalizeMethod
**Category:** Missing Wiring
**Action:** Amend Plan
**Details:**
The plan says (line 455): *"Declare return type as `{containerFullName}`"*. But the NormalizerEmitter currently computes `dtoFullName` using `GetDtoFullName(rootType.FullyQualifiedName, rootNode.TypeName)` which produces `{Namespace}.Normalized{TypeName}`.

The container name in Task 1's ContainerEmitter is also `Normalized{TypeName}` (line 305 of the plan). So the container's full name IS the same as what `GetDtoFullName` would produce. This is fine for the common case.

But this means the container class and the root type's per-type DTO would have the same name (`NormalizedPerson`). Task 4 says to skip emitting per-type DTOs for root types. But if Task 2 runs before Task 4, there will be a name collision between the container and the per-type DTO for the root type. The plan's task ordering assumes sequential execution where Task 4 handles the DTO skip.

**Recommendation:** Accept -- the plan's task ordering (Task 1 -> 2 -> 3 -> 4) means Tasks 2-3 may produce code that won't fully compile until Task 4 removes the root type's per-type DTO. This is acceptable if the implementer runs tests only within each task's scope. Worth noting.

---

### Finding 12: Plan doesn't address `Inlined` property kind for root's `Normalized` sub-properties
**Category:** Implicit Assumptions
**Action:** Accept
**Details:**
If the root type has a property of kind `Inlined` (meaning it's a complex type whose properties are kept as-is, not normalized), the inline root normalization just copies it: `result.{Name} = source.{Name}`. This is correctly covered by the plan's "Simple/Inlined" pattern (line 459). The `Inlined` kind is just a pass-through, same as `Simple`.

**Recommendation:** Accept -- this is handled correctly.

---

## Summary Table

| # | Category | Action | Issue | File:Line | Amendment / Question |
|---|----------|--------|-------|-----------|----------------------|
| 1 | Incorrect Code / Implicit Assumptions | Amend Plan | Self-referencing root type breaks "skip root helper" logic | NormalizerEmitter.cs:42-51 | Do NOT skip root type helpers; always emit them. Only inline root property handling in the public method. |
| 2 | Incorrect Code | Amend Plan | Entity list population may use wrong type key with `WithName()` | NormalizerEmitter.cs:449-457 | Explicitly use `GetTypeKey(node, model)` for `GetCollection` calls in entity list population |
| 3 | Missing Wiring | Amend Plan | Plan suggests new parameter but `model.RootTypes` already available | NormalizerEmitter.cs:9 | Use `model.RootTypes` to build root set; don't add new parameter |
| 4 | Incorrect Code | Accept | Inlined properties covered by plan's pseudocode | - | Add exhaustive switch/default for future-proofing |
| 5 | Incorrect Code | Accept | `GetCollection` returns `IReadOnlyList<T>` which supports indexing | NormalizationContext.cs:167-169 | Code is correct |
| 6 | Incorrect Code | Amend Plan | Root type IS in allNodes; when root is also entity, root won't be in context | NormalizeGenerator.cs:70-93 | Clarify how root-as-entity case works: either call root helper to register in context, or manually add |
| 7 | Implicit Assumptions | Accept | `typeCount` capacity hint still works | NormalizerEmitter.cs:72 | No change needed |
| 8 | Incorrect Code | Amend Plan | Inline root collection normalization must handle null + non-indexable collections | NormalizerEmitter.cs:372-416 | Reuse `EmitCollectionForLoop` for inline root collection normalization |
| 9 | Incorrect Code | Amend Plan | Inline root normalized property must handle nullable case | NormalizerEmitter.cs:253-268 | Handle nullable in inline root normalization; reuse existing emit methods |
| 10 | Insufficient Test Coverage | Amend Plan | Missing tests for self-ref root, nullable root props, multi-root, custom names | NormalizerEmitterTests.cs | Add 4+ explicit test scenarios for new container behavior |
| 11 | Missing Wiring | Accept | Container name collision with per-type DTO until Task 4 | - | Task ordering dependency; acceptable |
| 12 | Implicit Assumptions | Accept | Inlined kind for root properties | - | Handled correctly |

**No issues found in:** (all assigned categories had findings)

**Critical items requiring plan amendment before implementation:**
1. **Finding 1** (don't skip root helpers) and **Finding 6** (root-as-entity) are the most architecturally significant -- getting these wrong would produce broken generated code.
2. **Finding 8** and **Finding 9** (null handling) would cause runtime NullReferenceExceptions in generated code.
3. **Finding 10** (test gaps) means bugs from 1/6/8/9 might not be caught during TDD.
