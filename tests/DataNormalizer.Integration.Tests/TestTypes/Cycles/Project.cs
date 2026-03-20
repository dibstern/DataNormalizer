namespace DataNormalizer.Integration.Tests.TestTypes.Cycles;

public sealed class Project
{
    public string Name { get; set; } = "";
    public Team? CoreTeam { get; set; }
}
