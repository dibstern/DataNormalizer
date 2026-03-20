namespace DataNormalizer.Configuration;

/// <summary>
/// Configures graph-level behavior for a normalization root.
/// </summary>
/// <typeparam name="T">The root type of the normalization graph.</typeparam>
public sealed class GraphBuilder<T>
    where T : class
{
    /// <summary>
    /// Enables auto-discovery of referenced types (default behavior).
    /// </summary>
    /// <returns>This builder instance for chaining.</returns>
    public GraphBuilder<T> AutoDiscover() => this;

    /// <summary>
    /// Marks a type to be kept inline (not normalized into a separate collection).
    /// </summary>
    /// <typeparam name="TInline">The type to keep inline.</typeparam>
    /// <returns>This builder instance for chaining.</returns>
    public GraphBuilder<T> Inline<TInline>()
        where TInline : class => this;

    /// <summary>
    /// Copies serialization attributes from source types to generated DTOs.
    /// (v1: not yet implemented)
    /// </summary>
    /// <returns>This builder instance for chaining.</returns>
    public GraphBuilder<T> CopySourceAttributes() => this;

    /// <summary>
    /// Sets the property inclusion mode for types in this graph.
    /// </summary>
    /// <param name="mode">The property mode to use.</param>
    /// <returns>This builder instance for chaining.</returns>
    public GraphBuilder<T> UsePropertyMode(PropertyMode mode) => this;

    /// <summary>
    /// Configures property-level behavior for a specific type within this graph.
    /// </summary>
    /// <typeparam name="TType">The type to configure.</typeparam>
    /// <param name="configure">An action to configure the type builder.</param>
    /// <returns>This builder instance for chaining.</returns>
    public GraphBuilder<T> ForType<TType>(Action<TypeBuilder<TType>> configure)
        where TType : class
    {
        configure(new TypeBuilder<TType>());
        return this;
    }

    /// <summary>
    /// Sets JSON naming convention for generated properties.
    /// (v1: not yet implemented)
    /// </summary>
    /// <param name="namingPolicy">The JSON naming policy to apply.</param>
    /// <returns>This builder instance for chaining.</returns>
    public GraphBuilder<T> UseJsonNaming(System.Text.Json.JsonNamingPolicy namingPolicy) => this;

    /// <summary>
    /// Enables reference-based tracking for cycle detection. When enabled, the normalizer
    /// uses source object identity (reference equality) to detect cycles, providing perfect
    /// accuracy at the cost of requiring in-memory object references.
    /// </summary>
    /// <returns>This builder instance for chaining.</returns>
    public GraphBuilder<T> UseReferenceTrackingForCycles() => this;
}
