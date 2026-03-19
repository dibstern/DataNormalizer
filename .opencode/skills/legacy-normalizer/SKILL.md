---
name: legacy-normalizer
description: Documents the original expression tree normalization library (in data-normalization/ sibling directory). Architecture reference for understanding the design lineage, hash-based dedup, DFS type traversal, deferred serialization, and why the new library uses equality-based dedup instead.
---

# Legacy Normalizer Architecture Reference

## Overview

The original normalization library (located in the `data-normalization/` sibling directory) uses runtime expression trees to build hash functions and serializers at startup. DataNormalizer is a ground-up rewrite that replaces this approach with compile-time source generation.

This document serves as an architectural reference for understanding the design lineage and the improvements made in the new library.

## Core Architecture

### ObjectNormalizer

The central class. Uses `System.Linq.Expressions` to dynamically compile:
- **Hash functions**: `Func<T, int>` — compute a hash code from an object's properties
- **Serializers**: `Func<T, Dictionary<string, object>>` — flatten an object to a property dictionary

These are compiled once at startup and cached per type.

### Compile-Once-Run-Many Philosophy

```
Startup:
  1. Analyze type graph (reflection)
  2. Build expression trees for hash + serialize
  3. Compile to delegates (Expression.Compile())
  4. Cache delegates in dictionary

Runtime (per operation):
  1. Look up cached delegate
  2. Invoke delegate (no reflection)
  3. Hash → dedup → serialize
```

This compile-once-run-many pattern is the core insight. DataNormalizer preserves this philosophy but moves the "compile" step to build time via source generation — eliminating even the startup cost.

### Type Graph Traversal

**DFS, leaf-first (post-order)** via `GetAllTypesDepthFirst`:

```
Person → Address → GeoPosition (leaf, processed first)
                → Country (leaf, processed)
       → PhoneNumber (leaf, processed)
Person processed last (depends on all nested types)
```

The traversal uses reflection (`Type.GetProperties()`) to discover the object graph. Each type in the graph gets its own hash function and serializer.

DataNormalizer replicates this traversal at compile time using `INamedTypeSymbol` and Roslyn semantic analysis instead of reflection.

## Hash Formula

Rolling hash with prime multiplication:

```
hashCode = (hashCode * 397) ^ propertyValue.GetHashCode()
```

For each property of a type:
1. Multiply current hash by 397 (prime)
2. XOR with the property value's hash code
3. For null values: XOR with 0
4. For nested objects: XOR with the recursively computed hash

### Expression Tree for Hash

```csharp
// Conceptually generates:
Func<Person, int> hashPerson = person =>
{
    int hash = 0;
    hash = (hash * 397) ^ (person.Name?.GetHashCode() ?? 0);
    hash = (hash * 397) ^ person.Age.GetHashCode();
    hash = (hash * 397) ^ hashAddress(person.HomeAddress);  // recursive
    return hash;
};
```

## Deduplication

### Hash-Based (Original)

```
hash → index mapping in Dictionary<int, int>
```

1. Compute hash for object
2. Look up hash in dictionary
3. If found → return existing index (deduplicated)
4. If not found → assign new index, store hash → index

### CRITICAL DIFFERENCE: DataNormalizer Uses Equality-Based Dedup

The original library uses hash-only deduplication. This has a **collision bug**: two different objects with the same hash code are incorrectly treated as duplicates.

DataNormalizer fixes this by using `IEquatable<T>` equality for deduplication:

```csharp
// Original (hash-only, buggy):
Dictionary<int, int> hashToIndex;

// DataNormalizer (equality-based, correct):
Dictionary<TDto, int> dtoToIndex;  // where TDto : IEquatable<TDto>
```

The generated DTOs implement `IEquatable<T>` with property-by-property comparison, so deduplication is based on actual equality, not just hash codes.

## Circular Reference Handling

### Guards

Two parallel sets track objects currently being processed:

```csharp
HashSet<object> objectsStartedToHash;      // for hash computation
HashSet<object> objectsStartedToSerialize;  // for serialization
```

Before processing an object:
1. Check if it's in the "started" set
2. If yes → circular reference detected, skip or defer
3. If no → add to "started" set, process, then remove

### Deferred Serialization Queue

When a circular reference is detected during serialization:

```csharp
Queue<DeferredSerialization> deferredQueue;
```

1. Object A references Object B
2. Object B references Object A (circular!)
3. Object B's serialization of the A-property is deferred
4. After all direct serialization completes, process the deferred queue
5. By then, Object A is fully serialized and its index is known

DataNormalizer handles this differently: the `NormalizationContext.IsVisited`/`MarkVisited` pattern with `ReferenceEqualityComparer` breaks cycles during normalization. For denormalization, the two-pass approach (create all objects first, then resolve references) naturally handles cycles.

## Output Format

```csharp
Dictionary<string, List<Dictionary<string, object>>>
//         ^type      ^instances    ^properties
```

Example:
```json
{
  "Person": [
    { "Name": "Alice", "Age": 30, "HomeAddressIndex": 0 }
  ],
  "Address": [
    { "Street": "123 Main", "City": "Springfield" }
  ]
}
```

DataNormalizer replaces this with strongly-typed `NormalizedResult<T>` containing typed DTO collections, which provides compile-time safety and IntelliSense.

## NormalizerFactory and NormalizationContext

### NormalizerFactory

Pre-registers types and compiles hash/serialize delegates:

```csharp
var factory = new NormalizerFactory();
factory.Register<Person>();
factory.Register<Address>();
// Triggers expression tree compilation for all registered types
```

### NormalizationContext

Mutable state per normalization operation:

```csharp
var context = new NormalizationContext(factory);
context.Normalize(personInstance);
var result = context.GetResult();
// result is Dictionary<string, List<Dictionary<string, object>>>
```

DataNormalizer's `NormalizationContext` serves a similar purpose (tracking indices and dedup state) but is simpler because the generated code handles the type-specific logic.

## Key Differences: Legacy vs DataNormalizer

| Aspect | Legacy | DataNormalizer |
|--------|--------|---------------|
| Type discovery | Runtime reflection | Compile-time Roslyn analysis |
| Hash/serialize | Expression tree compilation | Source-generated code |
| Startup cost | High (compiles expression trees) | Zero (code is pre-generated) |
| Output type | `Dictionary<string, List<Dictionary<string, object>>>` | Strongly-typed `NormalizedResult<T>` |
| Deduplication | Hash-only (collision bug) | Equality-based (correct) |
| Circular refs | Deferred serialization queue | Visited set + two-pass denormalization |
| AOT support | No (dynamic code gen) | Yes (static code) |
| IntelliSense | No (dynamic types) | Yes (generated DTOs) |
| Configuration | Register types at runtime | Fluent builder parsed at compile time |

## Lessons from the Legacy Library

1. **DFS post-order traversal** is the correct order for processing type graphs (leaf types first, composite types last). DataNormalizer replicates this.

2. **The `(hash * 397) ^ value` formula** is efficient and well-distributed. DataNormalizer uses it in generated `GetHashCode()` methods.

3. **Compile-once-run-many** is the right philosophy. Source generation takes it further by moving compilation to build time.

4. **Circular references are real** in domain models (parent-child, bidirectional associations). Any normalization library must handle them.

5. **Hash-only dedup is insufficient** for correctness. Two objects with different property values can have the same hash code. Equality-based dedup is required.
