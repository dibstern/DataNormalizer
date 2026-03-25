using System.Text.Json;
using DataNormalizer.Integration.Tests.TestTypes.Search;
using DataNormalizer.Integration.Tests.TestTypes.Transport;
using NUnit.Framework;
using SearchOption = DataNormalizer.Integration.Tests.TestTypes.Search.SearchOption;

namespace DataNormalizer.Integration.Tests;

[TestFixture]
public sealed class SearchContractTests
{
    // --- Shared test data helpers ---

    private static SearchPlace MakePlace(string name, string kind, double lat, double lng) =>
        new()
        {
            ShortName = name,
            Kind = kind,
            Lat = lat,
            Lng = lng,
        };

    private static SearchCarrier MakeCarrier(string name, string code) => new() { Name = name, Code = code };

    private static SearchVehicle MakeVehicle(string name, string kind) => new() { Name = name, Kind = kind };

    private static SearchImage MakeImage(string title, string url) => new() { Title = title, ThumbnailUrl = url };

    private static SearchResponse CreateSearchResponse()
    {
        var originPlace = MakePlace("London", "city", 51.5074, -0.1278);
        var destPlace = MakePlace("Paris", "city", 48.8566, 2.3522);
        var sharedPlace = MakePlace("Brussels", "city", 50.8503, 4.3517);
        var carrier = MakeCarrier("Eurostar", "ES");
        var vehicle = MakeVehicle("TGV 9000", "train");
        var image = MakeImage("St Pancras", "https://example.com/st-pancras.jpg");

        var line = new SearchLine
        {
            Places = new() { originPlace, sharedPlace, destPlace },
            Path = "M51.5,-0.1 L48.8,2.3",
        };

        var hop = new SearchHop
        {
            Line = line,
            MarketingCarrier = carrier,
            Vehicle = vehicle,
            TransitImages = new() { image },
        };

        var option = new SearchOption { Hops = new() { hop } };
        var segment = new SearchSegment { Options = new() { option } };

        var route = new SearchRoute
        {
            Segments = new() { segment },
            Places = new() { originPlace, sharedPlace },
        };

        return new SearchResponse
        {
            Routes = new() { route },
            Places = new() { originPlace, destPlace, sharedPlace },
            OriginPlace = originPlace,
            DestinationPlace = destPlace,
        };
    }

    // --- Step 5: Search contract tests ---

