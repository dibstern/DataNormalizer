# Diagnostics Reference

DataNormalizer emits compiler diagnostics to help you identify configuration issues at build time. All diagnostics use the `DN` prefix.

## Diagnostics table

| ID     | Severity | Description                          | Resolution                                               |
| ------ | -------- | ------------------------------------ | -------------------------------------------------------- |
| DN0001 | Warning  | Circular reference detected          | Add `<NoWarn>DN0001</NoWarn>` if intentional             |
| DN0002 | Error    | Configuration class must be `partial` | Add the `partial` keyword to the class declaration       |
| DN0003 | Error    | Type has no public properties        | Add public properties or exclude the type                |
| DN0004 | Info     | Unmapped complex type will be inlined | Use `graph.Inline<T>()` explicitly, or add to the graph  |

## DN0001 — Circular reference detected

**Severity:** Warning

The source generator detected a cycle in your type graph. For example, `Person` references `Company` which references `Person`.

**What happens:** Normalization still works correctly. The generator uses value-equality-based deduplication to handle cycles, and denormalization uses a two-pass approach (create all objects first, then resolve references).

**Resolution:** If the cycle is intentional, suppress the warning in your `.csproj`:

```xml
<PropertyGroup>
    <NoWarn>$(NoWarn);DN0001</NoWarn>
</PropertyGroup>
```

## DN0002 — Configuration class must be `partial`

**Severity:** Error

The class marked with `[NormalizeConfiguration]` is missing the `partial` keyword. The source generator needs to add methods to this class, which requires it to be `partial`.

**Resolution:** Add `partial` to the class declaration:

```csharp
// Before (error)
[NormalizeConfiguration]
public class AppNormalization : NormalizationConfig { ... }

// After (fixed)
[NormalizeConfiguration]
public partial class AppNormalization : NormalizationConfig { ... }
```

## DN0003 — Type has no public properties

**Severity:** Error

A type in the normalization graph has no public properties. The generator cannot create a meaningful normalized DTO for it.

**Resolution:** Either add public properties to the type, or exclude it from the graph entirely (e.g., by not referencing it from other types, or by inlining it).

## DN0004 — Unmapped complex type will be inlined

**Severity:** Info

A complex type was discovered in the graph but was not explicitly registered with `NormalizeGraph<T>()`. The generator will inline it (keep it nested) rather than extracting it into a separate collection.

**Resolution:** If you want the type extracted into its own collection, add it to the graph. If you want it inlined, you can make this explicit to suppress the diagnostic:

```csharp
builder.NormalizeGraph<Person>(graph =>
{
    graph.Inline<Metadata>(); // explicitly inline
});
```

## Known constraints

### Circular reference deduplication

For circular types, back-edge properties (those creating the cycle) use shape-based comparison (null/non-null, collection count) rather than full structural comparison. All non-circular properties — including nested complex subtrees — are fully compared.

False deduplication only occurs if two objects in a cycle have identical simple properties, identical non-circular subtree structure, AND identical circular reference shapes. In practice this is rare, but worth being aware of when working with deeply circular graphs.
