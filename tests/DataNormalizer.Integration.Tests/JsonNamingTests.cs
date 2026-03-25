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
        // Root type uses "result" instead of a list
        Assert.That(json, Does.Contain("\"result\""));
        Assert.That(json, Does.Contain("\"addressDtos\""));
        Assert.That(json, Does.Contain("\"homeAddressIndex\""));

        // Should NOT contain PascalCase property names
        Assert.That(json, Does.Not.Contain("\"Result\""));
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
            HomeAddress = new TestTypes.NoJsonNaming.Location { Street = "123 Main St", City = "Springfield" },
        };

        var result = NoJsonNamingConfig.Normalize(contact);
        var json = JsonSerializer.Serialize(result);

        // EmitJsonPropertyNames = false → no [JsonPropertyName] on lists → PascalCase by default
        // But Result always gets [JsonPropertyName("result")] regardless
        Assert.That(json, Does.Contain("\"result\""));
        Assert.That(json, Does.Contain("\"LocationDtos\""));
        Assert.That(json, Does.Contain("\"HomeAddressIndex\""));

        // Lists should NOT contain camelCase (no JsonPropertyName on them)
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
            HomeAddress = new TestTypes.CustomSuffix.Location { Street = "123 Main St", City = "Springfield" },
        };

        var result = CustomSuffixNamingConfig.Normalize(contact);

        // Container type is ContactResultDto (ContainerSuffix unchanged = "Dto")
        Assert.That(result.GetType().Name, Is.EqualTo("ContactResultDto"));
    }

    [Test]
    public void CustomSuffix_JsonContainsModelListNames()
    {
        var contact = new TestTypes.CustomSuffix.Contact
        {
            Name = "Alice",
            Age = 30,
            HomeAddress = new TestTypes.CustomSuffix.Location { Street = "123 Main St", City = "Springfield" },
        };

        var result = CustomSuffixNamingConfig.Normalize(contact);
        var json = JsonSerializer.Serialize(result);

        // Root type uses "result" property; non-root types use DtoSuffix = "Model"
        // With EmitJsonPropertyNames = true (default) → camelCase in JSON
        Assert.That(json, Does.Contain("\"result\""));
        Assert.That(json, Does.Contain("\"locationModels\""));

        // Should NOT contain "Dto" suffix in list names
        Assert.That(json, Does.Not.Contain("\"contactDtos\""));
        Assert.That(json, Does.Not.Contain("\"locationDtos\""));
    }

    // --- Gap 1: DtoPrefix tests ---

    private static TestTypes.DtoPrefix.Contact CreateDtoPrefixContact() =>
        new()
        {
            Name = "Alice",
            Age = 30,
            HomeAddress = new TestTypes.DtoPrefix.Location { Street = "123 Main St", City = "Springfield" },
        };

    [Test]
    public void DtoPrefix_ContainerTypeNameUnchanged()
    {
        var contact = CreateDtoPrefixContact();
        var result = DtoPrefixNamingConfig.Normalize(contact);

        // DtoPrefix does not apply to containers — container is always {TypeName}Result{ContainerSuffix}
        Assert.That(result.GetType().Name, Is.EqualTo("ContactResultDto"));
    }

    [Test]
    public void DtoPrefix_EmptySuffix_JsonUsesListFallback()
    {
        var contact = CreateDtoPrefixContact();
        var result = DtoPrefixNamingConfig.Normalize(contact);
        var json = JsonSerializer.Serialize(result);

        // Root type uses "result" property; non-root with DtoSuffix = "" → "{baseName}List"
        // camelCase in JSON: "result", "locationList"
        Assert.That(json, Does.Contain("\"result\""));
        Assert.That(json, Does.Contain("\"locationList\""));
    }

    [Test]
    public void DtoPrefix_DtoTypesHavePrefixAndNoSuffix()
    {
        var contact = CreateDtoPrefixContact();
        var result = DtoPrefixNamingConfig.Normalize(contact);

        // DTO name = {DtoPrefix}{TypeName}{DtoSuffix} = "Flat" + "Contact" + "" = "FlatContact"
        // Root type uses Result property instead of ContactList
        var containerType = result.GetType();
        var resultProp = containerType.GetProperty("Result");
        Assert.That(resultProp, Is.Not.Null, "Expected Result property on container");

        var resultType = resultProp!.PropertyType;
        Assert.That(resultType.Name, Is.EqualTo("FlatContact"));
    }

    // --- Gap 2: ContainerSuffix tests ---

    private static TestTypes.ContainerSuffix.Contact CreateContainerSuffixContact() =>
        new()
        {
            Name = "Alice",
            Age = 30,
            HomeAddress = new TestTypes.ContainerSuffix.Location { Street = "123 Main St", City = "Springfield" },
        };

    [Test]
    public void ContainerSuffix_EmptySuffix_ContainerNameHasNoDto()
    {
        var contact = CreateContainerSuffixContact();
        var result = ContainerSuffixNamingConfig.Normalize(contact);

        // ContainerSuffix = "" → container is "ContactResult" (no "Dto" suffix)
        Assert.That(result.GetType().Name, Is.EqualTo("ContactResult"));
    }

    [Test]
    public void ContainerSuffix_DtoNamesStillUseDefaultSuffix()
    {
        var contact = CreateContainerSuffixContact();
        var result = ContainerSuffixNamingConfig.Normalize(contact);
        var json = JsonSerializer.Serialize(result);

        // Root type uses "result"; non-root DtoSuffix is still default "Dto" → "locationDtos"
        Assert.That(json, Does.Contain("\"result\""));
        Assert.That(json, Does.Contain("\"locationDtos\""));
    }

    // --- Gap 3: Graph-level UseNaming override tests ---

    private static TestTypes.GraphNaming.Contact CreateGraphNamingContact() =>
        new()
        {
            Name = "Alice",
            Age = 30,
            HomeAddress = new TestTypes.GraphNaming.Location { Street = "123 Main St", City = "Springfield" },
        };

    [Test]
    public void GraphNaming_GraphOverridesGlobalDtoSuffix()
    {
        var contact = CreateGraphNamingContact();
        var result = GraphNamingConfig.Normalize(contact);
        var json = JsonSerializer.Serialize(result);

        // Root type uses "result"; non-root types use graph-level DtoSuffix override "View"
        // → list properties use "View" suffix: "locationViews"
        Assert.That(json, Does.Contain("\"result\""));
        Assert.That(json, Does.Contain("\"locationViews\""));

        // Should NOT contain the global "Record" suffix
        Assert.That(json, Does.Not.Contain("\"contactRecords\""));
        Assert.That(json, Does.Not.Contain("\"locationRecords\""));
    }

    [Test]
    public void GraphNaming_ContainerUsesDefaultContainerSuffix()
    {
        var contact = CreateGraphNamingContact();
        var result = GraphNamingConfig.Normalize(contact);

        // ContainerSuffix not overridden at graph level → uses default "Dto"
        Assert.That(result.GetType().Name, Is.EqualTo("ContactResultDto"));
    }
}
