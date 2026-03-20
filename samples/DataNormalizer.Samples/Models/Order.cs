using System.Collections.Generic;

namespace DataNormalizer.Samples.Models;

public sealed class Order
{
    public int OrderId { get; set; }
    public Customer Customer { get; set; } = new();
    public Address ShippingAddress { get; set; } = new();
    public List<OrderLine> Lines { get; set; } = new();
}
