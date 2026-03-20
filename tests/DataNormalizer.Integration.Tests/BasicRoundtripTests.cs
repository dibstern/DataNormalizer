using DataNormalizer.Integration.Tests.TestTypes;
using NUnit.Framework;

namespace DataNormalizer.Integration.Tests;

[TestFixture]
public sealed class BasicRoundtripTests
{
    [Test]
    public void Roundtrip_SimpleProperties_Preserved()
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
        var restored = BasicNormalizationConfig.Denormalize(result);

        Assert.That(restored.Name, Is.EqualTo("Alice"));
        Assert.That(restored.Age, Is.EqualTo(30));
    }

    [Test]
    public void Roundtrip_NestedAddress_Preserved()
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
        var restored = BasicNormalizationConfig.Denormalize(result);

        Assert.That(restored.HomeAddress.Street, Is.EqualTo("123 Main St"));
        Assert.That(restored.HomeAddress.City, Is.EqualTo("Springfield"));
        Assert.That(restored.HomeAddress.ZipCode, Is.EqualTo("62701"));
    }

    [Test]
    public void Roundtrip_NullableProperty_Preserved()
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
            WorkAddress = new Address
            {
                Street = "456 Oak Ave",
                City = "Capital City",
                ZipCode = "62703",
            },
        };

        var result = BasicNormalizationConfig.Normalize(person);
        var restored = BasicNormalizationConfig.Denormalize(result);

        Assert.That(restored.WorkAddress, Is.Not.Null);
        Assert.That(restored.WorkAddress!.Street, Is.EqualTo("456 Oak Ave"));
    }

    [Test]
    public void Roundtrip_NullProperty_StaysNull()
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
            WorkAddress = null,
        };

        var result = BasicNormalizationConfig.Normalize(person);
        var restored = BasicNormalizationConfig.Denormalize(result);

        Assert.That(restored.WorkAddress, Is.Null);
    }

    [Test]
    public void Roundtrip_Collection_Preserved()
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
            PhoneNumbers = new()
            {
                new PhoneNumber { Number = "555-0100", Type = "Home" },
                new PhoneNumber { Number = "555-0200", Type = "Work" },
            },
        };

        var result = BasicNormalizationConfig.Normalize(person);
        var restored = BasicNormalizationConfig.Denormalize(result);

        Assert.That(restored.PhoneNumbers, Has.Count.EqualTo(2));
        Assert.That(restored.PhoneNumbers[0].Number, Is.EqualTo("555-0100"));
        Assert.That(restored.PhoneNumbers[1].Number, Is.EqualTo("555-0200"));
    }

    [Test]
    public void Roundtrip_EmptyCollection_Preserved()
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
            PhoneNumbers = new(),
        };

        var result = BasicNormalizationConfig.Normalize(person);
        var restored = BasicNormalizationConfig.Denormalize(result);

        Assert.That(restored.PhoneNumbers, Is.Not.Null);
        Assert.That(restored.PhoneNumbers, Is.Empty);
    }

    [Test]
    public void Roundtrip_SharedAddress_SameReferenceAfterDenormalize()
    {
        var sharedAddr = new Address
        {
            Street = "123 Main St",
            City = "Springfield",
            ZipCode = "62701",
        };
        var person = new Person
        {
            Name = "Alice",
            Age = 30,
            HomeAddress = sharedAddr,
            WorkAddress = sharedAddr,
        };

        var result = BasicNormalizationConfig.Normalize(person);
        var restored = BasicNormalizationConfig.Denormalize(result);

        // After denormalization, both should point to the SAME object
        Assert.That(restored.HomeAddress, Is.SameAs(restored.WorkAddress));
    }

    [Test]
    public void Roundtrip_Order_Preserved()
    {
        var order = new Order
        {
            OrderId = 42,
            Description = "Test Order",
            ShippingAddress = new Address
            {
                Street = "789 Elm St",
                City = "Capital City",
                ZipCode = "62703",
            },
        };

        var result = BasicNormalizationConfig.Normalize(order);
        var restored = BasicNormalizationConfig.Denormalize(result);

        Assert.That(restored.OrderId, Is.EqualTo(42));
        Assert.That(restored.Description, Is.EqualTo("Test Order"));
        Assert.That(restored.ShippingAddress.Street, Is.EqualTo("789 Elm St"));
    }
}
