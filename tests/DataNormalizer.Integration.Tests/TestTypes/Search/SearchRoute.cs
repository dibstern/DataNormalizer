using System.Collections.Generic;

namespace DataNormalizer.Integration.Tests.TestTypes.Search;

public sealed class SearchRoute
{
    public List<SearchSegment> Segments { get; set; } = new();
    public List<SearchPlace> Places { get; set; } = new();
}
