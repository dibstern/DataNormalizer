using System.Collections.Immutable;

namespace DataNormalizer.Generators.Models;

internal sealed class TypeGraphNode
{
    public required string TypeFullName { get; init; }
    public required string TypeName { get; init; }
    public required ImmutableArray<AnalyzedProperty> Properties { get; init; }
    public bool HasCircularReference { get; init; }
}
