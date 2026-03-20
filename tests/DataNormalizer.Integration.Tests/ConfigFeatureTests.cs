using DataNormalizer.Integration.Tests.TestTypes;
using NUnit.Framework;

namespace DataNormalizer.Integration.Tests;

[TestFixture]
public sealed class ConfigFeatureTests
{
    [Test]
    public void IgnoreProperty_AgeExcludedFromDto()
    {
        var employee = new Employee
        {
            Name = "Bob",
            Age = 25,
            Department = "Engineering",
            Office = "A101",
        };

        var result = IgnorePropertyConfig.Normalize(employee);

        // NormalizedEmployee should NOT have an Age property
        // Verify via reflection that the DTO type doesn't have "Age"
        var dtoType = result.Root.GetType();
        var ageProperty = dtoType.GetProperty("Age");
        Assert.That(ageProperty, Is.Null, "Age should be excluded from generated DTO");

        // Other properties should be present
        Assert.That(result.Root.Name, Is.EqualTo("Bob"));
        Assert.That(result.Root.Department, Is.EqualTo("Engineering"));
        Assert.That(result.Root.Office, Is.EqualTo("A101"));
    }

    [Test]
    public void IgnoreProperty_RoundtripPreservesNonIgnoredProperties()
    {
        var employee = new Employee
        {
            Name = "Bob",
            Age = 25,
            Department = "Engineering",
            Office = "A101",
        };

        var result = IgnorePropertyConfig.Normalize(employee);
        var restored = IgnorePropertyConfig.Denormalize(result);

        Assert.That(restored.Name, Is.EqualTo("Bob"));
        Assert.That(restored.Department, Is.EqualTo("Engineering"));
        Assert.That(restored.Office, Is.EqualTo("A101"));
        // Age is lost (ignored) — it will be default(int) = 0
        Assert.That(restored.Age, Is.EqualTo(0));
    }
}
