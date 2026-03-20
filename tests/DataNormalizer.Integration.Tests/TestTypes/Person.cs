using System.Collections.Generic;

namespace DataNormalizer.Integration.Tests.TestTypes;

public sealed class Person
{
    public string Name { get; set; } = "";
    public int Age { get; set; }
    public Address HomeAddress { get; set; } = new();
    public Address? WorkAddress { get; set; }
    public List<PhoneNumber> PhoneNumbers { get; set; } = new();
}
