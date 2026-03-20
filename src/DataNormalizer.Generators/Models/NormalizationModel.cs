using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace DataNormalizer.Generators.Models;

internal sealed class NormalizationModel
{
    public string ConfigClassName { get; init; } = "";
    public string ConfigNamespace { get; init; } = "";
    public ImmutableArray<RootTypeInfo> RootTypes { get; init; } = ImmutableArray<RootTypeInfo>.Empty;

    public ImmutableDictionary<string, TypeConfiguration> TypeConfigurations { get; init; } =
        ImmutableDictionary<string, TypeConfiguration>.Empty;

    public ImmutableHashSet<string> InlinedTypes { get; init; } = ImmutableHashSet<string>.Empty;
    public ImmutableHashSet<string> ExplicitTypes { get; init; } = ImmutableHashSet<string>.Empty;
    public bool CopySourceAttributes { get; init; }
    public string? JsonNamingPolicy { get; init; }
    public bool AutoDiscover { get; init; } = true;
    public bool UseReferenceTrackingForCycles { get; init; }
}

internal sealed class RootTypeInfo
{
    public required INamedTypeSymbol TypeSymbol { get; init; }
    public required string FullyQualifiedName { get; init; }
}
