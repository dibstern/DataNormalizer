namespace DataNormalizer.Integration.Tests.TestTypes.GraphNaming;

public sealed class Contact
{
    public string Name { get; set; } = "";
    public int Age { get; set; }
    public Location HomeAddress { get; set; } = new();
}
