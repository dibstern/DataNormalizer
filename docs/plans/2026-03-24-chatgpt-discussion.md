# ChatGPT discussion on normalisation and gzip

## Prompt
Analyse this normalised response json in @docs/plans/search-response.json
Unpack what the data structure would look like and how much data it would use if it were unnormalised, and then consider whether gzipping the unnormalised version versus gzipping the normalised version will make a difference in the size of data transferred

## Response

Thought for 1m 51s

I parsed the JSON you attached and treated it as a classic lookup-table + index-reference normalized payload.

The important part is that this payload is not just “a big tree”. It is really two layers:

Entity tables / dictionaries by array index

transitImages

vehicles

carriers

places

paths

lines

transitGuides

hops

options

segments

routes

Objects that reference those tables by integer index

hops[*].line → index into lines

hops[*].marketingCarrier → index into carriers

hops[*].vehicle → index into vehicles

hops[*].transitImages[] → indexes into transitImages

lines[*].places[] → indexes into places

lines[*].path → index into paths

options[*].hops[] → indexes into hops

segments[*].options[] → indexes into options

segments[*].transitGuides[] → indexes into transitGuides

routes[*].segments[] → indexes into segments

routes[*].transitGuides[] → indexes into transitGuides

routes[*].places[] → indexes into places

result.originPlace / destinationPlace / routes[] / transitGuides[]

places[*].nearbyCity → index into places

So the payload is effectively a graph flattened into arrays, with cross-references by number.

What the unnormalised version would look like

A fully unnormalised version would inline the referenced objects instead of using indexes.

For example, a hop like this:

{
  "line": 0,
  "marketingCarrier": 0,
  "vehicle": 0,
  "duration": 1740,
  "frequency": 770,
  "operatingDays": 127,
  "transitImages": [0, 1, 2]
}

would become conceptually like:

{
  "line": {
    "places": [
      { "shortName": "Skybus Coach Terminal", ... },
      { "shortName": "Melbourne Airport T3 Skybus/Arrival Dr", ... }
    ],
    "names": [...],
    "distance": ...,
    "path": "fr|dFswasZyFn@",
    "colors": [...]
  },
  "marketingCarrier": {
    "name": "SkyBus",
    "code": "_SKYBUS",
    "iconUrl": "/logos/Trains/96/_SKYBUS_202507160223.png",
    "url": "https://www.skybus.com.au",
    "phone": "1300 759 287",
    "email": "info@skybus.com.au",
    "twitter": "@skybus_aus",
    "transitImages": [
      { "title": "Skybus Super Shuttle", ... },
      { "title": "Bus Interior", ... },
      { "title": "Bus Exterior", ... }
    ]
  },
  "vehicle": {
    "name": "bus",
    "kind": "bus"
  },
  "duration": 1740,
  "frequency": 770,
  "operatingDays": 127,
  "transitImages": [
    { "title": "Skybus Super Shuttle", ... },
    { "title": "Bus Interior", ... },
    { "title": "Bus Exterior", ... }
  ]
}

And then each option would inline hops, each segment would inline options, each route would inline segments, and result would inline routes and places.

That means repeated structures would get copied again and again:

the same carrier object into many hops

the same line into many hops

the same path string into many lines-as-inlined-into-hops

the same place objects into many lines/routes/results

the same transit guide objects into many segments/routes/result

Actual size in this payload

I measured the uploaded JSON directly.

Using a minified JSON representation:

Normalised JSON: about 120,101 bytes

Fully unnormalised JSON: about 971,097 bytes

So in this payload, unnormalising makes it about 8.1× larger before compression.

If you look at the pretty-printed text as pasted:

Normalised pretty JSON: about 248,600 bytes

Fully unnormalised pretty JSON: about 2,711,114 bytes

Again, roughly an order-of-magnitude blow-up.

Why it blows up so much

This particular payload has heavy reuse in the transport graph:

110 hops

124 options

16 segments

6 routes

only 17 distinct lines

only 32 carriers

only 12 vehicles

only 25 transit images

