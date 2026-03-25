using System.Text.Json;
using DataNormalizer.Integration.Tests.TestTypes;
using NUnit.Framework;

namespace DataNormalizer.Integration.Tests;

[TestFixture]
public sealed class JsonNamingTests
{
    private static Person CreateTestPerson() =>
        new()
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

    [Test]
    public void DefaultConfig_JsonContainsCamelCasePropertyNames()
    {
        var person = CreateTestPerson();
        var result = BasicNormalizationConfig.Normalize(person);
        var json = JsonSerializer.Serialize(result);

        // Default config emits [JsonPropertyName] with camelCase
        Assert.That(json, Does.Contain("\"personDtos\""));
        Assert.That(json, Does.Contain("\"addressDtos\""));
        Assert.That(json, Does.Contain("\"homeAddressIndex\""));

        // Should NOT contain PascalCase property names
        Assert.That(json, Does.Not.Contain("\"PersonDtos\""));
        Assert.That(json, Does.Not.Contain("\"AddressDtos\""));
        Assert.That(json, Does.Not.Contain("\"HomeAddressIndex\""));
    }

    [Test]
    public void NoJsonNaming_JsonContainsPascalCasePropertyNames()
    {
        var contact = new TestTypes.NoJsonNaming.Contact
        {
            Name = "Alice",
            Age = 30,
            HomeAddress = new TestTypes.NoJsonNaming.Location
            {
                Street = "123 Main St",
                City = "Springfield",
            },
        };

        var result = NoJsonNamingConfig.Normalize(contact);
        var json = JsonSerializer.Serialize(result);

        // EmitJsonPropertyNames = false → no [JsonPropertyName] → PascalCase by default
        Assert.That(json, Does.Contain("\"ContactDtos\""));
        Assert.That(json, Does.Contain("\"LocationDtos\""));
        Assert.That(json, Does.Contain("\"HomeAddressIndex\""));

        // Should NOT contain camelCase
        Assert.That(json, Does.Not.Contain("\"contactDtos\""));
        Assert.That(json, Does.Not.Contain("\"locationDtos\""));
        Assert.That(json, Does.Not.Contain("\"homeAddressIndex\""));
    }

    [Test]
    public void CustomSuffix_ContainerTypeNamePreserved()
    {
        var contact = new TestTypes.CustomSuffix.Contact
        {
            Name = "Alice",
            Age = 30,
            HomeAddress = new TestTypes.CustomSuffix.Location
            {
                Street = "123 Main St",
                City = "Springfield",
            },
        };

        var result = CustomSuffixNamingConfig.Normalize(contact);

        // Container type is ContactResultDto (ContainerSuffix unchanged = "Dto")
        Assert.That(
            result.GetType().Name,
            Is.EqualTo("ContactResultDto")
        );
    }

    [Test]
    public void CustomSuffix_JsonContainsModelListNames()
    {
        var contact = new TestTypes.CustomSuffix.Contact
        {
            Name = "Alice",
            Age = 30,
            HomeAddress = new TestTypes.CustomSuffix.Location
            {
                Street = "123 Main St",
                City = "Springfield",
            },
        };

        var result = CustomSuffixNamingConfig.Normalize(contact);
        var json = JsonSerializer.Serialize(result);

        // DtoSuffix = "Model" → list properties are "ContactModels", "LocationModels"
        // With EmitJsonPropertyNames = true (default) → camelCase in JSON
        Assert.That(json, Does.Contain("\"contactModels\""));
        Assert.That(json, Does.Contain("\"locationModels\""));

        // Should NOT contain "Dto" suffix in list names
        Assert.That(json, Does.Not.Contain("\"contactDtos\""));
        Assert.That(json, Does.Not.Contain("\"locationDtos\""));
    }
}
