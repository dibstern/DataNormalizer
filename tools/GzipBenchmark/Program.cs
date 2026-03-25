using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

// Find repo root by looking for DataNormalizer.sln
var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
while (dir != null && !File.Exists(Path.Combine(dir.FullName, "DataNormalizer.sln")))
    dir = dir.Parent;
if (dir == null)
{
    Console.Error.WriteLine("Cannot find repo root");
    return 1;
}

var jsonPath = Path.Combine(dir.FullName, "docs", "plans", "search-response.json");
var rawJson = File.ReadAllText(jsonPath);

// Parse
var doc = JsonNode.Parse(rawJson)!;

// === NORMALIZED (as-is) ===
var normalizedMinified = doc.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
var normalizedPretty = doc.ToJsonString(new JsonSerializerOptions { WriteIndented = true });

// === UNNORMALIZE ===
// Extract lookup tables
var transitImages = doc["transitImages"]!.AsArray();
var vehicles = doc["vehicles"]!.AsArray();
var carriers = doc["carriers"]!.AsArray();
var places = doc["places"]!.AsArray();
var paths = doc["paths"]!.AsArray();
var lines = doc["lines"]!.AsArray();
var transitGuides = doc["transitGuides"]!.AsArray();
var hops = doc["hops"]!.AsArray();
var options = doc["options"]!.AsArray();
var segments = doc["segments"]!.AsArray();
var routes = doc["routes"]!.AsArray();

JsonNode? Lookup(JsonArray table, JsonNode? index)
{
    if (index is null) return null;
    var i = index.GetValue<int>();
    if (i < 0 || i >= table.Count) return null;
    return table[i]!.DeepClone();
}

JsonArray? LookupArray(JsonArray table, JsonNode? indices)
{
    if (indices is null) return null;
    var arr = new JsonArray();
    foreach (var idx in indices.AsArray())
    {
        var item = Lookup(table, idx);
        if (item != null)
            arr.Add(item);
    }
    return arr;
}

// Expand places: replace nearbyCity index (one level only, no recursion)
var expandedPlaces = new JsonArray();
foreach (var p in places)
{
    var ep = p!.DeepClone().AsObject();
    if (ep.ContainsKey("nearbyCity"))
    {
        var nearbyCityNode = Lookup(places, ep["nearbyCity"]);
        if (nearbyCityNode != null)
            ep["nearbyCity"] = nearbyCityNode;
    }
    expandedPlaces.Add(ep);
}

// Expand carriers: replace transitImages indices
var expandedCarriers = new JsonArray();
foreach (var c in carriers)
{
    var ec = c!.DeepClone().AsObject();
    if (ec.ContainsKey("transitImages"))
        ec["transitImages"] = LookupArray(transitImages, ec["transitImages"]);
    expandedCarriers.Add(ec);
}

// Expand lines: replace places and path indices
var expandedLines = new JsonArray();
foreach (var l in lines)
{
    var el = l!.DeepClone().AsObject();
    if (el.ContainsKey("places"))
        el["places"] = LookupArray(expandedPlaces, el["places"]);
    if (el.ContainsKey("path"))
    {
        var pathNode = Lookup(paths, el["path"]);
        if (pathNode != null)
            el["path"] = pathNode;
    }
    expandedLines.Add(el);
}

// Expand hops: replace line, marketingCarrier, vehicle, transitImages
var expandedHops = new JsonArray();
foreach (var h in hops)
{
    var eh = h!.DeepClone().AsObject();
    if (eh.ContainsKey("line"))
    {
        var lineNode = Lookup(expandedLines, eh["line"]);
        if (lineNode != null)
            eh["line"] = lineNode;
    }
    if (eh.ContainsKey("marketingCarrier"))
    {
        var carrierNode = Lookup(expandedCarriers, eh["marketingCarrier"]);
        if (carrierNode != null)
            eh["marketingCarrier"] = carrierNode;
    }
    if (eh.ContainsKey("vehicle"))
    {
        var vehicleNode = Lookup(vehicles, eh["vehicle"]);
        if (vehicleNode != null)
            eh["vehicle"] = vehicleNode;
    }
    if (eh.ContainsKey("transitImages"))
        eh["transitImages"] = LookupArray(transitImages, eh["transitImages"]);
    // Expand codeshares carrier references too
    if (eh.ContainsKey("codeshares") && eh["codeshares"] is JsonArray codeshares)
    {
        var expandedCodeshares = new JsonArray();
        foreach (var cs in codeshares)
        {
            var ecs = cs!.DeepClone().AsObject();
            if (ecs.ContainsKey("carrier"))
            {
                var csCarrier = Lookup(expandedCarriers, ecs["carrier"]);
                if (csCarrier != null)
                    ecs["carrier"] = csCarrier;
            }
            expandedCodeshares.Add(ecs);
        }
        eh["codeshares"] = expandedCodeshares;
    }
    expandedHops.Add(eh);
}

// Expand options: replace hops
var expandedOptions = new JsonArray();
foreach (var o in options)
{
    var eo = o!.DeepClone().AsObject();
    if (eo.ContainsKey("hops"))
        eo["hops"] = LookupArray(expandedHops, eo["hops"]);
    expandedOptions.Add(eo);
}

// Expand segments: replace options and transitGuides
var expandedSegments = new JsonArray();
foreach (var s in segments)
{
    var es = s!.DeepClone().AsObject();
    if (es.ContainsKey("options"))
        es["options"] = LookupArray(expandedOptions, es["options"]);
    if (es.ContainsKey("transitGuides"))
        es["transitGuides"] = LookupArray(transitGuides, es["transitGuides"]);
    expandedSegments.Add(es);
}

