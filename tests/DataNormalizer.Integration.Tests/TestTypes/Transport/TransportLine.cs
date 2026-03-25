using System.Collections.Generic;

namespace DataNormalizer.Integration.Tests.TestTypes.Transport;

public sealed class TransportLine
{
    public string LineName { get; set; } = "";
    public string Color { get; set; } = "";
    public string Mode { get; set; } = "";
    public List<TransportStation> Stops { get; set; } = new();
    public TransportNetwork Network { get; set; } = null!;
}
