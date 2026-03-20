using System.Collections.Immutable;

namespace DataNormalizer.Generators.Models;

internal sealed class AnalyzedProperty
{
    public required string Name { get; init; }
    public required string TypeFullName { get; init; }
    public required PropertyKind Kind { get; init; }
    public bool IsNullable { get; init; }
    public bool IsCollection { get; init; }
    public string? CollectionElementTypeFullName { get; init; }
    public bool IsCircularReference { get; init; }
    public CollectionTypeKind CollectionKind { get; init; }
    public bool IsReferenceType { get; init; }
    public ImmutableArray<string> SourceAttributes { get; init; } = ImmutableArray<string>.Empty;
}
