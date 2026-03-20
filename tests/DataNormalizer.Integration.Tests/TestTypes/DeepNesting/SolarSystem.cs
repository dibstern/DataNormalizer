namespace DataNormalizer.Integration.Tests.TestTypes.DeepNesting;

public sealed class SolarSystem
{
    public string Name { get; set; } = "";
    public Planet MainPlanet { get; set; } = new();
}
