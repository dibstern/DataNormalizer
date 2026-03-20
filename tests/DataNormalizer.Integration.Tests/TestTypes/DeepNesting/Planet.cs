namespace DataNormalizer.Integration.Tests.TestTypes.DeepNesting;

public sealed class Planet
{
    public string Name { get; set; } = "";
    public Continent MainContinent { get; set; } = new();
}
