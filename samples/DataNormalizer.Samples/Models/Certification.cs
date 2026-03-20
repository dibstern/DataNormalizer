namespace DataNormalizer.Samples.Models;

public sealed class Certification
{
    public string Name { get; set; } = "";
    public string IssuedBy { get; set; } = "";
    public Certification? Prerequisite { get; set; }
    public Skill RequiredSkill { get; set; } = new();
}
