namespace DataNormalizer.Integration.Tests.TestTypes.Search;

public sealed class SearchPlace
{
    public string ShortName { get; set; } = "";
    public string Kind { get; set; } = "";
    public double Lat { get; set; }
    public double Lng { get; set; }
}
