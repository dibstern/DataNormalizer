using System.Collections.Generic;

namespace DataNormalizer.Samples.Models;

public sealed class Employee
{
    public string Name { get; set; } = "";
    public string Title { get; set; } = "";
    public Employee? Mentor { get; set; }
    public List<Certification> Certifications { get; set; } = new();
}