only 3 transit guides

A few references are especially reused:

one line is referenced by 88 hops

one carrier is referenced by 36 hops

another carrier also by 36 hops

one vehicle is referenced by 57 hops

That is exactly the kind of structure normalization helps: lots of repeated metadata attached to many graph edges.

What happens once you gzip it

This is the interesting part.

For the minified JSON:

Normalised + gzip: about 22,283 bytes

Unnormalised + gzip: about 116,169 bytes

So even after gzip, the unnormalised version is still about 5.2× larger.

For the pretty-printed JSON:

Normalised pretty + gzip: about 24,420 bytes

Unnormalised pretty + gzip: about 166,986 bytes

So gzip absolutely helps both, but it does not erase the advantage of normalization here.

Why gzip does not fully cancel the difference

A natural intuition is: “if the unnormalised version repeats the same objects over and over, won’t gzip compress those repeats away?”

Yes, partly. But not completely.

There are three reasons:

Repeated objects are not always byte-identical in context

same carrier may appear nested inside different hops, options, segments, routes

surrounding punctuation and neighboring fields differ

so gzip cannot reuse them as perfectly as a single shared reference can

Inlining duplicates large subtrees, not just leaf strings

line → places → path

carrier → transitImages

route → segments → options → hops

repeated nested structure still costs a lot even when compressible

Indexes are extremely cheap

"marketingCarrier": 2 is tiny

replacing that with a whole carrier object is much larger, even after compression

So the answer is:

Without gzip: normalization makes a huge difference

With gzip: normalization still makes a substantial difference

But gzip narrows the gap somewhat

raw gap here: about 8.1×

gzipped gap here: about 5.2×

Bottom line

For this response shape, normalization is still clearly worthwhile even if the response is gzipped.

In this concrete payload:

raw minified savings from normalization: about 851 KB

gzipped savings from normalization: about 94 KB

That is a very meaningful reduction in transferred bytes, especially on mobile, at high request volume, or when these responses are cached and served frequently.

The short conclusion is: gzip does not make normalization irrelevant here; normalization still materially reduces transfer size.

If you want, I can also break down which top-level tables contribute most to the savings, or sketch a denormalised TypeScript type for what the expanded shape would look like.

I wrote a library to do this normalisation automatically, does it actually support the normalisation that's currently in place?

https://dibstern.github.io/DataNormalizer/

And how should this library sell the need for itself in its readme and documentation, and why gzip isn't enough?

I’m going to inspect the library’s docs and compare its model against the normalized payload shape you shared, then I’ll turn that into concrete README positioning.

I’ve got the docs and the payload shape. The key distinction is this: your library appears able to express the same kind of normalization, but not the same wire format without extra customization.

Thought for 2m 29s

Yes, but only at the pattern level, not as a drop-in match for your current wire format.

Your current payload is clearly a flattened graph with typed top-level tables and integer references between them. For example, lines[*] points at places and path, hops point at line, marketingCarrier, vehicle, and transitImages, routes point at segments, transitGuides, and places, and result points at originPlace, destinationPlace, routes, and transitGuides.

That is very close to what DataNormalizer says it generates: a flat container DTO with one typed collection per discovered entity type, where nested object references become integer Index properties and collections become Indices arrays. It also supports auto-discovery from a root graph and selective inlining of types that should stay nested.

So the honest answer is:

Semantically: yes, mostly.

Schema-compatibly: no, not from the docs as written.

What it supports already

It appears to support the core mechanics your current normalization relies on:

flattening a nested object graph into top-level typed collections,

replacing references with integer indexes,

keeping selected types inline instead of extracting them, which is important because your payload only normalizes some parts and leaves lots of subobjects nested, like hotelInfo, scheduleInfo, bookingInfo, links, indicativePrices, and various debug/config blobs.

denormalizing back into the object graph.

So if you modeled the whole response as a root type and marked the non-shared detail types as inline, you could reproduce the shape of the idea.

Where it does not match the current normalization

The main mismatches are these.

