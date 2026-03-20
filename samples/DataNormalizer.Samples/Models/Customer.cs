namespace DataNormalizer.Samples.Models;

public sealed class Customer
{
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public Address BillingAddress { get; set; } = new();
}
