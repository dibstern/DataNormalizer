namespace DataNormalizer.Integration.Tests.TestTypes.CustomSuffix;

public sealed class Contact
{
    public string Name { get; set; } = "";
    public int Age { get; set; }
    public Location HomeAddress { get; set; } = new();
}
