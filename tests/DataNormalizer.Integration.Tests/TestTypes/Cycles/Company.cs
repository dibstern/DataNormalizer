namespace DataNormalizer.Integration.Tests.TestTypes.Cycles;

public sealed class Company
{
    public string Title { get; set; } = "";
    public CyclePerson? Ceo { get; set; }
}
