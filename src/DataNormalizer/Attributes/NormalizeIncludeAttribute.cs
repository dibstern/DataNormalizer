namespace DataNormalizer.Attributes;

/// <summary>
/// Marks a property for explicit inclusion when using
/// <see cref="DataNormalizer.Configuration.PropertyMode.ExplicitOnly"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = false)]
public sealed class NormalizeIncludeAttribute : Attribute;
