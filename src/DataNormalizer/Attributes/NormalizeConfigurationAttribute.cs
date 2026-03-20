namespace DataNormalizer.Attributes;

/// <summary>
/// Marks a <see cref="DataNormalizer.Configuration.NormalizationConfig"/> subclass for source
/// generator processing. The class must be <c>partial</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class NormalizeConfigurationAttribute : Attribute;
