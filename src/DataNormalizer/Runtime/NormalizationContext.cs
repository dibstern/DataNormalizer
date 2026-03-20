namespace DataNormalizer.Runtime;

/// <summary>
/// Tracks normalization state including deduplication indices and flat collections.
/// Used by generated normalizer code.
/// </summary>
public sealed class NormalizationContext
{
    private Dictionary<string, object> _indexMaps = new(StringComparer.Ordinal);
    private Dictionary<string, List<object>> _collections = new(StringComparer.Ordinal);
    private Dictionary<object, int>? _sourceToIndex;

    /// <summary>
    /// Creates a new NormalizationContext with default capacity.
    /// </summary>
    public NormalizationContext() { }

    /// <summary>
    /// Creates a new NormalizationContext with pre-allocated capacity for the expected number of type keys.
    /// </summary>
    /// <param name="estimatedTypeCount">The estimated number of distinct type keys that will be used.</param>
    public NormalizationContext(int estimatedTypeCount)
    {
        if (estimatedTypeCount > 0)
        {
            _indexMaps = new Dictionary<string, object>(estimatedTypeCount, StringComparer.Ordinal);
            _collections = new Dictionary<string, List<object>>(estimatedTypeCount, StringComparer.Ordinal);
        }
    }

    /// <summary>
    /// Gets an existing index for a DTO or assigns a new one based on value equality.
    /// </summary>
    /// <typeparam name="TDto">The DTO type, which must implement <see cref="IEquatable{T}"/>.</typeparam>
    /// <param name="typeKey">The type key identifying the collection.</param>
    /// <param name="dto">The DTO instance to look up or register.</param>
    /// <returns>A tuple of the index and whether the DTO was newly added.</returns>
    public (int Index, bool IsNew) GetOrAddIndex<TDto>(string typeKey, TDto dto)
        where TDto : IEquatable<TDto>
    {
        if (!_indexMaps.TryGetValue(typeKey, out var mapObj))
        {
            mapObj = new Dictionary<TDto, int>();
            _indexMaps[typeKey] = mapObj;
        }

        var map = (Dictionary<TDto, int>)mapObj;
        if (map.TryGetValue(dto, out var existingIndex))
            return (existingIndex, false);

        var newIndex = map.Count;
        map[dto] = newIndex;
        return (newIndex, true);
    }

    /// <summary>
    /// Combined GetOrAddIndex + AddToCollection in a single operation.
    /// Avoids two separate string-key dictionary lookups when registering a new entity.
    /// Generated code uses this for non-circular types and circular keyDto registration.
    /// </summary>
    /// <typeparam name="TDto">The DTO type, which must implement <see cref="IEquatable{T}"/>.</typeparam>
    /// <param name="typeKey">The type key identifying the collection.</param>
    /// <param name="dto">The DTO instance to look up or register.</param>
    /// <returns>A tuple of the index and whether the DTO was newly added.</returns>
    public (int Index, bool IsNew) GetOrAddIndexAndStore<TDto>(string typeKey, TDto dto)
        where TDto : class, IEquatable<TDto>
    {
        if (!_indexMaps.TryGetValue(typeKey, out var mapObj))
        {
            mapObj = new Dictionary<TDto, int>();
            _indexMaps[typeKey] = mapObj;
        }

        var map = (Dictionary<TDto, int>)mapObj;
        if (map.TryGetValue(dto, out var existingIndex))
            return (existingIndex, false);

        var newIndex = map.Count;
        map[dto] = newIndex;

        // Also add to collection (avoids separate AddToCollection call)
        if (!_collections.TryGetValue(typeKey, out var list))
        {
            list = [];
            _collections[typeKey] = list;
        }

        if (list.Count == newIndex)
        {
            list.Add(dto);
        }
        else
        {
            while (list.Count <= newIndex)
                list.Add(null!);
            list[newIndex] = dto;
        }

        return (newIndex, true);
    }

    /// <summary>
    /// Adds a normalized object to the flat collection at the specified index.
    /// </summary>
    /// <param name="typeKey">The type key identifying the collection.</param>
    /// <param name="index">The index at which to store the object.</param>
    /// <param name="obj">The object to store.</param>
    public void AddToCollection(string typeKey, int index, object obj)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);

        if (!_collections.TryGetValue(typeKey, out var list))
        {
            list = [];
            _collections[typeKey] = list;
        }

        if (list.Count == index)
        {
            list.Add(obj);
        }
        else
        {
            while (list.Count <= index)
                list.Add(null!);
            list[index] = obj;
        }
    }

    /// <summary>
    /// Gets the flat collection of normalized DTOs for a specific type key.
    /// </summary>
    /// <typeparam name="T">The normalized DTO type.</typeparam>
    /// <param name="typeKey">The type key identifying the collection.</param>
    /// <returns>A read-only list of normalized DTOs, or an empty list if the key is not found.</returns>
    public IReadOnlyList<T> GetCollection<T>(string typeKey)
        where T : class =>
        _collections.TryGetValue(typeKey, out var list) ? new CastingReadOnlyList<T>(list) : Array.Empty<T>();

    /// <summary>
    /// Gets the flat collection of normalized DTOs using <c>typeof(T).Name</c> as the type key.
    /// </summary>
    /// <typeparam name="T">The normalized DTO type.</typeparam>
    /// <returns>A read-only list of normalized DTOs.</returns>
    public IReadOnlyList<T> GetCollection<T>()
        where T : class => GetCollection<T>(typeof(T).Name);

    /// <summary>
    /// Gets all type keys that have collections.
    /// </summary>
    public IEnumerable<string> CollectionNames => _collections.Keys;

    /// <summary>
    /// Checks if a source object has already been assigned an index via reference tracking.
    /// Used by generated code when UseReferenceTrackingForCycles is enabled.
    /// </summary>
    public bool TryGetTrackedIndex(object source, out int index)
    {
        if (_sourceToIndex is not null)
            return _sourceToIndex.TryGetValue(source, out index);
        index = -1;
        return false;
    }

    /// <summary>
    /// Maps a source object to its assigned index using reference identity.
    /// Used by generated code when UseReferenceTrackingForCycles is enabled.
    /// </summary>
    public void TrackSource(object source, int index)
    {
        _sourceToIndex ??= new Dictionary<object, int>(System.Collections.Generic.ReferenceEqualityComparer.Instance);
        _sourceToIndex[source] = index;
    }

    private sealed class CastingReadOnlyList<T> : IReadOnlyList<T>
        where T : class
    {
        private readonly List<object> _inner;

        public CastingReadOnlyList(List<object> inner) => _inner = inner;

        public T this[int index] => (T)_inner[index];

        public int Count => _inner.Count;

        public IEnumerator<T> GetEnumerator()
        {
            for (var i = 0; i < _inner.Count; i++)
                yield return (T)_inner[i];
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
