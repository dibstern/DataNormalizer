namespace DataNormalizer.Configuration;

/// <summary>
/// Configures JSON contract customization for a normalization graph.
/// This is a syntactic marker — the source generator reads it via Roslyn syntax analysis.
/// </summary>
public sealed class JsonContractBuilder
{
    /// <summary>
    /// Gets or sets the JSON property name for the root object in the normalized container.
    /// </summary>
    public string? RootPropertyName { get; set; }

    /// <summary>
    /// Declares a collection of the specified type with a custom JSON property name.
    /// </summary>
    /// <typeparam name="T">The element type of the collection.</typeparam>
    /// <param name="jsonName">The JSON property name for this collection.</param>
    public void Collection<T>(string jsonName) { }
}