// Expand routes: replace segments, transitGuides, places, hotelInfo.centerPlace
var expandedRoutes = new JsonArray();
foreach (var r in routes)
{
    var er = r!.DeepClone().AsObject();
    if (er.ContainsKey("segments"))
        er["segments"] = LookupArray(expandedSegments, er["segments"]);
    if (er.ContainsKey("transitGuides"))
        er["transitGuides"] = LookupArray(transitGuides, er["transitGuides"]);
    if (er.ContainsKey("places"))
        er["places"] = LookupArray(expandedPlaces, er["places"]);
    // Expand hotelInfo.centerPlace
    if (
        er.ContainsKey("hotelInfo")
        && er["hotelInfo"] is JsonObject hotelInfo
        && hotelInfo.ContainsKey("centerPlace")
    )
    {
        var centerNode = Lookup(expandedPlaces, hotelInfo["centerPlace"]);
        if (centerNode != null)
            hotelInfo["centerPlace"] = centerNode;
    }
    expandedRoutes.Add(er);
}

// Expand result: replace routes, transitGuides, originPlace, destinationPlace
var result = doc["result"]!.DeepClone().AsObject();
if (result.ContainsKey("routes"))
    result["routes"] = LookupArray(expandedRoutes, result["routes"]);
if (result.ContainsKey("transitGuides"))
    result["transitGuides"] = LookupArray(transitGuides, result["transitGuides"]);
if (result.ContainsKey("originPlace"))
{
    var originNode = Lookup(expandedPlaces, result["originPlace"]);
    if (originNode != null)
        result["originPlace"] = originNode;
}
if (result.ContainsKey("destinationPlace"))
{
    var destNode = Lookup(expandedPlaces, result["destinationPlace"]);
    if (destNode != null)
        result["destinationPlace"] = destNode;
}

// Build unnormalized document: keep request, analytics, adsConfig, timeZones, debug
// But inline the result instead of having separate lookup tables
var unnormalized = new JsonObject();
foreach (var prop in doc.AsObject())
{
    switch (prop.Key)
    {
        case "transitImages":
        case "vehicles":
        case "carriers":
        case "places":
        case "paths":
        case "lines":
        case "transitGuides":
        case "hops":
        case "options":
        case "segments":
        case "routes":
            // These are lookup tables; skip them in unnormalized output
            break;
        case "result":
            unnormalized["result"] = result;
            break;
        default:
            unnormalized[prop.Key] = prop.Value?.DeepClone();
            break;
    }
}

var unnormalizedMinified = unnormalized.ToJsonString(
    new JsonSerializerOptions { WriteIndented = false }
);
var unnormalizedPretty = unnormalized.ToJsonString(
    new JsonSerializerOptions { WriteIndented = true }
);

// === MEASURE SIZES ===
static int GzipSize(string text)
{
    var bytes = Encoding.UTF8.GetBytes(text);
    using var ms = new MemoryStream();
    using (var gz = new GZipStream(ms, CompressionLevel.Optimal, leaveOpen: true))
        gz.Write(bytes, 0, bytes.Length);
    return (int)ms.Length;
}

var normMinRaw = Encoding.UTF8.GetByteCount(normalizedMinified);
var normMinGz = GzipSize(normalizedMinified);
var normPrettyRaw = Encoding.UTF8.GetByteCount(normalizedPretty);
var normPrettyGz = GzipSize(normalizedPretty);

var unnormMinRaw = Encoding.UTF8.GetByteCount(unnormalizedMinified);
var unnormMinGz = GzipSize(unnormalizedMinified);
var unnormPrettyRaw = Encoding.UTF8.GetByteCount(unnormalizedPretty);
var unnormPrettyGz = GzipSize(unnormalizedPretty);

Console.WriteLine("## Minified JSON");
Console.WriteLine();
Console.WriteLine("| Format | Raw | Gzipped | Ratio vs Normalized |");
Console.WriteLine("|--------|-----|---------|---------------------|");
Console.WriteLine(
    $"| Normalized | {normMinRaw / 1024.0:F1} KB | {normMinGz / 1024.0:F1} KB | 1.0x |"
);
Console.WriteLine(
    $"| Unnormalized | {unnormMinRaw / 1024.0:F1} KB | {unnormMinGz / 1024.0:F1} KB | {(double)unnormMinGz / normMinGz:F1}x |"
);
Console.WriteLine();
Console.WriteLine("## Pretty-printed JSON");
Console.WriteLine();
Console.WriteLine("| Format | Raw | Gzipped | Ratio vs Normalized |");
Console.WriteLine("|--------|-----|---------|---------------------|");
Console.WriteLine(
    $"| Normalized | {normPrettyRaw / 1024.0:F1} KB | {normPrettyGz / 1024.0:F1} KB | 1.0x |"
);
Console.WriteLine(
    $"| Unnormalized | {unnormPrettyRaw / 1024.0:F1} KB | {unnormPrettyGz / 1024.0:F1} KB | {(double)unnormPrettyGz / normPrettyGz:F1}x |"
);
Console.WriteLine();
Console.WriteLine($"Raw savings (minified): {(unnormMinRaw - normMinRaw) / 1024.0:F0} KB");
Console.WriteLine($"Gzipped savings (minified): {(unnormMinGz - normMinGz) / 1024.0:F0} KB");

return 0;
