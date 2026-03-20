namespace DataNormalizer.Integration.Tests.TestTypes.Cycles;

public sealed class NodeA
{
    public string Value { get; set; } = "";
    public NodeB? RefB { get; set; }
}