First, the library’s documented naming convention is generated and opinionated: HomeIndex, MembersIndices, PersonList, AddressList, Normalized{RootType}Result. Your live payload instead keeps domain names like line, segments, places, routes, transitGuides, and top-level arrays named exactly after the entity type.

Second, the docs describe a root container where the root object is stored in its own typed array, with the root at index 0. Your payload has a custom top-level envelope and then a single result object, not ResultList[0].

Third, the docs say deduplication is value-based, via generated IEquatable<T> and “value-equality-based deduplication.” That is not necessarily the same thing as “preserve distinct nodes unless they are literally the same shared instance” or “use my domain identity rules.” If your existing normalizer dedupes by canonical identity rather than plain value equality, this is a real semantic difference.

So I would phrase it this way:

DataNormalizer supports the same normalization strategy as the current payload, but not the current payload’s exact contract unless you add an adapter layer or extend the library with contract-shaping features.

What would make it fully support the current scheme

If your goal is “can this replace the hand-written normalization already in production?”, the missing features are probably:

configurable reference naming, so line can remain line instead of LineIndex

configurable collection naming, so lines can remain lines instead of LineList

configurable root envelope shape, so you can emit result instead of ResultList[0]

configurable dedupe policy, so users can choose value equality, reference identity, or custom key selectors

maybe per-property control, not just per-type inline control, if the same type sometimes needs to be normalized and sometimes kept inline

Those are the features that would turn it from “good generic normalizer” into “can emit our existing wire format”.

How I would sell it in the README

Right now the docs sell the mechanism. They should sell the pain it removes.

The strongest pitch is not “normalization exists.” It is:

Stop hand-writing brittle flatten/rehydrate code for large response graphs.
DataNormalizer generates strongly typed normalization and denormalization code at compile time, so shared objects are emitted once, references become compact integer links, and your API contracts stay fast and predictable.

That is the real value proposition:

less handwritten mapping code

fewer bugs in dedupe/reference handling

smaller payloads

a schema that is explicit about shared entities

compile-time generation instead of reflection-heavy runtime magic

reversible denormalization

A better README structure would be:

1. Open with the problem

“APIs often return the same entity again and again inside nested trees: the same place, carrier, image, user, address, product, policy, or tag. That duplicates bytes, duplicates client state, and leads to hand-written mapping code.”

2. Then the promise

“DataNormalizer generates flat, deduplicated DTOs with integer references automatically.”

3. Then a real-world example

Not just Team -> Person -> Address. Show something graph-like:

routes → segments → options → hops

hops → carrier / vehicle / line

line → places / path

That looks much closer to the kind of payload where normalization obviously matters. Your current transport example is actually a much better sales demo than the people-and-addresses example.

4. Then explain why compile-time generation matters

“Strongly typed result container, no string-key lookups, no reflection serializer tricks, direct System.Text.Json serialization, generated denormalizer.”

5. Then explain when not to use it

This actually increases trust:

don’t use for tiny payloads

don’t use if the graph has little reuse

don’t use if you need a very specific legacy wire contract that the generator cannot shape yet

How to explain why gzip is not enough

This needs to be in the README because lots of people will think it.

The core point is:

gzip compresses repeated text; normalization removes repeated structure.

Those are not the same thing.

A clean explanation is:

gzip only helps on the wire
It reduces transfer bytes, but it does not give you explicit shared references, does not simplify client state, and does not eliminate repeated parse/allocation work if the consumer processes the denormalized tree.

Repeated objects are not free even when compressed
In an unnormalized payload, the same entity is repeated inside slightly different surrounding JSON, so gzip can only compress it approximately. A single integer index is still much cheaper than repeating an entire subtree.

Normalization changes the data model, not just the byte stream
It gives you one canonical copy of each shared entity and explicit links between them. That is useful for caching, diffing, patching, merging, memoization, and client-side stores. gzip gives you none of that.

gzip and normalization stack
This is the key selling line. It is not “normalization instead of gzip.” It is “normalize first, gzip second.”

That last line should probably appear verbatim in the docs.

