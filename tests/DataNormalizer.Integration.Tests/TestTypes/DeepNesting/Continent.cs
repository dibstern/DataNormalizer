namespace DataNormalizer.Integration.Tests.TestTypes.DeepNesting;

public sealed class Continent
{
    public string Name { get; set; } = "";
    public Country MainCountry { get; set; } = new();
}
