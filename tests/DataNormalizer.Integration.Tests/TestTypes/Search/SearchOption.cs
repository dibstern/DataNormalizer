using System.Collections.Generic;

namespace DataNormalizer.Integration.Tests.TestTypes.Search;

public sealed class SearchOption
{
    public List<SearchHop> Hops { get; set; } = new();
}
