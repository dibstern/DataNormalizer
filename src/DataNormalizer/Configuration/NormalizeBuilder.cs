namespace DataNormalizer.Configuration;

/// <summary>
/// Builder for defining normalization graphs and type-level configurations.
/// Used inside <see cref="NormalizationConfig.Configure"/>.
/// </summary>
public sealed class NormalizeBuilder
{
    /// <summary>
    /// Adds a root type to the normalization graph. The generator auto-discovers all referenced types.
    /// </summary>
    /// <typeparam name="T">The root type to normalize.</typeparam>
    /// <returns>A <see cref="GraphBuilder{T}"/> for further configuration.</returns>
    public GraphBuilder<T> NormalizeGraph<T>()
        where T : class => new();

    /// <summary>
    /// Adds a root type to the normalization graph and applies additional configuration.
    /// The generator auto-discovers all referenced types.
    /// </summary>
    /// <typeparam name="T">The root type to normalize.</typeparam>
    /// <param name="configure">An action to configure the graph builder.</param>
    /// <returns>A <see cref="GraphBuilder{T}"/> for further configuration.</returns>
    public GraphBuilder<T> NormalizeGraph<T>(Action<GraphBuilder<T>> configure)
        where T : class
    {
        var builder = new GraphBuilder<T>();
        configure(builder);
        return builder;
    }

    /// <summary>
    /// Configures property-level behavior for a specific type.
    /// </summary>
    /// <typeparam name="T">The type to configure.</typeparam>
    /// <returns>A <see cref="TypeBuilder{T}"/> for further configuration.</returns>
    public TypeBuilder<T> ForType<T>()
        where T : class => new();

    /// <summary>
    /// Configures property-level behavior for a specific type using a configuration action.
    /// </summary>
    /// <typeparam name="T">The type to configure.</typeparam>
    /// <param name="configure">An action to configure the type builder.</param>
    /// <returns>This builder instance for chaining.</returns>
    public NormalizeBuilder ForType<T>(Action<TypeBuilder<T>> configure)
        where T : class
    {
        configure(new TypeBuilder<T>());
        return this;
    }
}
