using System.Collections.Generic;

namespace DataNormalizer.Integration.Tests.TestTypes.Search;

public sealed class SearchSegment
{
    public List<SearchOption> Options { get; set; } = new();
}
