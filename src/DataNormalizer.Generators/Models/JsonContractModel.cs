using System;
using System.Collections.Immutable;

namespace DataNormalizer.Generators.Models;

internal sealed class JsonContractModel : IEquatable<JsonContractModel>
{
    public string? RootPropertyName { get; init; }

    public ImmutableDictionary<string, string> CollectionJsonNames { get; init; } =
        ImmutableDictionary<string, string>.Empty;

    public bool Equals(JsonContractModel? other)
    {
        if (other is null)
            return false;
        if (ReferenceEquals(this, other))
            return true;
        if (RootPropertyName != other.RootPropertyName)
            return false;
        if (CollectionJsonNames.Count != other.CollectionJsonNames.Count)
            return false;
        foreach (var kvp in CollectionJsonNames)
        {
            if (
                !other.CollectionJsonNames.TryGetValue(kvp.Key, out var otherVal)
                || kvp.Value != otherVal
            )
                return false;
        }
        return true;
    }

    public override bool Equals(object? obj) => obj is JsonContractModel other && Equals(other);

    public override int GetHashCode()
    {
        // Use XOR-based order-independent hashing for dictionary entries.
        // ImmutableDictionary iteration order is not contractually guaranteed,
        // so (hash * 397) ^ entry-by-entry would be order-dependent.
        var hash = RootPropertyName?.GetHashCode() ?? 0;
        var dictHash = 0;
        foreach (var kvp in CollectionJsonNames)
        {
            dictHash ^= kvp.Key.GetHashCode() ^ (kvp.Value.GetHashCode() * 397);
        }
        return (hash * 397) ^ dictHash;
    }

    public static JsonContractModel Default { get; } = new();
}
