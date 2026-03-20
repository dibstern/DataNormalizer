namespace DataNormalizer.Runtime;

/// <summary>
/// Contains the result of normalizing an object graph. Provides access to the root DTO,
/// flat collections, and index resolution.
/// </summary>
/// <typeparam name="TRoot">The type of the root normalized DTO.</typeparam>
public sealed class NormalizedResult<TRoot>
{
    private readonly NormalizationContext _context;

    /// <summary>
    /// Initializes a new instance of <see cref="NormalizedResult{TRoot}"/>.
    /// </summary>
    /// <param name="root">The root normalized DTO.</param>
    /// <param name="rootIndex">The index of the root in its type collection.</param>
    /// <param name="context">The normalization context containing flat collections.</param>
    public NormalizedResult(TRoot root, int rootIndex, NormalizationContext context)
    {
        Root = root ?? throw new ArgumentNullException(nameof(root));
        RootIndex = rootIndex;
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// The root normalized DTO.
    /// </summary>
    public TRoot Root { get; }

    /// <summary>
    /// The index of the root in its type collection.
    /// </summary>
    public int RootIndex { get; }

    /// <summary>
    /// Gets the flat collection of normalized DTOs for a specific type key.
    /// </summary>
    /// <typeparam name="T">The normalized DTO type.</typeparam>
    /// <param name="typeKey">The type key identifying the collection.</param>
    /// <returns>A read-only list of normalized DTOs.</returns>
    public IReadOnlyList<T> GetCollection<T>(string typeKey)
        where T : class => _context.GetCollection<T>(typeKey);

    /// <summary>
    /// Gets the flat collection of normalized DTOs using <c>typeof(T).Name</c> as the type key.
    /// </summary>
    /// <typeparam name="T">The normalized DTO type.</typeparam>
    /// <returns>A read-only list of normalized DTOs.</returns>
    public IReadOnlyList<T> GetCollection<T>()
        where T : class => _context.GetCollection<T>();

    /// <summary>
    /// Resolves a normalized DTO by type key and index.
    /// </summary>
    /// <typeparam name="T">The normalized DTO type.</typeparam>
    /// <param name="typeKey">The type key identifying the collection.</param>
    /// <param name="index">The index within the collection.</param>
    /// <returns>The resolved normalized DTO.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is out of range.</exception>
    public T Resolve<T>(string typeKey, int index)
        where T : class
    {
        var collection = _context.GetCollection<T>(typeKey);
        if (index < 0 || index >= collection.Count)
            throw new ArgumentOutOfRangeException(
                nameof(index),
                $"Index {index} out of range for '{typeKey}' ({collection.Count} items)."
            );
        return collection[index];
    }

    /// <summary>
    /// Gets all type keys that have collections.
    /// </summary>
    public IEnumerable<string> CollectionNames => _context.CollectionNames;
}
