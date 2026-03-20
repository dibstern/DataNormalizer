namespace DataNormalizer.Configuration;

/// <summary>
/// Controls which properties are included in the generated DTO.
/// </summary>
public enum PropertyMode
{
    /// <summary>
    /// All public properties are included. Use <c>IgnoreProperty</c> to exclude specific ones.
    /// </summary>
    IncludeAll = 0,

    /// <summary>
    /// Only properties explicitly added via <c>IncludeProperty</c> or
    /// <see cref="DataNormalizer.Attributes.NormalizeIncludeAttribute"/> are included.
    /// </summary>
    ExplicitOnly = 1,
}
