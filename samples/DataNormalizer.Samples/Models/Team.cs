using System.Collections.Generic;

namespace DataNormalizer.Samples.Models;

public sealed class Team
{
    public string Name { get; set; } = "";
    public string Specialty { get; set; } = "";
    public List<Employee> Members { get; set; } = new();
}