README copy I would actually use
Hero

Compile-time graph normalization for .NET
Generate flat, deduplicated DTOs from nested object graphs, with typed collections, integer references, and generated denormalization.

Problem section

Large API responses often repeat the same nested objects many times: users, addresses, places, carriers, images, tags, policies. gzip helps with transfer size, but it does not remove duplicated structure from the payload model itself. You still ship repeated subtrees, parse repeated JSON, and hand-maintain flatten/rehydrate code.

Value section

DataNormalizer generates that code for you at compile time:

shared objects emitted once

references replaced with compact integer links

selected types kept inline where normalization would be overkill

denormalization generated too

result container stays strongly typed and serializes directly with System.Text.Json

gzip section

Why not just gzip?
Because gzip compresses repeated bytes, not repeated meaning. A normalized payload still gzips well, but it also gives you a better wire contract: one canonical copy of each shared entity, explicit references, smaller repeated subtrees, and a shape that is easier to cache, diff, and consume.

My blunt recommendation

If you want this library to sell itself better, shift the positioning from:

“source generator that normalizes nested object graphs”

to:

“automatic generation of compact, deduplicated API contracts for repeated object graphs”

That framing is much closer to why someone would adopt it.

And if you want it to support your current production-style normalization more convincingly, I would prioritize:

custom naming policies,

custom root-envelope shaping,

custom dedupe comparers.

Those three would move it from “generic utility” to “plausible replacement for bespoke API normalizers.”

If you want, I can draft a rewritten README opening and a “Why gzip isn’t enough” section in your project’s tone.

Looks awesome; how would I adjust DataNormalization to very easily allow the use of existing naming instead of generating custom names? Maybe the generated classes could use the suffix DTO and could have automatically written json conversion naming? Or is there a better design?

Yes — but I would not make JSON converters the primary design.

The cleaner design is to separate three concerns:

normalization semantics

generated CLR type shape

serialized JSON contract

Those are related, but they should not be fused unless you want the generator to become hard to reason about.

Best design: let users opt into a contract naming policy

The simplest path is:

keep the generated internal model predictable

add a contract/naming policy layer

emit JSON property names matching an existing schema

So instead of changing the actual property names from LineIndex to line, you can generate:

public sealed class HopDto
{
    [JsonPropertyName("line")]
    public required int LineIndex { get; init; }

    [JsonPropertyName("marketingCarrier")]
    public required int MarketingCarrierIndex { get; init; }

    [JsonPropertyName("vehicle")]
    public required int VehicleIndex { get; init; }

    [JsonPropertyName("transitImages")]
    public required int[] TransitImageIndices { get; init; }
}

That gives you a few big advantages:

generated code remains self-describing in C#

JSON can match an existing wire format exactly

you avoid weird CLR property names like line or transitGuides

you do not need custom converters for ordinary cases

System.Text.Json stays fast and boring

That is usually the right tradeoff.

Why converters are probably the wrong default

A custom converter is powerful, but it is the wrong first tool here because it pushes ordinary schema shaping into handwritten serialization logic.

That causes problems:

harder to debug

harder to inspect generated DTOs

harder for users to understand what the contract is

harder to compose with normal System.Text.Json

harder to keep AOT/source-gen friendly

more edge cases around nullability, collections, enums, etc.

In other words: if all you need is “serialize this property as line instead of LineIndex”, a converter is overkill.

Use converters only if the wire format is doing something fundamentally non-idiomatic, like:

dynamic property names

mixed scalar/object representations

polymorphic special cases

weird tuple-like arrays instead of objects

Your normalization format does not sound like that. It sounds like normal JSON objects with conventional fields. That is ideal for attributes or generated metadata, not converters.

The best ergonomic model

I would support three naming modes.

1. Default generated naming

This is what you have now.

Example:

LineIndex

TransitImageIndices

LineList

NormalizedTripResult

Good for greenfield usage.

2. Existing-contract compatibility mode

This is the important one for your use case.

Example JSON:

line

marketingCarrier

