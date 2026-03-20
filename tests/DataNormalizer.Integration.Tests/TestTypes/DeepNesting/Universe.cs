namespace DataNormalizer.Integration.Tests.TestTypes.DeepNesting;

public sealed class Universe
{
    public string Name { get; set; } = "";
    public Galaxy MainGalaxy { get; set; } = new();
}
