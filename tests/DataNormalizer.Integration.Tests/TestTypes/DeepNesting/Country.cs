namespace DataNormalizer.Integration.Tests.TestTypes.DeepNesting;

public sealed class Country
{
    public string Name { get; set; } = "";
    public City Capital { get; set; } = new();
}
