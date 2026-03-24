using System;

namespace DataNormalizer.Generators.Models;

internal sealed class NamingModel : IEquatable<NamingModel>
{
    public string DtoPrefix { get; init; } = "";
    public string DtoSuffix { get; init; } = "Dto";
    public string ContainerSuffix { get; init; } = "Dto";
    public bool EmitJsonPropertyNames { get; init; } = true;

    public bool Equals(NamingModel? other)
    {
        if (other is null)
            return false;
        if (ReferenceEquals(this, other))
            return true;
        return DtoPrefix == other.DtoPrefix
            && DtoSuffix == other.DtoSuffix
            && ContainerSuffix == other.ContainerSuffix
            && EmitJsonPropertyNames == other.EmitJsonPropertyNames;
    }

    public override bool Equals(object? obj) => obj is NamingModel other && Equals(other);

    public override int GetHashCode()
    {
        var hash = 17;
        hash = (hash * 397) ^ (DtoPrefix?.GetHashCode() ?? 0);
        hash = (hash * 397) ^ (DtoSuffix?.GetHashCode() ?? 0);
        hash = (hash * 397) ^ (ContainerSuffix?.GetHashCode() ?? 0);
        hash = (hash * 397) ^ EmitJsonPropertyNames.GetHashCode();
        return hash;
    }

    public static NamingModel Default { get; } = new();
}
