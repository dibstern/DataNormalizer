namespace DataNormalizer.Samples.Models;

public sealed class OrderLine
{
    public Product Product { get; set; } = new();
    public int Quantity { get; set; }
}
