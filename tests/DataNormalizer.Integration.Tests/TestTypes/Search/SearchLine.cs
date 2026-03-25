using System.Collections.Generic;

namespace DataNormalizer.Integration.Tests.TestTypes.Search;

public sealed class SearchLine
{
    public List<SearchPlace> Places { get; set; } = new();
    public string Path { get; set; } = "";
}
