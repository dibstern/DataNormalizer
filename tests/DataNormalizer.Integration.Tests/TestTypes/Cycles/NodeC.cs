namespace DataNormalizer.Integration.Tests.TestTypes.Cycles;

public sealed class NodeC
{
    public string Value { get; set; } = "";
    public NodeA? RefA { get; set; }
}