vehicle

transitImages

top-level lines, places, routes, result

But the CLR names can stay idiomatic:

LineIndex

MarketingCarrierIndex

VehicleIndex

TransitImageIndices

Lines

Places

Routes

Result

Just decorate with [JsonPropertyName(...)].

3. Full custom naming hooks

For advanced users:

custom singular/plural name for entity collections

custom reference property names

custom root envelope names

custom suffix/prefix rules

That gives flexibility without making the base API messy.

Concrete recommendation

I would add generator configuration like this:

[Normalize(
    JsonContract = JsonContractMode.Compatible,
    DtoSuffix = "Dto",
    UseJsonPropertyNames = true
)]
public partial class SearchResult
{
}

And then per-type / per-property overrides:

[NormalizedName("lines")]
public sealed partial class Line
{
}

[ReferenceName("line")]
public Line? Line { get; init; }

[ReferenceName("transitImages")]
public IReadOnlyList<TransitImage> TransitImages { get; init; } = [];

Or possibly:

[NormalizedCollectionName("lines")]
[NormalizedItemName("line")]
public sealed partial class Line
{
}

That would let the generator infer:

collection JSON name: lines

reference JSON property: line

For arrays of refs, still use transitImages, not TransitImageIndices, at the JSON layer.

How I would shape the generator output

I would keep the generated CLR names explicit and stable.

For example:

public sealed class HopDto
{
    [JsonPropertyName("line")]
    public int LineIndex { get; init; }

    [JsonPropertyName("marketingCarrier")]
    public int MarketingCarrierIndex { get; init; }

    [JsonPropertyName("vehicle")]
    public int VehicleIndex { get; init; }

    [JsonPropertyName("transitImages")]
    public int[] TransitImageIndices { get; init; } = [];
}

And top-level:

public sealed class NormalizedSearchResultDto
{
    [JsonPropertyName("lines")]
    public List<LineDto> Lines { get; init; } = [];

    [JsonPropertyName("places")]
    public List<PlaceDto> Places { get; init; } = [];

    [JsonPropertyName("routes")]
    public List<RouteDto> Routes { get; init; } = [];

    [JsonPropertyName("result")]
    public SearchResultDto Result { get; init; } = default!;
}

This is much better than renaming the CLR property itself to line, because the CLR side still communicates meaning: it is an index reference, not an embedded LineDto.

Should the generated classes use Dto suffix?

Yes, probably.

That is a good idea for two reasons:

it distinguishes generated transport types from domain types

it makes compatibility mode cleaner

For example:

domain: Line, Hop, Route

generated: LineDto, HopDto, RouteDto

normalized container: NormalizedSearchResponseDto

That keeps the generator from “stealing” nice names and makes debugging easier.

I would do that before I would do aggressive renaming tricks.

One subtle but important point

You do not actually want “existing naming” everywhere.

There are really two kinds of names:

entity names

line, route, place, carrier

reference semantics

this property is not a LineDto, it is an int index pointing into lines

If you collapse those too much, the generated CLR model becomes misleading.

For example, this is bad:

public int line { get; init; }

because it looks sloppy in C#, and it hides the meaning.

This is much better:

[JsonPropertyName("line")]
public int LineIndex { get; init; }

It tells both truths:

JSON compatibility name = line

CLR semantic name = LineIndex

That is the design I would choose.

Better design than just “automatic JSON naming”

So the overall answer is:

Yes to Dto suffixes

Yes to generated [JsonPropertyName] attributes

No to custom converters as the default mechanism

No to forcing CLR property names to mimic legacy JSON names

Yes to a configurable naming policy system

The API I’d aim for

Something like this:

[Normalize(
    DtoSuffix = "Dto",
    ContainerSuffix = "Dto",
    JsonMode = JsonMode.ExplicitPropertyNames,
    ReferenceNaming = ReferenceNamingMode.Compatible,
    CollectionNaming = CollectionNamingMode.Compatible
)]
public partial class SearchResponse
{
}

Then optional overrides:

