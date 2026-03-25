using System.Collections.Generic;

namespace DataNormalizer.Integration.Tests.TestTypes.Search;

public sealed class SearchHop
{
    public SearchLine Line { get; set; } = new();
    public SearchCarrier? MarketingCarrier { get; set; }
    public SearchVehicle Vehicle { get; set; } = new();
    public List<SearchImage> TransitImages { get; set; } = new();
}
