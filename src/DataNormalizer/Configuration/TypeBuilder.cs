using System.Linq.Expressions;

namespace DataNormalizer.Configuration;

/// <summary>
/// Configures property-level behavior for a specific type.
/// </summary>
/// <typeparam name="T">The type being configured.</typeparam>
public sealed class TypeBuilder<T>
    where T : class
{
    /// <summary>
    /// Excludes a property from the generated DTO.
    /// </summary>
    /// <param name="selector">Expression selecting the property to exclude.</param>
    /// <returns>This builder instance for chaining.</returns>
    public TypeBuilder<T> IgnoreProperty(Expression<Func<T, object?>> selector) => this;

    /// <summary>
    /// Marks a property for normalization (extracted into a separate collection).
    /// </summary>
    /// <param name="selector">Expression selecting the property to normalize.</param>
    /// <returns>This builder instance for chaining.</returns>
    public TypeBuilder<T> NormalizeProperty(Expression<Func<T, object?>> selector) => this;

    /// <summary>
    /// Marks a property to be kept inline.
    /// </summary>
    /// <param name="selector">Expression selecting the property to inline.</param>
    /// <returns>This builder instance for chaining.</returns>
    public TypeBuilder<T> InlineProperty(Expression<Func<T, object?>> selector) => this;

    /// <summary>
    /// Explicitly includes a property (used with <see cref="PropertyMode.ExplicitOnly"/> mode).
    /// </summary>
    /// <param name="selector">Expression selecting the property to include.</param>
    /// <returns>This builder instance for chaining.</returns>
    public TypeBuilder<T> IncludeProperty(Expression<Func<T, object?>> selector) => this;

    /// <summary>
    /// Sets a custom name for the generated collection key.
    /// (v1: parsed but not fully integrated)
    /// </summary>
    /// <param name="name">The custom collection key name.</param>
    /// <returns>This builder instance for chaining.</returns>
    public TypeBuilder<T> WithName(string name) => this;

    /// <summary>
    /// Sets the property inclusion mode for this type.
    /// </summary>
    /// <param name="mode">The property mode to use.</param>
    /// <returns>This builder instance for chaining.</returns>
    public TypeBuilder<T> UsePropertyMode(PropertyMode mode) => this;
}
