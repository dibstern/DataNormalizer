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
        var root = result.PersonList[0];

        Assert.That(root.Name, Is.EqualTo("Alice"));
        Assert.That(root.Age, Is.EqualTo(30));
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
        var root = result.PersonList[0];

        // HomeAddressIndex should be a valid index
        Assert.That(root.HomeAddressIndex, Is.GreaterThanOrEqualTo(0));
        // Should have Address entries in the collection
        var addresses = result.AddressList;
        Assert.That(addresses, Has.Length.GreaterThanOrEqualTo(1));
        Assert.That(addresses[root.HomeAddressIndex].Street, Is.EqualTo("123 Main St"));
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
        var root = result.PersonList[0];

        // Same address values should dedup to one entry
        Assert.That(root.HomeAddressIndex, Is.EqualTo(root.WorkAddressIndex));
        var addresses = result.AddressList;
        Assert.That(addresses, Has.Length.EqualTo(1));
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
        var root = result.PersonList[0];

        Assert.That(root.HomeAddressIndex, Is.Not.EqualTo(root.WorkAddressIndex));
        var addresses = result.AddressList;
        Assert.That(addresses, Has.Length.EqualTo(2));
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
        var root = result.PersonList[0];

        Assert.That(root.HomeAddressIndex, Is.EqualTo(root.WorkAddressIndex));
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
        var root = result.PersonList[0];

        Assert.That(root.WorkAddressIndex, Is.Null);
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
        var root = result.PersonList[0];

        Assert.That(root.PhoneNumbersIndices, Is.Empty);
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
        var root = result.PersonList[0];

        Assert.That(root.PhoneNumbersIndices, Has.Length.EqualTo(2));
        var phones = result.PhoneNumberList;
        Assert.That(phones, Has.Length.GreaterThanOrEqualTo(2));
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
        var root = result.PersonList[0];

        Assert.That(root.PhoneNumbersIndices, Has.Length.EqualTo(2));
        // Both indices should be the same (dedup)
        Assert.That(root.PhoneNumbersIndices[0], Is.EqualTo(root.PhoneNumbersIndices[1]));
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

        Assert.That(personResult.PersonList[0].Name, Is.EqualTo("Alice"));
        Assert.That(orderResult.OrderList[0].OrderId, Is.EqualTo(42));
    }
}
