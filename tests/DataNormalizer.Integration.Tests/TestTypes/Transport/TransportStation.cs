using System.Collections.Generic;

namespace DataNormalizer.Integration.Tests.TestTypes.Transport;

public sealed class TransportStation
{
    public string StationName { get; set; } = "";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string Zone { get; set; } = "";
    public List<TransportLine> ServingLines { get; set; } = new();
}
