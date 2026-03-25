# README & Docs Site Overhaul Implementation Plan

> **For Agent:** REQUIRED SUB-SKILL: Use executing-plans to implement this plan task-by-task.

**Goal:** Rewrite the README with a problem-first pitch and transport route example, create a gzip benchmark script with real numbers, and update all DocFX documentation articles to reflect naming policy, JSON contract features, and the new container shape (`Result` property).

**Architecture:** A standalone console app generates benchmark numbers from `search-response.json`. Those numbers are embedded in the README and a new DocFX article. All existing docs are updated to show `result.Result` instead of `list[0]`, and new articles cover naming/contracts and gzip analysis.

**Tech Stack:** C# 12, .NET 9, System.Text.Json, System.IO.Compression, DocFX

**Key design decisions:**
- README is short and persuasive -- configuration details live in docs site
- Transport route example replaces Team/Person/Address in the README (Getting Started keeps the simple example)
- Real benchmark numbers from a standalone script, not ChatGPT estimates
- New articles: `naming-and-contracts.md` and `why-gzip-isnt-enough.md`
- All existing articles updated for `result.Result` container shape and new diagnostics

---

### Task 1: Create the gzip benchmark script

**Files:**
- Create: `tools/GzipBenchmark/GzipBenchmark.csproj`
- Create: `tools/GzipBenchmark/Program.cs`

**Step 1: Create the project**

```bash
mkdir -p tools/GzipBenchmark
```

Create `tools/GzipBenchmark/GzipBenchmark.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
</Project>
```

**Step 2: Write `Program.cs`**

