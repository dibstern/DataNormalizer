using DataNormalizer.Integration.Tests.TestTypes;
using NUnit.Framework;

namespace DataNormalizer.Integration.Tests;

[TestFixture]
public sealed class SmokeTests
{
    [Test]
    public void BasicNormalization_Compiles_And_Runs()
    {
        var person = new Person
        {
            Name = "Alice",
            Age = 30,
            HomeAddress = new Address
            {
                Street = "123 Main St",
                City = "Springfield",
                ZipCode = "62701",
            },
        };

        var result = BasicNormalizationConfig.Normalize(person);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.PersonList.Length, Is.GreaterThan(0));
    }
}
