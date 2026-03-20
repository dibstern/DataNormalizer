namespace DataNormalizer.Integration.Tests.TestTypes.Cycles;

public sealed class NodeB
{
    public string Value { get; set; } = "";
    public NodeC? RefC { get; set; }
}
