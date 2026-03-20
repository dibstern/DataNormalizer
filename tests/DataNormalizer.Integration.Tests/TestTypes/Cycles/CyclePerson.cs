namespace DataNormalizer.Integration.Tests.TestTypes.Cycles;

public sealed class CyclePerson
{
    public string Name { get; set; } = "";
    public Company? Employer { get; set; }
}
