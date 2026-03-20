namespace DataNormalizer.Samples.Models;

public sealed class Corporation
{
    public string Name { get; set; } = "";
    public Division HeadDivision { get; set; } = new();
}
