using DataNormalizer.Integration.Tests.TestTypes;
using NUnit.Framework;

namespace DataNormalizer.Integration.Tests;

[TestFixture]
public sealed class SimpleNormalizationTests
{
    [Test]
    public void Normalize_SimplePerson_PreservesSimpleProperties()
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

        Assert.That(result.Root.Name, Is.EqualTo("Alice"));
        Assert.That(result.Root.Age, Is.EqualTo(30));
    }

    [Test]
    public void Normalize_NestedAddress_ProducesIndex()
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

        // HomeAddressIndex should be a valid index
        Assert.That(result.Root.HomeAddressIndex, Is.GreaterThanOrEqualTo(0));
        // Should have Address entries in the collection
        var addresses = result.GetCollection<NormalizedAddress>("Address");
        Assert.That(addresses, Has.Count.GreaterThanOrEqualTo(1));
        Assert.That(addresses[result.Root.HomeAddressIndex].Street, Is.EqualTo("123 Main St"));
    }

    [Test]
    public void Normalize_ValueEqualityDedup_SameAddressValues_OneEntry()
    {
        var addr1 = new Address
        {
            Street = "123 Main St",
            City = "Springfield",
            ZipCode = "62701",
        };
        var addr2 = new Address
        {
            Street = "123 Main St",
            City = "Springfield",
            ZipCode = "62701",
        }; // same values, different instance

        var person = new Person
        {
            Name = "Alice",
            Age = 30,
            HomeAddress = addr1,
            WorkAddress = addr2,
        };

        var result = BasicNormalizationConfig.Normalize(person);

        // Same address values should dedup to one entry
        Assert.That(result.Root.HomeAddressIndex, Is.EqualTo(result.Root.WorkAddressIndex));
        var addresses = result.GetCollection<NormalizedAddress>("Address");
        Assert.That(addresses, Has.Count.EqualTo(1));
    }

    [Test]
    public void Normalize_DifferentAddresses_DifferentIndices()
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
                City = "Shelbyville",
                ZipCode = "62702",
            },
        };

        var result = BasicNormalizationConfig.Normalize(person);

        Assert.That(result.Root.HomeAddressIndex, Is.Not.EqualTo(result.Root.WorkAddressIndex));
        var addresses = result.GetCollection<NormalizedAddress>("Address");
        Assert.That(addresses, Has.Count.EqualTo(2));
    }

    [Test]
    public void Normalize_SameReferenceUsedTwice_SameIndex()
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
            WorkAddress = sharedAddr, // same reference
        };

        var result = BasicNormalizationConfig.Normalize(person);

        Assert.That(result.Root.HomeAddressIndex, Is.EqualTo(result.Root.WorkAddressIndex));
    }

    [Test]
    public void Normalize_NullProperty_NullIndex()
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

        Assert.That(result.Root.WorkAddressIndex, Is.Null);
    }

    [Test]
    public void Normalize_EmptyCollection_EmptyIndicesArray()
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

        Assert.That(result.Root.PhoneNumbersIndices, Is.Empty);
    }

    [Test]
    public void Normalize_CollectionWithItems_ProducesIndices()
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

        Assert.That(result.Root.PhoneNumbersIndices, Has.Length.EqualTo(2));
        var phones = result.GetCollection<NormalizedPhoneNumber>("PhoneNumber");
        Assert.That(phones, Has.Count.GreaterThanOrEqualTo(2));
    }

    [Test]
    public void Normalize_CollectionWithDuplicates_Deduplicates()
    {
        var phone = new PhoneNumber { Number = "555-0100", Type = "Home" };
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
            PhoneNumbers = new() { phone, phone }, // same reference twice
        };

        var result = BasicNormalizationConfig.Normalize(person);

        Assert.That(result.Root.PhoneNumbersIndices, Has.Length.EqualTo(2));
        // Both indices should be the same (dedup)
        Assert.That(result.Root.PhoneNumbersIndices[0], Is.EqualTo(result.Root.PhoneNumbersIndices[1]));
    }

    [Test]
    public void Normalize_MultipleRootTypes_BothWork()
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

        var personResult = BasicNormalizationConfig.Normalize(person);
        var orderResult = BasicNormalizationConfig.Normalize(order);

        Assert.That(personResult.Root.Name, Is.EqualTo("Alice"));
        Assert.That(orderResult.Root.OrderId, Is.EqualTo(42));
    }
}
