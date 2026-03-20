using System.Collections.Immutable;

namespace DataNormalizer.Generators.Models;

internal sealed class TypeConfiguration
{
    public required string FullyQualifiedName { get; init; }
    public ImmutableHashSet<string> IgnoredProperties { get; init; } = ImmutableHashSet<string>.Empty;
    public ImmutableHashSet<string> IncludedProperties { get; init; } = ImmutableHashSet<string>.Empty;
    public ImmutableHashSet<string> NormalizedProperties { get; init; } = ImmutableHashSet<string>.Empty;
    public ImmutableHashSet<string> InlinedProperties { get; init; } = ImmutableHashSet<string>.Empty;
    public string? CustomName { get; init; }
    public GeneratorPropertyMode? PropertyMode { get; init; }
}

internal enum GeneratorPropertyMode
{
    IncludeAll = 0,
    ExplicitOnly = 1,
}
