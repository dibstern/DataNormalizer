namespace DataNormalizer.Generators.Models;

internal enum CollectionTypeKind
{
    None = 0,
    List,
    Array,
    IReadOnlyList,
    ICollection,
    IList,
    IEnumerable,
    HashSet,
    ImmutableList,
    ImmutableArray,
}
