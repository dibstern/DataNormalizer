using System.Collections.Generic;

namespace DataNormalizer.Samples.Models;

public sealed class Division
{
    public string Name { get; set; } = "";
    public Division? ParentDivision { get; set; }
    public List<Department> Departments { get; set; } = new();
}
