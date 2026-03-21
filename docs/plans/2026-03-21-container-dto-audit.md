# Container DTO Plan Audit Synthesis

Dispatched 5 auditors across 7 tasks. 

## Ask User (3)

### 1. All existing tests use entity-as-root, not wrapper DTOs
Every integration test config uses `NormalizeGraph<Person>()` where Person IS the entity type. The approved design assumes wrapper DTOs (`PeopleDto { Person[] People }`) where root properties dissolve into the container. When the root IS the entity, the container and per-type DTO would be the same class, causing recursive type references in entity lists (`NormalizedPerson[] PersonList` on `NormalizedPerson` itself). Need design decision.

### 2. Self-referencing root types (TreeNode)
Circular reference tests use `NormalizeGraph<TreeNode>()` where TreeNode has `List<TreeNode> Children`. The plan says to skip `NormalizeXxx()` helpers for root types, but self-referencing roots NEED that helper for recursive normalization.

### 3. Multi-root config — confirm separate containers per root
When config has multiple `NormalizeGraph<>()` calls, confirm each produces its own container type and Normalize/Denormalize pair.

## Amend Plan (20+)

### Architecture-level
- Root-as-entity: root won't be registered in NormalizationContext, so `GetCollection` for its entity list will be empty
- Skip-root-helper logic breaks self-referencing roots (TreeNode)
- `WithName()` custom key vs container property naming mismatch
- Container must use arrays (`Length`) but plan says Pass 1/2 are "unchanged" (`Count`)

### Code-level
- Missing null handling for inline root normalized/collection properties
- `GetCSharpTypeName` helper produces `"int??"` for nullable value types — remove it
- Tests use `TypeFullName = "System.String"` but analyzer produces `"string"`
- `Properties.Length == 0` skip in entity list loop contradicts test and semantics
- Missing `using NUnit.Framework;` in test file
- `containerHintName` undefined in Task 4 code
- Missing LINQ import in NormalizeGenerator
- Design doc says remove `CastingReadOnlyList<T>` but implementation plan keeps it
- Root reconstruction underspecified (7+ collection kinds, nullable properties)
- `CollectionNames` and `RootIndex` have no container equivalents
- 12+ NormalizedResult API references in samples not specifically addressed
- 8+ missing test scenarios across emitters

## Accept (10+)

Minor observations: duplicate `ToCamelCase` implementations, hardcoded generator version, empty-root edge case, pre-existing inconsistencies.
