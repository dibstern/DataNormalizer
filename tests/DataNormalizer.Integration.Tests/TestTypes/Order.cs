namespace DataNormalizer.Integration.Tests.TestTypes;

public sealed class Order
{
    public int OrderId { get; set; }
    public string Description { get; set; } = "";
    public Address ShippingAddress { get; set; } = new();
}