[NormalizedCollectionName("transitGuides")]
public sealed partial class TransitGuide
{
}

public sealed partial class Hop
{
    [ReferenceName("line")]
    public Line Line { get; init; } = default!;

    [ReferenceName("marketingCarrier")]
    public Carrier MarketingCarrier { get; init; } = default!;

    [ReferenceName("transitImages")]
    public IReadOnlyList<TransitImage> TransitImages { get; init; } = [];
}

And the generator decides:

CLR property name: LineIndex

JSON property name: line

That is the sweet spot.

If you want the simplest possible implementation

The minimum viable version is:

add DtoSuffix

add a naming policy that computes JSON property names

emit [JsonPropertyName] on generated DTO properties and container properties

keep CLR names unchanged and idiomatic

That gets you 80–90% of the value with little complexity.

My blunt take

The best design is:

Preserve semantic CLR names, emit legacy-compatible JSON names.

That is much better than trying to make the CLR types themselves look exactly like the wire contract.

Because the generated C# is for developers.
The JSON contract is for interoperability.
They do not need to be identical.

If you want, I can sketch the exact attribute model and source-generator output I’d recommend for DataNormalizer, with complete example generated code.

That looks great -> how about setting this configuration via the config class in DataNormalization?

Yes — that is probably the best place for it.

A config-class approach is cleaner than pushing lots of attributes onto domain types, because this is really a normalizer output policy, not core domain meaning. Attributes are still useful for targeted overrides, but the main defaults should live in configuration.

Why config is the right primary design

The naming choices you are talking about are mostly global policy:

DTO suffix or not

root/container naming

whether JSON names should mimic an existing contract

reference property naming

collection naming

whether CLR names stay semantic like LineIndex

Those are cross-cutting concerns. If you express them with attributes everywhere, users end up scattering transport concerns through their models. A config object keeps that centralized and easier to reason about.

So I would make the design:

Config class = primary defaults

Attributes = optional per-type / per-property override

JSON converters = last resort only

The shape I’d recommend

I would add a NamingOptions section to the existing config class, or if you want to keep it flatter, add these directly to NormalizerConfig.

Something like this:

public sealed class NormalizerConfig
{
    public string DtoSuffix { get; init; } = "Dto";
    public string ContainerSuffix { get; init; } = "Dto";

    public JsonContractMode JsonContractMode { get; init; } = JsonContractMode.Generated;

    public ClrNamingMode ClrNamingMode { get; init; } = ClrNamingMode.Semantic;

    public ReferencePropertyNamingMode ReferencePropertyNamingMode { get; init; }
        = ReferencePropertyNamingMode.IndexSuffix;

    public CollectionPropertyNamingMode CollectionPropertyNamingMode { get; init; }
        = CollectionPropertyNamingMode.PluralizedEntityName;

    public RootObjectNamingMode RootObjectNamingMode { get; init; }
        = RootObjectNamingMode.RootTypeName;

    public bool EmitJsonPropertyNameAttributes { get; init; } = false;

