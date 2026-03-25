namespace DataNormalizer.Attributes;

/// <summary>
/// Overrides the JSON property name for a property in the generated normalized DTO.
/// This is a syntactic marker — the source generator reads it via Roslyn syntax analysis.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = false)]
public sealed class NormalizeJsonNameAttribute : Attribute
{
    /// <summary>
    /// Gets the custom JSON property name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="NormalizeJsonNameAttribute"/> class.
    /// </summary>
    /// <param name="name">The custom JSON property name to use.</param>
    public NormalizeJsonNameAttribute(string name) => Name = name;
}
