using System.Collections.Generic;

namespace DataNormalizer.Integration.Tests.TestTypes;

public sealed class LocationTreeNode
{
    public string Label { get; set; } = "";
    public LocationTreeNode? Parent { get; set; }
    public List<LocationTreeNode> Children { get; set; } = new();
    public string LocationName { get; set; } = "";
}