    public bool PreserveExistingContractNames { get; init; } = false
No I mean look at the existing config class and show me how you would adjust it
Thought for 21s

Yes — given the current shape, I’d extend the existing config/builder API, not replace it.

From the docs, the library already has:

a base NormalizationConfig class,

a Configure(NormalizeBuilder builder) entry point,

NormalizeGraph<T>(),

graph-level config like Inline<T>(),

and per-type config via ForType<T>(...).

That means the cleanest design is to add contract naming configuration into that same fluent surface.

What I’d change

I’d add three layers of naming config:

global defaults on NormalizeBuilder

per-graph overrides on GraphBuilder

per-type / per-property overrides where needed

That fits the existing API style much better than introducing converters or forcing attributes everywhere.

How I’d extend the config class

Today, you probably have something conceptually like this:

public sealed partial class AppNormalization : NormalizationConfig
{
    protected override void Configure(NormalizeBuilder builder)
    {
        builder.NormalizeGraph<SearchResponse>(graph =>
        {
            graph.Inline<DebugInfo>();
            graph.Inline<BookingInfo>();
        });
    }
}

I would evolve it to something like this:

public sealed partial class AppNormalization : NormalizationConfig
{
    protected override void Configure(NormalizeBuilder builder)
    {
        builder.UseNaming(naming =>
        {
            naming.DtoSuffix = "Dto";
            naming.ContainerSuffix = "Dto";

            naming.ClrReferenceStyle = ClrReferenceStyle.IndexSuffix;
            naming.ClrCollectionStyle = ClrCollectionStyle.Pluralized;

            naming.JsonContractStyle = JsonContractStyle.Compatible;
            naming.EmitJsonPropertyNames = true;
        });

        builder.NormalizeGraph<SearchResponse>(graph =>
        {
            graph.UseJsonContract(contract =>
            {
                contract.RootPropertyName = "result";
                contract.Collection<SearchRoute>("routes");
                contract.Collection<SearchSegment>("segments");
                contract.Collection<SearchOption>("options");
                contract.Collection<SearchHop>("hops");
                contract.Collection<SearchLine>("lines");
                contract.Collection<SearchPlace>("places");
                contract.Collection<SearchCarrier>("carriers");
                contract.Collection<SearchVehicle>("vehicles");
                contract.Collection<TransitGuide>("transitGuides");
                contract.Collection<TransitImage>("transitImages");
                contract.Collection<Path>("paths");
            });

            graph.Inline<DebugInfo>();
            graph.Inline<BookingInfo>();
            graph.Inline<IndicativePrice>();
        });

        builder.ForType<SearchHop>(hop =>
        {
            hop.Reference(x => x.Line).JsonName("line");
            hop.Reference(x => x.MarketingCarrier).JsonName("marketingCarrier");
            hop.Reference(x => x.Vehicle).JsonName("vehicle");
            hop.ReferenceCollection(x => x.TransitImages).JsonName("transitImages");
        });

        builder.ForType<SearchLine>(line =>
        {
            line.Reference(x => x.Path).JsonName("path");
            line.ReferenceCollection(x => x.Places).JsonName("places");
        });

        builder.ForType<SearchRoute>(route =>
        {
            route.ReferenceCollection(x => x.Segments).JsonName("segments");
            route.ReferenceCollection(x => x.TransitGuides).JsonName("transitGuides");
            route.ReferenceCollection(x => x.Places).JsonName("places");
        });

        builder.ForType<SearchResponse>(root =>
        {
            root.Reference(x => x.OriginPlace).JsonName("originPlace");
            root.Reference(x => x.DestinationPlace).JsonName("destinationPlace");
            root.ReferenceCollection(x => x.Routes).JsonName("routes");
            root.ReferenceCollection(x => x.TransitGuides).JsonName("transitGuides");
        });
    }
}
Why this design fits your existing library

The docs already describe the library as configuration-driven through NormalizationConfig and a fluent builder, with graph-level and type-level controls. So adding naming/contract policy there is the least surprising extension.

It also preserves the distinction between:

what gets normalized

how generated CLR types are named

how the JSON contract is emitted

That distinction matters. Your current transport payload has domain-friendly JSON names like line, routes, places, and result, while the generated CLR side benefits from explicit names like LineIndex and TransitImageIndices.

The minimum API surface I’d add

If you want the smallest useful change set, I’d add just these:

public sealed class NamingBuilder
{
    public string DtoSuffix { get; set; } = "";
    public string ContainerSuffix { get; set; } = "Result";
    public bool EmitJsonPropertyNames { get; set; } = false;
    public JsonContractStyle JsonContractStyle { get; set; } = JsonContractStyle.Generated;
    public ClrReferenceStyle ClrReferenceStyle { get; set; } = ClrReferenceStyle.IndexSuffix;
}
public sealed class JsonContractBuilder
{
    public string? RootPropertyName { get; set; }