    [Test]
    public void Normalize_SearchResponse_ProducesValidContainer()
    {
        var response = CreateSearchResponse();
        var result = SearchNormalizationConfig.Normalize(response);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Result, Is.Not.Null);
    }

    [Test]
    public void Normalize_SearchResponse_JsonHasResultProperty()
    {
        var response = CreateSearchResponse();
        var result = SearchNormalizationConfig.Normalize(response);
        var json = JsonSerializer.Serialize(result);

        // RootPropertyName = "result"
        Assert.That(json, Does.Contain("\"result\""));
    }

    [Test]
    public void Normalize_SearchResponse_ContainerDoesNotHaveSearchResponseDtosList()
    {
        var response = CreateSearchResponse();
        var result = SearchNormalizationConfig.Normalize(response);

        // Container should NOT have a SearchResponseDtos property (NeedsList=false)
        var containerType = result.GetType();
        var listProp = containerType.GetProperty("SearchResponseDtos");
        Assert.That(listProp, Is.Null, "Container should not have SearchResponseDtos list");
    }

    [Test]
    public void Normalize_SearchResponse_JsonDoesNotContainSearchResponseDtos()
    {
        var response = CreateSearchResponse();
        var result = SearchNormalizationConfig.Normalize(response);
        var json = JsonSerializer.Serialize(result);

        Assert.That(json, Does.Not.Contain("\"searchResponseDtos\""));
    }

    [Test]
    public void Normalize_SearchResponse_JsonHasCustomCollectionNames()
    {
        var response = CreateSearchResponse();
        var result = SearchNormalizationConfig.Normalize(response);
        var json = JsonSerializer.Serialize(result);

        // Collection<T> overrides
        Assert.That(json, Does.Contain("\"routes\""));
        Assert.That(json, Does.Contain("\"segments\""));
        Assert.That(json, Does.Contain("\"options\""));
        Assert.That(json, Does.Contain("\"hops\""));
        Assert.That(json, Does.Contain("\"lines\""));
        Assert.That(json, Does.Contain("\"places\""));
        Assert.That(json, Does.Contain("\"carriers\""));
        Assert.That(json, Does.Contain("\"vehicles\""));
        Assert.That(json, Does.Contain("\"transitImages\""));
    }

    [Test]
    public void Normalize_SearchResponse_JsonDoesNotContainDefaultDtoListNames()
    {
        var response = CreateSearchResponse();
        var result = SearchNormalizationConfig.Normalize(response);
        var json = JsonSerializer.Serialize(result);

        // Should NOT contain default camelCase list names
        Assert.That(json, Does.Not.Contain("\"searchRouteDtos\""));
        Assert.That(json, Does.Not.Contain("\"searchPlaceDtos\""));
        Assert.That(json, Does.Not.Contain("\"searchLineDtos\""));
        Assert.That(json, Does.Not.Contain("\"searchCarrierDtos\""));
    }

    [Test]
    public void Normalize_SearchHop_JsonHasPerPropertyNameOverrides()
    {
        var response = CreateSearchResponse();
        var result = SearchNormalizationConfig.Normalize(response);
        var json = JsonSerializer.Serialize(result);

        // Hop DTO should have per-property JSON name overrides
        // Parse JSON and look at the hops array items
        var doc = JsonDocument.Parse(json);
        var hopsArray = doc.RootElement.GetProperty("hops");
        Assert.That(hopsArray.GetArrayLength(), Is.GreaterThan(0));

        var hop = hopsArray[0];
        // Reference().JsonName("line") → "line" instead of "lineIndex"
        Assert.That(hop.TryGetProperty("line", out _), Is.True, "Hop should have 'line' property");
        // Reference().JsonName("marketingCarrier") → "marketingCarrier" instead of "marketingCarrierIndex"
        Assert.That(
            hop.TryGetProperty("marketingCarrier", out _),
            Is.True,
            "Hop should have 'marketingCarrier' property"
        );
        // Reference().JsonName("vehicle") → "vehicle" instead of "vehicleIndex"
        Assert.That(hop.TryGetProperty("vehicle", out _), Is.True, "Hop should have 'vehicle' property");
        // ReferenceCollection().JsonName("transitImages") → "transitImages" instead of "transitImagesIndices"
        Assert.That(hop.TryGetProperty("transitImages", out _), Is.True, "Hop should have 'transitImages' property");
    }

    [Test]
    public void Normalize_SharedPlaces_DeduplicatesToSameIndex()
    {
        var response = CreateSearchResponse();
        var result = SearchNormalizationConfig.Normalize(response);

        // OriginPlace and Places[0] are the same instance — should have same index
        Assert.That(
            result.Result.OriginPlaceIndex,
            Is.EqualTo(result.Result.PlacesIndices[0]),
            "Origin and first Places entry should share the same index"
        );
    }

    [Test]
    public void Normalize_SharedPlaces_SinglePlaceEntry()
    {
        var sharedPlace = MakePlace("SharedCity", "city", 10.0, 20.0);
        var response = new SearchResponse
        {
            Routes = new(),
            Places = new() { sharedPlace, sharedPlace },
            OriginPlace = sharedPlace,
            DestinationPlace = sharedPlace,
        };

        var result = SearchNormalizationConfig.Normalize(response);

        // All references to the same place should dedup to one entry
        Assert.That(result.SearchPlaceDtos, Has.Length.EqualTo(1));
        Assert.That(result.Result.OriginPlaceIndex, Is.EqualTo(0));
        Assert.That(result.Result.DestinationPlaceIndex, Is.EqualTo(0));
        Assert.That(result.Result.PlacesIndices[0], Is.EqualTo(0));
        Assert.That(result.Result.PlacesIndices[1], Is.EqualTo(0));
    }

    [Test]
    public void Normalize_NullableCarrier_Null_ProducesNullIndex()
    {
        var hop = new SearchHop
        {
            Line = new SearchLine { Path = "test" },
            MarketingCarrier = null,
            Vehicle = MakeVehicle("Bus", "bus"),
        };
        var option = new SearchOption { Hops = new() { hop } };
        var segment = new SearchSegment { Options = new() { option } };
        var route = new SearchRoute { Segments = new() { segment } };
        var response = new SearchResponse
        {
            Routes = new() { route },
            OriginPlace = MakePlace("A", "stop", 0, 0),
            DestinationPlace = MakePlace("B", "stop", 0, 0),
        };

        var result = SearchNormalizationConfig.Normalize(response);
        var hopDto = result.SearchHopDtos[0];

        Assert.That(hopDto.MarketingCarrierIndex, Is.Null);
    }

    [Test]
    public void Normalize_NullableCarrier_Populated_ProducesValidIndex()
    {
        var carrier = MakeCarrier("TestAir", "TA");
        var hop = new SearchHop
        {
            Line = new SearchLine { Path = "test" },
            MarketingCarrier = carrier,
            Vehicle = MakeVehicle("Plane", "aircraft"),
        };
        var option = new SearchOption { Hops = new() { hop } };
        var segment = new SearchSegment { Options = new() { option } };
        var route = new SearchRoute { Segments = new() { segment } };
        var response = new SearchResponse
        {
            Routes = new() { route },
            OriginPlace = MakePlace("A", "stop", 0, 0),
            DestinationPlace = MakePlace("B", "stop", 0, 0),
        };

        var result = SearchNormalizationConfig.Normalize(response);
        var hopDto = result.SearchHopDtos[0];

        Assert.That(hopDto.MarketingCarrierIndex, Is.Not.Null);
        Assert.That(hopDto.MarketingCarrierIndex, Is.GreaterThanOrEqualTo(0));
        Assert.That(result.SearchCarrierDtos[hopDto.MarketingCarrierIndex!.Value].Name, Is.EqualTo("TestAir"));
    }

    [Test]
    public void Roundtrip_SearchResponse_PreservesAllData()
    {
        var response = CreateSearchResponse();

        // Normalize → Serialize → Deserialize → Denormalize
        var container = SearchNormalizationConfig.Normalize(response);
        var json = JsonSerializer.Serialize(container);
        var deserialized = JsonSerializer.Deserialize<SearchResponseResultDto>(json)!;
        var restored = SearchNormalizationConfig.Denormalize(deserialized);

        // Verify root-level data
        Assert.That(restored.Routes, Has.Count.EqualTo(1));
        Assert.That(restored.Places, Has.Count.EqualTo(3));
        Assert.That(restored.OriginPlace.ShortName, Is.EqualTo("London"));
        Assert.That(restored.DestinationPlace.ShortName, Is.EqualTo("Paris"));

        // Verify nested data
        var restoredRoute = restored.Routes[0];
        Assert.That(restoredRoute.Segments, Has.Count.EqualTo(1));
        Assert.That(restoredRoute.Places, Has.Count.EqualTo(2));

        var restoredSegment = restoredRoute.Segments[0];
        Assert.That(restoredSegment.Options, Has.Count.EqualTo(1));

        var restoredOption = restoredSegment.Options[0];
        Assert.That(restoredOption.Hops, Has.Count.EqualTo(1));

        var restoredHop = restoredOption.Hops[0];
        Assert.That(restoredHop.Line.Path, Is.EqualTo("M51.5,-0.1 L48.8,2.3"));
        Assert.That(restoredHop.Line.Places, Has.Count.EqualTo(3));
        Assert.That(restoredHop.MarketingCarrier, Is.Not.Null);
        Assert.That(restoredHop.MarketingCarrier!.Name, Is.EqualTo("Eurostar"));
        Assert.That(restoredHop.MarketingCarrier.Code, Is.EqualTo("ES"));
        Assert.That(restoredHop.Vehicle.Name, Is.EqualTo("TGV 9000"));
        Assert.That(restoredHop.Vehicle.Kind, Is.EqualTo("train"));
        Assert.That(restoredHop.TransitImages, Has.Count.EqualTo(1));
        Assert.That(restoredHop.TransitImages[0].Title, Is.EqualTo("St Pancras"));
    }

    [Test]
    public void Roundtrip_SharedPlaces_RestoreSameReference()
    {
        var response = CreateSearchResponse();

        var container = SearchNormalizationConfig.Normalize(response);
        var json = JsonSerializer.Serialize(container);
        var deserialized = JsonSerializer.Deserialize<SearchResponseResultDto>(json)!;
        var restored = SearchNormalizationConfig.Denormalize(deserialized);

        // Origin and first Places entry should be the same object after roundtrip
        Assert.That(restored.OriginPlace, Is.SameAs(restored.Places[0]));
    }

    [Test]
    public void Roundtrip_NullCarrier_StaysNull()
    {
        var hop = new SearchHop
        {
            Line = new SearchLine { Path = "test" },
            MarketingCarrier = null,
            Vehicle = MakeVehicle("Bus", "bus"),
        };
        var option = new SearchOption { Hops = new() { hop } };
        var segment = new SearchSegment { Options = new() { option } };
        var route = new SearchRoute { Segments = new() { segment } };
        var response = new SearchResponse
        {
            Routes = new() { route },
            OriginPlace = MakePlace("A", "stop", 0, 0),
            DestinationPlace = MakePlace("B", "stop", 0, 0),
        };

        var container = SearchNormalizationConfig.Normalize(response);
        var json = JsonSerializer.Serialize(container);
        var deserialized = JsonSerializer.Deserialize<SearchResponseResultDto>(json)!;
        var restored = SearchNormalizationConfig.Denormalize(deserialized);

        Assert.That(restored.Routes[0].Segments[0].Options[0].Hops[0].MarketingCarrier, Is.Null);
    }

    // --- Step 8: Default Result name ---

    [Test]
    public void DefaultConfig_WithoutRootPropertyName_JsonUsesResult()
    {
        // BasicNormalizationConfig does NOT set RootPropertyName
        var person = new TestTypes.Person
        {
            Name = "Alice",
            Age = 30,
            HomeAddress = new TestTypes.Address
            {
                Street = "123 Main St",
                City = "Springfield",
                ZipCode = "62701",
            },
        };

        var result = BasicNormalizationConfig.Normalize(person);
        var json = JsonSerializer.Serialize(result);

        // Default root property name should be "result"
        Assert.That(json, Does.Contain("\"result\""));
    }

    // --- Step 9: Transport network tests (NeedsList=true) ---

    [Test]
    [CancelAfter(5000)]
    public void Normalize_TransportNetwork_ContainerHasBothResultAndList()
    {
        var network = CreateTransportNetwork();
        var result = TransportNormalizationConfig.Normalize(network);

        // Container should have BOTH Result AND TransportNetworkDtos list
        // because TransportLine back-references TransportNetwork (NeedsList=true)
        var containerType = result.GetType();
        var resultProp = containerType.GetProperty("Result");
        var listProp = containerType.GetProperty("TransportNetworkDtos");

        Assert.That(resultProp, Is.Not.Null, "Container should have Result property");
        Assert.That(listProp, Is.Not.Null, "Container should have TransportNetworkDtos list (NeedsList=true)");
        Assert.That(result.TransportNetworkDtos.Length, Is.GreaterThan(0));
    }

    [Test]
    [CancelAfter(5000)]
    public void Normalize_TransportNetwork_JsonHasCustomCollectionNames()
    {
        var network = CreateTransportNetwork();
        var result = TransportNormalizationConfig.Normalize(network);
        var json = JsonSerializer.Serialize(result);

        Assert.That(json, Does.Contain("\"lines\""));
        Assert.That(json, Does.Contain("\"stations\""));
        Assert.That(json, Does.Contain("\"result\""));
    }

    [Test]
    [CancelAfter(5000)]
    public void Roundtrip_TransportNetwork_PreservesData()
    {
        var network = CreateTransportNetwork();

        var container = TransportNormalizationConfig.Normalize(network);
        var json = JsonSerializer.Serialize(container);
        var deserialized = JsonSerializer.Deserialize<TransportNetworkResultDto>(json)!;
        var restored = TransportNormalizationConfig.Denormalize(deserialized);

        Assert.That(restored.NetworkName, Is.EqualTo("London Underground"));
        Assert.That(restored.Region, Is.EqualTo("London"));
        Assert.That(restored.Lines, Has.Count.EqualTo(1));
        Assert.That(restored.Lines[0].LineName, Is.EqualTo("Victoria Line"));
        Assert.That(restored.Lines[0].Color, Is.EqualTo("#009DDC"));
        Assert.That(restored.Lines[0].Mode, Is.EqualTo("metro"));
        Assert.That(restored.Stations, Has.Count.EqualTo(2));
    }

    [Test]
    [CancelAfter(5000)]
    public void Roundtrip_TransportNetwork_BackReferencePreserved()
    {
        var network = CreateTransportNetwork();

        var container = TransportNormalizationConfig.Normalize(network);
        var json = JsonSerializer.Serialize(container);
        var deserialized = JsonSerializer.Deserialize<TransportNetworkResultDto>(json)!;
        var restored = TransportNormalizationConfig.Denormalize(deserialized);

        // TransportLine.Network should reference the root network
        var line = restored.Lines[0];
        Assert.That(line.Network, Is.Not.Null);
        Assert.That(line.Network.NetworkName, Is.EqualTo("London Underground"));
        // Should be the same instance as the root
        Assert.That(line.Network, Is.SameAs(restored));
    }

    private static TransportNetwork CreateTransportNetwork()
    {
        var network = new TransportNetwork { NetworkName = "London Underground", Region = "London" };

        var station1 = new TransportStation
        {
            StationName = "Victoria",
            Latitude = 51.4965,
            Longitude = -0.1447,
            Zone = "1",
        };
        var station2 = new TransportStation
        {
            StationName = "King's Cross",
            Latitude = 51.5308,
            Longitude = -0.1238,
            Zone = "1",
        };

        var line = new TransportLine
        {
            LineName = "Victoria Line",
            Color = "#009DDC",
            Mode = "metro",
            Stops = new() { station1, station2 },
            Network = network, // back-reference
        };

        station1.ServingLines = new() { line };
        station2.ServingLines = new() { line };

        network.Lines = new() { line };
        network.Stations = new() { station1, station2 };

        return network;
    }
}
