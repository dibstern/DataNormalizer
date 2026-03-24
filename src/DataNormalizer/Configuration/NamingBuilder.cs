namespace DataNormalizer.Configuration;

/// <summary>
/// Configures naming conventions for generated DTOs and containers.
/// </summary>
public sealed class NamingBuilder
{
    /// <summary>
    /// Gets or sets the prefix prepended to generated DTO type names. Default is <c>""</c>.
    /// </summary>
    public string DtoPrefix { get; set; } = "";

    /// <summary>
    /// Gets or sets the suffix appended to generated DTO type names. Default is <c>"Dto"</c>.
    /// </summary>
    public string DtoSuffix { get; set; } = "Dto";

    /// <summary>
    /// Gets or sets the suffix appended to the generated container type name. Default is <c>"Dto"</c>.
    /// </summary>
    public string ContainerSuffix { get; set; } = "Dto";

    /// <summary>
    /// Gets or sets whether to emit <c>[JsonPropertyName]</c> attributes on generated properties.
    /// Default is <c>true</c>.
    /// </summary>
    public bool EmitJsonPropertyNames { get; set; } = true;
}
