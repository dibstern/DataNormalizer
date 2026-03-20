namespace DataNormalizer.Configuration;

/// <summary>
/// Base class for normalization configurations. Subclass this, apply
/// <see cref="DataNormalizer.Attributes.NormalizeConfigurationAttribute"/>,
/// and override <see cref="Configure"/> to define your normalization graph.
/// </summary>
public abstract class NormalizationConfig
{
    /// <summary>
    /// Override this method to define the normalization graph and type-level configurations.
    /// </summary>
    /// <param name="builder">The builder used to register root types and configure property behavior.</param>
    protected abstract void Configure(NormalizeBuilder builder);
}
