using System.Collections.Generic;

namespace DataNormalizer.Samples.Models;

public sealed class Department
{
    public string Name { get; set; } = "";
    public decimal Budget { get; set; }
    public List<Team> Teams { get; set; } = new();
}