    public void Collection<T>(string jsonName);
}
public sealed class TypeBuilder<T>
{
    public void Reference<TProp>(Expression<Func<T, TProp>> property);
    public void ReferenceCollection<TProp>(Expression<Func<T, IEnumerable<TProp>>> property);
}

And then let the reference configuration support:

public sealed class ReferenceBuilder
{
    public void JsonName(string jsonName);
}

That alone would cover most of what you want.

What the generated output would become

For a compatible contract, the generated DTO should still look like this in C#:

public sealed class SearchHopDto
{
    [JsonPropertyName("line")]
    public int LineIndex { get; init; }

    [JsonPropertyName("marketingCarrier")]
    public int? MarketingCarrierIndex { get; init; }

    [JsonPropertyName("vehicle")]
    public int VehicleIndex { get; init; }

    [JsonPropertyName("transitImages")]
    public int[] TransitImageIndices { get; init; } = [];
}

And the container:

public sealed class NormalizedSearchResponseDto
{
    [JsonPropertyName("result")]
    public SearchResponseDto Result { get; init; } = default!;

    [JsonPropertyName("routes")]
    public SearchRouteDto[] Routes { get; init; } = [];

    [JsonPropertyName("segments")]
    public SearchSegmentDto[] Segments { get; init; } = [];

    [JsonPropertyName("options")]
    public SearchOptionDto[] Options { get; init; } = [];

    [JsonPropertyName("hops")]
    public SearchHopDto[] Hops { get; init; } = [];

    [JsonPropertyName("lines")]
    public SearchLineDto[] Lines { get; init; } = [];

    [JsonPropertyName("places")]
    public SearchPlaceDto[] Places { get; init; } = [];
}

That is the sweet spot:

good CLR names for developers

existing JSON names on the wire

no converter complexity

One design tweak I would strongly recommend

Do not make the naming system infer too much from the current generated names like LineIndex / PersonList.

Instead, model references and collections explicitly in the generator metadata.

In other words, the generator should know:

this property is a scalar reference to Line

this property is a collection of references to TransitImage

this collection stores all Route DTOs

this is the root DTO

Then naming becomes a formatting step, not a fragile string-rewrite step.

That will save you pain later.

My preferred final shape

If I were adjusting the existing config class, I’d make it feel like this:

public sealed partial class AppNormalization : NormalizationConfig
{
    protected override void Configure(NormalizeBuilder builder)
    {
        builder.UseNaming(n =>
        {
            n.DtoSuffix = "Dto";
            n.ContainerSuffix = "Dto";
            n.EmitJsonPropertyNames = true;
            n.JsonContractStyle = JsonContractStyle.Compatible;
        });

        builder.NormalizeGraph<SearchResponse>(graph =>
        {
            graph.UseJsonContract(c =>
            {
                c.RootPropertyName = "result";
                c.Collection<SearchRoute>("routes");
                c.Collection<SearchSegment>("segments");
                c.Collection<SearchOption>("options");
                c.Collection<SearchHop>("hops");
                c.Collection<SearchLine>("lines");
                c.Collection<SearchPlace>("places");
                c.Collection<SearchCarrier>("carriers");
                c.Collection<SearchVehicle>("vehicles");
                c.Collection<TransitGuide>("transitGuides");
                c.Collection<TransitImage>("transitImages");
                c.Collection<Path>("paths");
            });

            graph.Inline<DebugInfo>();
            graph.Inline<BookingInfo>();
        });

        builder.ForType<SearchHop>(x =>
        {
            x.Reference(p => p.Line).JsonName("line");
            x.Reference(p => p.MarketingCarrier).JsonName("marketingCarrier");
            x.Reference(p => p.Vehicle).JsonName("vehicle");
            x.ReferenceCollection(p => p.TransitImages).JsonName("transitImages");
        });
    }
}

That feels like a natural extension of the existing NormalizationConfig pattern the docs already expose.

The blunt version is: add contract naming to the builder API, emit [JsonPropertyName], keep CLR names semantic, and avoid converters unless the JSON shape itself is weird.

I can sketch the actual NormalizeBuilder, GraphBuilder, and TypeBuilder API additions as complete code if you want.
