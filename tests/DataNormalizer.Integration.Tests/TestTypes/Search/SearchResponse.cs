using System.Collections.Generic;

namespace DataNormalizer.Integration.Tests.TestTypes.Search;

public sealed class SearchResponse
{
    public List<SearchRoute> Routes { get; set; } = new();
    public List<SearchPlace> Places { get; set; } = new();
    public SearchPlace OriginPlace { get; set; } = new();
    public SearchPlace DestinationPlace { get; set; } = new();
}
