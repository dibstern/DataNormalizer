namespace DataNormalizer.Integration.Tests.TestTypes.DeepNesting;

public sealed class Galaxy
{
    public string Name { get; set; } = "";
    public SolarSystem MainSystem { get; set; } = new();
}
