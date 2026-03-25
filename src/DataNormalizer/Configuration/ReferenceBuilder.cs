namespace DataNormalizer.Configuration;

/// <summary>
/// Configures a reference or reference-collection property.
/// This is a syntactic marker — the source generator reads it via Roslyn syntax analysis.
/// </summary>
public sealed class ReferenceBuilder
{
    /// <summary>
    /// Sets the JSON property name for the generated index property.
    /// </summary>
    /// <param name="jsonName">The JSON property name to use.</param>
    /// <returns>This builder instance for chaining.</returns>
    public ReferenceBuilder JsonName(string jsonName) => this;
}
