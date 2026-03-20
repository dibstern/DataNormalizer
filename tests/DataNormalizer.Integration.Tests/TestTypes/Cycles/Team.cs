namespace DataNormalizer.Integration.Tests.TestTypes.Cycles;

public sealed class Team
{
    public string Name { get; set; } = "";
    public Member? Lead { get; set; }
}
