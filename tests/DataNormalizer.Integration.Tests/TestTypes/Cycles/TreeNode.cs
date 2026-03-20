using System.Collections.Generic;

namespace DataNormalizer.Integration.Tests.TestTypes.Cycles;

public sealed class TreeNode
{
    public string Label { get; set; } = "";
    public TreeNode? Parent { get; set; }
    public List<TreeNode> Children { get; set; } = new();
}