The script:
1. Loads `docs/plans/search-response.json` (path relative to repo root)
2. Parses it as a `JsonDocument`
3. Measures the minified normalized size (raw + gzipped)
4. Programmatically unnormalizes it by expanding index references:
   - For each hop: replace `line` (int) with the full `lines[i]` object, replace `marketingCarrier` with `carriers[i]`, replace `vehicle` with `vehicles[i]`, replace `transitImages` (int[]) with array of full `transitImages[i]` objects
   - For each line: replace `places` (int[]) with full `places[i]` objects, replace `path` (int) with `paths[i]` string
   - For each option: replace `hops` (int[]) with full expanded hop objects
   - For each segment: replace `options` (int[]) with full expanded option objects, replace `transitGuides` (int[]) with full objects
   - For each route: replace `segments` (int[]) with full expanded segments, replace `transitGuides`, `places`
   - For result: replace `routes`, `transitGuides`, `originPlace`, `destinationPlace`
   - For each carrier: replace `transitImages` (int[]) with full objects
   - For each place: replace `nearbyCity` (int) with full place object (one level only -- do not recurse into the expanded place's own `nearbyCity`)
   - For each route: replace `hotelInfo.centerPlace` (int) with full `places[i]` object
5. Serializes the unnormalized tree as minified JSON
6. Measures raw + gzipped sizes
7. Also measures pretty-printed sizes for both
8. Prints a markdown table

```csharp
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

// Find repo root by looking for DataNormalizer.sln
var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
while (dir != null && !File.Exists(Path.Combine(dir.FullName, "DataNormalizer.sln")))
    dir = dir.Parent;
if (dir == null) { Console.Error.WriteLine("Cannot find repo root"); return 1; }

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
        if (item != null) arr.Add(item);
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
        if (lineNode != null) eh["line"] = lineNode;
    }
    if (eh.ContainsKey("marketingCarrier"))
    {
        var carrierNode = Lookup(expandedCarriers, eh["marketingCarrier"]);
        if (carrierNode != null) eh["marketingCarrier"] = carrierNode;
    }
    if (eh.ContainsKey("vehicle"))
    {
        var vehicleNode = Lookup(vehicles, eh["vehicle"]);
        if (vehicleNode != null) eh["vehicle"] = vehicleNode;
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
                if (csCarrier != null) ecs["carrier"] = csCarrier;
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
    if (er.ContainsKey("hotelInfo") && er["hotelInfo"] is JsonObject hotelInfo
        && hotelInfo.ContainsKey("centerPlace"))
    {
        var centerNode = Lookup(expandedPlaces, hotelInfo["centerPlace"]);
        if (centerNode != null) hotelInfo["centerPlace"] = centerNode;
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
    if (originNode != null) result["originPlace"] = originNode;
}
if (result.ContainsKey("destinationPlace"))
{
    var destNode = Lookup(expandedPlaces, result["destinationPlace"]);
    if (destNode != null) result["destinationPlace"] = destNode;
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

var unnormalizedMinified = unnormalized.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
var unnormalizedPretty = unnormalized.ToJsonString(new JsonSerializerOptions { WriteIndented = true });

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
Console.WriteLine($"| Normalized | {normMinRaw / 1024.0:F1} KB | {normMinGz / 1024.0:F1} KB | 1.0x |");
Console.WriteLine($"| Unnormalized | {unnormMinRaw / 1024.0:F1} KB | {unnormMinGz / 1024.0:F1} KB | {(double)unnormMinGz / normMinGz:F1}x |");
Console.WriteLine();
Console.WriteLine("## Pretty-printed JSON");
Console.WriteLine();
Console.WriteLine("| Format | Raw | Gzipped | Ratio vs Normalized |");
Console.WriteLine("|--------|-----|---------|---------------------|");
Console.WriteLine($"| Normalized | {normPrettyRaw / 1024.0:F1} KB | {normPrettyGz / 1024.0:F1} KB | 1.0x |");
Console.WriteLine($"| Unnormalized | {unnormPrettyRaw / 1024.0:F1} KB | {unnormPrettyGz / 1024.0:F1} KB | {(double)unnormPrettyGz / normPrettyGz:F1}x |");
Console.WriteLine();
Console.WriteLine($"Raw savings (minified): {(unnormMinRaw - normMinRaw) / 1024.0:F0} KB");
Console.WriteLine($"Gzipped savings (minified): {(unnormMinGz - normMinGz) / 1024.0:F0} KB");

return 0;
```

**Step 3: Run the benchmark**

```bash
dotnet run --project tools/GzipBenchmark
```

Record the output. These numbers will be used in Tasks 2 and 5.

**Step 4: Commit**

```
feat: add gzip benchmark script for normalization vs compression analysis
```

---

### Task 2: Rewrite README.md

**Files:**
- Modify: `README.md`

**Step 1: Replace the entire README**

Use the benchmark numbers from Task 1. The new README follows this structure:

1. **Title + tagline** (problem-focused, not mechanism-focused)
2. **Badges + doc links**
3. **The Problem** (3 sentences about repeated objects in API responses)
4. **What DataNormalizer Does** (2 sentences + transport example)
   - Show a simplified transport graph: `SearchResponse` → routes → hops → shared carriers/places
   - Show the normalized JSON output with `result`, typed arrays, integer refs
5. **Normalization + Gzip** (benchmark table from Task 1, "normalize first, gzip second" tagline, link to full analysis)
6. **Quick Start** (installation + 3 steps with transport-flavored types)
   - Types: `SearchResponse`, `Route`, `Hop`, `Carrier`, `Place`
   - Config with `NormalizeGraph<SearchResponse>()`
   - `result.Result` for root, `result.RouteDtos` for collections
7. **When To Use It** (good for / not ideal for)
8. **Documentation** (links to DocFX articles)
9. **Target Frameworks** (table)
10. **License**

Use the transport example types (simplified -- not the full search-response.json complexity). Something like:

```csharp
public class SearchResponse
{
    public List<Route> Routes { get; set; }
    public Place Origin { get; set; }
    public Place Destination { get; set; }
}

public class Route
{
    public string Name { get; set; }
    public List<Hop> Hops { get; set; }
}

public class Hop
{
    public Carrier Carrier { get; set; }
    public Place Departure { get; set; }
    public Place Arrival { get; set; }
    public int DurationMinutes { get; set; }
}

public class Carrier
{
    public string Name { get; set; }
    public string Code { get; set; }
}

public class Place
{
    public string Name { get; set; }
    public double Lat { get; set; }
    public double Lng { get; set; }
}
```

The normalized JSON output (simplified):

```json
{
  "result": {
    "routesIndices": [0, 1],
    "originIndex": 0,
    "destinationIndex": 1
  },
  "routeDtos": [
    { "name": "Fly", "hopsIndices": [0, 1] },
    { "name": "Train", "hopsIndices": [2] }
  ],
  "hopDtos": [
    { "carrierIndex": 0, "departureIndex": 0, "arrivalIndex": 2, "durationMinutes": 30 },
    { "carrierIndex": 1, "departureIndex": 2, "arrivalIndex": 1, "durationMinutes": 85 },
    { "carrierIndex": 2, "departureIndex": 0, "arrivalIndex": 1, "durationMinutes": 660 }
  ],
  "carrierDtos": [
    { "name": "SkyBus", "code": "SKYBUS" },
    { "name": "Qantas", "code": "QF" },
    { "name": "NSW TrainLink", "code": "XPT" }
  ],
  "placeDtos": [
    { "name": "Melbourne", "lat": -37.814, "lng": 144.963 },
    { "name": "Sydney", "lat": -33.865, "lng": 151.207 },
    { "name": "Melbourne Airport", "lat": -37.670, "lng": 144.849 }
  ]
}
```

Call out: "Melbourne appears once. Carriers are stored once. Each hop references them by index."

For the gzip section, use a table like:

```markdown
Measured on a real transport search API response ([source](docs/plans/search-response.json)):

| Format | Raw | Gzipped |
|--------|-----|---------|
| Unnormalized | XXX KB | XXX KB |
| Normalized | XXX KB | XXX KB |

Even after gzip, the normalized version is **X.Xx smaller**.
```

**Step 2: Review the README**

Read through the full README to verify:
- `result.Result` is used (not `list[0]`)
- Transport example makes sense
- Benchmark numbers are filled in
- All doc links are correct
- No Team/Person/Address references remain

**Step 3: Commit**

```
docs: rewrite README with problem-first pitch, transport example, and gzip benchmarks
```

---

### Task 3: Create `docs/articles/why-gzip-isnt-enough.md`

**Files:**
- Create: `docs/articles/why-gzip-isnt-enough.md`

**Step 1: Write the article**

Structure:
1. **The question** -- "If my API responses are gzipped, do I still need normalization?"
2. **The short answer** -- Yes. Gzip compresses repeated bytes; normalization removes repeated structure. They stack.
3. **The benchmark** -- Full table from Task 1 (minified + pretty-printed rows)
4. **Why gzip doesn't fully cancel normalization** -- Three reasons:
   - Repeated objects are not byte-identical in context (surrounded by different JSON, different field ordering in practice)
   - Inlining duplicates entire subtrees (carrier → transitImages → ...), not just leaf strings
   - Integer indexes are extremely cheap: `"carrier": 2` is a few bytes; the full carrier object is hundreds
5. **What normalization gives beyond compression**
   - Explicit shared references (one canonical copy per entity)
   - Simpler client state management
   - Easier caching, diffing, and patching
   - Reduced parse/allocation work
6. **The bottom line** -- "Normalize first, gzip second."
7. **Getting started** -- Link to Getting Started and Configuration Guide

Use the real benchmark numbers from Task 1 throughout.

**Step 2: Commit**

```
docs: add 'Why Gzip Isn't Enough' article with real benchmark numbers
```

---

### Task 4: Create `docs/articles/naming-and-contracts.md`

**Files:**
- Create: `docs/articles/naming-and-contracts.md`

**Step 1: Write the article**

Structure:

1. **Overview** -- DataNormalizer generates semantic CLR names (`LineIndex`, `CarrierDtos`) and lets you configure the JSON wire format independently via `[JsonPropertyName]` attributes.

2. **Naming Policy** (`UseNaming()`)
   - `DtoSuffix` (default: `"Dto"`) -- suffix for generated DTO types
   - `DtoPrefix` (default: `""`) -- prefix for generated DTO types
   - `ContainerSuffix` (default: `"Dto"`) -- suffix for the container result type
   - `EmitJsonPropertyNames` (default: `true`) -- whether to emit `[JsonPropertyName]` on generated properties
   - Global config: `builder.UseNaming(n => { ... })`
   - Per-graph config: `graph.UseNaming(n => { ... })`
   - Example:

```csharp
builder.UseNaming(n =>
{
    n.DtoSuffix = "Dto";
    n.EmitJsonPropertyNames = true;
});
```

3. **JSON Contract Customization** (`UseJsonContract()`)
   - `RootPropertyName` -- override the JSON name of the root `Result` property (default: `"result"`)
   - `Collection<T>("jsonName")` -- override the JSON name of a type's collection in the container
   - Example with transport search:

```csharp
builder.NormalizeGraph<SearchResponse>(graph =>
{
    graph.UseJsonContract(c =>
    {
        c.RootPropertyName = "result";
        c.Collection<Route>("routes");
        c.Collection<Hop>("hops");
        c.Collection<Carrier>("carriers");
        c.Collection<Place>("places");
    });
});
```

   Generated container:
```csharp
public partial class SearchResponseResultDto
{
    [JsonPropertyName("result")]
    public SearchResponseDto Result { get; set; }

    [JsonPropertyName("routes")]
    public RouteDto[] RouteDtos { get; set; }

    [JsonPropertyName("hops")]
    public HopDto[] HopDtos { get; set; }
    // ...
}
```

4. **Reference JSON Names** (`Reference().JsonName()`)
   - Override the JSON name of individual reference properties
   - Example:

```csharp
builder.ForType<Hop>(x =>
{
    x.Reference(p => p.Carrier).JsonName("carrier");
    x.Reference(p => p.Departure).JsonName("departure");
    x.Reference(p => p.Arrival).JsonName("arrival");
});
```

   Generated DTO:
```csharp
public partial class HopDto
{
    [JsonPropertyName("carrier")]
    public int CarrierIndex { get; set; }

    [JsonPropertyName("departure")]
    public int DepartureIndex { get; set; }
    // ...
}
```

5. **`[NormalizeJsonName]` attribute** -- alternative to config for per-property overrides:

```csharp
public class Hop
{
    [NormalizeJsonName("carrier")]
    public Carrier Carrier { get; set; }
}
```

   Priority order: config `Reference().JsonName()` > `[NormalizeJsonName]` attribute > default camelCase

6. **Matching an existing wire format** -- complete example showing how to configure DataNormalizer to match an existing API contract, combining `UseNaming`, `UseJsonContract`, and `Reference().JsonName()`.

7. **Root property behavior**
   - Every container has a `Result` property (C# name) with a configurable JSON name
   - If the root type is not referenced by any other type: no list array for the root type
   - If the root type IS referenced by other types: both `Result` and the root list array exist

**Step 2: Commit**

```
docs: add Naming & JSON Contracts article
```

---

### Task 5: Update `docs/articles/getting-started.md`

**Files:**
- Modify: `docs/articles/getting-started.md`

**Step 1: Update the article**

Changes:
- In "Working with the result" section, replace `result.TeamList[0]` with `result.Result`
- **Remove ALL `TeamList` references** -- Team is the root and not referenced by other types, so `NeedsList = false` and no `TeamList` property exists on the container
- Update the container access example to show `Result` as the entry point
- Remove "always at index 0" language throughout
- Update generated type descriptions to use `Dto` suffix:
  - Line 85: `Normalized{TypeName}` → `{TypeName}Dto`
  - Line 93: `Normalized{RootType}Result` → `{RootType}ResultDto`
  - Line 98: `result.TeamList[0]` → `result.Result`
  - Lines 100-103: `result.TeamList` → removed (no root list), `NormalizedTeam[]` → `TeamDto`, `NormalizedPerson[]` → `PersonDto[]`, `NormalizedAddress[]` → `AddressDto[]`, property names `PersonList` → `PersonDtos`, `AddressList` → `AddressDtos`
- Add "Next steps" link to the new Naming & JSON Contracts article
- Update "DN0001–DN0004" reference (line 112) to "DN0001–DN1002"
- Keep Team/Person/Address as the tutorial example (it's simpler for getting started)

The "Working with the result" section becomes:

```csharp
var result = AppNormalization.Normalize(team);

result.Result                            // The root DTO
result.Result.Name                       // "Engineering"
result.Result.MembersIndices             // [0, 1]

result.PersonDtos                        // PersonDto[] (typed array)
result.AddressDtos                       // AddressDto[] (typed array)
// All collections are typed properties — no string-keyed lookups.
// The container serializes directly with System.Text.Json.
```

**Step 2: Expand "What the source generator produces" into "How It Works"**

Rename the section to "How It Works" and expand it to include:

1. **Per-type DTOs** (`{TypeName}Dto`) -- partial classes implementing `IEquatable<T>` for value-based deduplication. Nested object references become `int` index properties (`{Name}Index`), collections become `int[]` (`{Name}Indices`). Types marked as inline keep their original structure.

2. **A container result** (`{RootType}ResultDto`) -- holds a `Result` property for the root entity and typed array properties for every other entity type in the graph. If the root type is also referenced by other types, a root list array is included too.

3. **`Normalize(T)` / `Denormalize({RootType}ResultDto)`** -- static methods on the configuration class. `Normalize` flattens the graph using value-equality-based deduplication. `Denormalize` reconstructs the original object graph with shared references preserved.

4. **Naming and JSON serialization** -- By default, DTOs get a `Dto` suffix and `[JsonPropertyName]` attributes for camelCase JSON. Both are configurable via `UseNaming()`. The JSON wire format can be further customized with `UseJsonContract()` and `Reference().JsonName()`.

All generated types are `partial`, so you can extend them with additional members.

**Step 3: Verify all code examples are consistent**

Read through the file to check that Team/Person/Address types, config, and result access are consistent with the new container shape. Verify no references to `Normalized{TypeName}`, `{X}List`, or `list[0]` remain.

**Step 4: Commit**

```
docs: update Getting Started with How It Works section, Result property, and Dto naming
```

---

### Task 6: Update `docs/articles/configuration.md`

**Files:**
- Modify: `docs/articles/configuration.md`

**Step 1: Update existing sections**

Enumerate ALL stale references and update them:
- Line 50: `Normalized{TypeName}` → `{TypeName}Dto`
- Lines 87-88: `NormalizedTeamResult` → `TeamResultDto`, `NormalizedOrderResult` → `OrderResultDto`
- Line 96: `Normalized{RootType}Result` → `{RootType}ResultDto`
- Lines 100-105: `result.TeamList[0]` → `result.Result`, remove `result.TeamList` (root has no list when not referenced by other types), `NormalizedTeam[]` → `TeamDto`, `NormalizedPerson[]` → `PersonDto[]`, `NormalizedAddress[]` → `AddressDto[]`, `PersonList` → `PersonDtos`, `AddressList` → `AddressDtos`
- Remove "always at index 0" language
- Add explanation of root property behavior: root always gets `Result` property, only gets a list array if other types reference it

**Step 2: Add Naming Policy section**

After the "Multiple root types" section, add:

```markdown
## Naming Policy

Control the naming of generated DTO types and their JSON serialization:

\```csharp
builder.UseNaming(n =>
{
    n.DtoSuffix = "Dto";          // suffix for DTO types (default: "Dto")
    n.DtoPrefix = "";              // prefix for DTO types (default: "")
    n.ContainerSuffix = "Dto";    // suffix for container type (default: "Dto")
    n.EmitJsonPropertyNames = true; // emit [JsonPropertyName] attributes (default: true)
});
\```

Naming can be configured globally or per-graph:

\```csharp
builder.NormalizeGraph<Team>(graph =>
{
    graph.UseNaming(n => { n.DtoSuffix = "Model"; });
});
\```

For full details, see [Naming & JSON Contracts](naming-and-contracts.md).
```

**Step 3: Add JSON Contract section**

```markdown
## JSON Contract Customization

Control the JSON wire format of the container:

\```csharp
builder.NormalizeGraph<SearchResponse>(graph =>
{
    graph.UseJsonContract(c =>
    {
        c.RootPropertyName = "result";
        c.Collection<Route>("routes");
        c.Collection<Place>("places");
    });
});
\```

Override individual reference property JSON names:

\```csharp
builder.ForType<Hop>(x =>
{
    x.Reference(p => p.Carrier).JsonName("carrier");
});
\```

Or use the `[NormalizeJsonName]` attribute:

\```csharp
public class Hop
{
    [NormalizeJsonName("carrier")]
    public Carrier Carrier { get; set; }
}
\```

For full details, see [Naming & JSON Contracts](naming-and-contracts.md).
```

**Step 4: Commit**

```
docs: update Configuration Guide with naming policy and JSON contract sections
```

---

### Task 7: Update `docs/articles/diagnostics.md`

**Files:**
- Modify: `docs/articles/diagnostics.md`

**Step 1: Add DN1001 and DN1002 to the diagnostics table**

Add two rows to the table:

```markdown
| DN1001 | Error    | Unparsed configuration statement     | Simplify the statement or move it outside the builder lambda |
| DN1002 | Error    | Duplicate Collection\<T\> type         | Remove the duplicate `Collection<T>()` call                  |
```

**Step 2: Add detailed sections for DN1001 and DN1002**

After the DN0004 section, add:

```markdown
## DN1001 — Unparsed configuration statement

**Severity:** Error

The source generator encountered a statement inside a builder lambda (`UseNaming`, `UseJsonContract`, `ForType`, etc.) that it could not parse. Only simple property assignments, method calls, and local variable declarations are supported inside builder lambdas.

**Common causes:**
- `if` statements or other control flow inside builder lambdas
- Calling non-builder methods (e.g., `Console.WriteLine`)
- Complex expressions that aren't simple assignments or method calls

**Resolution:** Move non-configuration logic outside the builder lambda, or simplify the statement.

## DN1002 — Duplicate Collection\<T\> type

**Severity:** Error

`Collection<T>()` was called more than once for the same type `T` within a single `UseJsonContract` block.

**Resolution:** Remove the duplicate call. Only one JSON name can be assigned per type.
```

**Step 3: Commit**

```
docs: add DN1001 and DN1002 to Diagnostics Reference
```

---

### Task 8: Update `docs/index.md` and `docs/api/index.md`

**Files:**
- Modify: `docs/index.md`
- Modify: `docs/api/index.md`

**Step 1: Update the landing page (`docs/index.md`)**

- Update tagline to match README
- Replace Team/Person/Address example with a compact transport example
- Update JSON output to show `result` property and `Dto`-suffixed collection names
- Update doc links to include new articles
- Add a brief mention of gzip benefits

The "What It Does" section becomes a compact before/after using transport types:

Before: nested routes with repeated carriers and places
After: flat container with `result`, typed `Dto` arrays, integer refs

Add link for "Why Gzip Isn't Enough" in the Get Started section.

**Step 2: Update API index (`docs/api/index.md`)**

- Line 9: Update `Normalized{RootType}Result` to `{RootType}ResultDto`
- Update any other stale generated type name references

**Step 3: Commit**

```
docs: update landing page and API index with transport example and new naming
```

---

### Task 9: Update `docs/articles/toc.yml`

**Files:**
- Modify: `docs/articles/toc.yml`

**Step 1: Add new articles to TOC**

```yaml
- name: Getting Started
  href: getting-started.md
- name: Configuration Guide
  href: configuration.md
- name: Naming & JSON Contracts
  href: naming-and-contracts.md
- name: Why Gzip Isn't Enough
  href: why-gzip-isnt-enough.md
- name: Diagnostics Reference
  href: diagnostics.md
```

**Step 2: Commit**

```
docs: add naming/contracts and gzip articles to TOC
```

---

### Task 10: Update README diagnostics table

**Files:**
- Modify: `README.md`

**Step 1: Verify README diagnostics**

The README should have an updated diagnostics table that includes DN1001 and DN1002, or links to the docs site for the full table. Since the design says "short README", just link to the diagnostics page instead of duplicating the full table.

If the README still has a diagnostics table, either:
- Add DN1001 and DN1002 to it, OR
- Replace it with a link: "See [Diagnostics Reference](https://dibstern.github.io/DataNormalizer/articles/diagnostics.html) for compiler diagnostics DN0001–DN1002."

**Step 2: Commit (if changes needed)**

```
docs: update README diagnostics reference
```

---

### Task 11: Build docs site and verify

**Step 1: Build the DocFX site**

```bash
dotnet tool restore
dotnet docfx docs/docfx.json
```

Verify no build errors.

**Step 2: Spot-check the built site**

Check that:
- `_site/index.html` renders correctly
- `_site/articles/getting-started.html` exists and has updated content
- `_site/articles/configuration.html` exists and has new sections
- `_site/articles/naming-and-contracts.html` exists (new article)
- `_site/articles/why-gzip-isnt-enough.html` exists (new article)
- `_site/articles/diagnostics.html` has DN1001 and DN1002
- Internal links resolve (no broken hrefs)

**Step 3: Commit any fixes**

```
docs: fix any DocFX build issues
```

---

### Task 12: Final verification

**Step 1: Run all tests**

```bash
dotnet test DataNormalizer.sln --verbosity quiet
```

All tests should still pass (this is a docs-only change, no code modified).

**Step 2: Run the benchmark script one more time**

```bash
dotnet run --project tools/GzipBenchmark
```

Verify the numbers in the README and articles match.

**Step 3: Review all changed files**

```bash
git diff HEAD~N --stat
```

Review the full set of changes for consistency.

**Step 4: Final commit if needed**

```
docs: final review and consistency fixes
```
