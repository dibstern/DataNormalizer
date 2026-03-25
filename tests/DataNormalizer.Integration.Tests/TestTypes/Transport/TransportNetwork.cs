using System.Collections.Generic;

namespace DataNormalizer.Integration.Tests.TestTypes.Transport;

public sealed class TransportNetwork
{
    public string NetworkName { get; set; } = "";
    public string Region { get; set; } = "";
    public List<TransportLine> Lines { get; set; } = new();
    public List<TransportStation> Stations { get; set; } = new();
}
