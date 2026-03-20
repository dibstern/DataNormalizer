namespace DataNormalizer.Attributes;

/// <summary>
/// Marks a property to be excluded from the generated normalized DTO.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = false)]
public sealed class NormalizeIgnoreAttribute : Attribute;
