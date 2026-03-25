# README & Docs Site Overhaul Design

**Date:** 2026-03-25

**Goal:** Rewrite the README with a problem-first pitch, transport route example, gzip benchmark numbers, and updated container shape (`Result` property). Update all DocFX articles to reflect naming policy and JSON contract features. Add new articles for gzip analysis and naming/contract configuration.

**Context:** Phases 1 (naming policy) and 2 (JSON contract customization) are complete. The README and docs site still show the old container shape (`TeamList[0]`), use the Team/Person/Address example exclusively, and don't mention gzip, naming policy, or JSON contract configuration.

---

## Design Principles

1. **README sells, docs explain** -- README is short and persuasive. DocFX articles provide depth.
2. **Problem-first** -- Lead with the pain (repeated objects, hand-written mapping code, wasted bytes).
3. **Real numbers** -- Run actual gzip benchmarks against the transport search payload; don't estimate.
4. **Transport example** -- Use routes/segments/hops/carriers to demonstrate why normalization matters for graph-like data.
5. **Accurate container shape** -- All examples show `result.Result` (not `list[0]`), naming policy defaults, and JSON contract configuration.

---

## Part 1: Benchmark Script

A standalone C# console app at `tools/GzipBenchmark/` that:

1. Loads `docs/plans/search-response.json`
2. Minifies it (remove whitespace)
3. Measures raw and gzipped sizes (this is the normalized version)
4. Programmatically unnormalizes it:
   - Walk each entity that uses index references
   - Replace each integer index with the full referenced object
   - Recurse: routes → segments → options → hops → line/carrier/vehicle/images
   - Result is a fully inlined tree
5. Minifies the unnormalized version
6. Measures raw and gzipped sizes
7. Prints a comparison table:

```
| Format              | Raw (KB)  | Gzipped (KB) | Ratio |
|---------------------|-----------|--------------|-------|
| Normalized (as-is)  | XXX       | XX           |       |
| Unnormalized        | XXX       | XX           | X.Xx  |
```

The script uses `System.Text.Json` for parsing and `System.IO.Compression.GZipStream` for compression. No external dependencies.

Output numbers feed into README and the "Why Gzip Isn't Enough" article.

---

## Part 2: README Rewrite

### New structure

```
# DataNormalizer

Compile-time graph normalization for .NET.
Generate flat, deduplicated API contracts from nested object graphs.

[badges] | [Documentation] | [API Reference]

## The Problem

APIs often return the same entity many times inside nested trees:
the same place, carrier, image, user, address, product, or tag.
That duplicates bytes on the wire, duplicates parsing work on the client,
and leads to hand-written flatten/rehydrate mapping code.

## What DataNormalizer Does

DataNormalizer generates normalization and denormalization code at compile time.
Shared objects are stored once. References become compact integer indices.
The result container serializes directly with System.Text.Json.

[Transport route example: before (nested, repeated carriers/places)
 vs after (flat arrays with integer refs)]

## Normalization + Gzip

| Format         | Raw     | Gzipped  |
|--------------- |---------|----------|
| Unnormalized   | XXX KB  | XXX KB   |
| Normalized     | XXX KB  | XXX KB   |

gzip compresses repeated bytes; normalization removes repeated structure.
They stack: normalize first, gzip second.

[Link: Full analysis →]

## Quick Start

### Installation
dotnet add package DataNormalizer

### 1. Define types
[transport-style types: SearchResponse, SearchRoute, SearchHop, SearchCarrier, SearchPlace]

### 2. Create config
[NormalizeConfiguration] config with NormalizeGraph<SearchResponse>()

### 3. Normalize and use
var result = Config.Normalize(response);
result.Result  // the root
result.SearchRouteDtos  // flat arrays
var json = JsonSerializer.Serialize(result);
var restored = Config.Denormalize(result);

## When To Use It

Good for:
- Graph-like API responses with shared entities (transport, e-commerce, social)
- High-volume APIs where payload size matters
- Replacing hand-written normalization code

Not ideal for:
- Tiny payloads with little entity reuse
- Simple list responses with no shared references
- Legacy wire formats that need exact schema matching (see JSON contract customization)

## Documentation

- Getting Started
- Configuration Guide
- Naming & JSON Contracts
- Why Gzip Isn't Enough
- Diagnostics Reference
- API Reference

## Target Frameworks

Runtime: net6.0-net10.0
Generator: netstandard2.0

## License

MIT
```

### Key changes from current README
- Problem-first opening instead of mechanism-first
- Transport route example instead of Team/Person/Address
- Gzip benchmark table with real numbers
- `result.Result` instead of `result.TeamList[0]`
- Much shorter -- configuration details moved to docs site
- "When to use it / when not" section for trust-building

---

## Part 3: DocFX Site Updates

### 3a. Update `docs/index.md` (landing page)

- Update tagline to match README
- Replace Team/Person/Address example with transport example
- Update JSON output to show `Result` property
- Add links to new articles

### 3b. Update `docs/articles/getting-started.md`

- Keep Team/Person/Address as the tutorial example (simpler for getting started)
- Update container access from `result.TeamList[0]` to `result.Result`
- Update generated type descriptions to mention `Dto` suffix (naming defaults)
- Add a "What's next" callout linking to naming/contract configuration
- Mention that `Result` is always the entry point for the root entity

### 3c. Update `docs/articles/configuration.md`

- Update Container Result API section to show `result.Result`
- Add section: **Naming Policy** (`UseNaming()`)
  - `DtoSuffix`, `DtoPrefix`, `ContainerSuffix`
  - `EmitJsonPropertyNames`
  - Global vs per-graph scoping
- Add section: **JSON Contract Customization** (`UseJsonContract()`)
  - `RootPropertyName`
  - `Collection<T>("jsonName")`
  - Full example matching the transport search use case
- Add section: **Reference JSON Names** (`Reference().JsonName()`, `[NormalizeJsonName]`)
  - Per-property JSON name overrides
  - Priority order: config > attribute > default
- Update diagnostics references to include DN1001 and DN1002

### 3d. Create `docs/articles/naming-and-contracts.md` (NEW)

Dedicated article covering:
- **The design philosophy**: semantic CLR names + configurable JSON names
- **NamingBuilder** full API reference with examples
- **JsonContractBuilder** full API reference with examples
- **ReferenceBuilder** full API reference with examples
- **[NormalizeJsonName] attribute** usage and priority rules
- **Full transport search example**: complete config + generated container + generated DTOs
- **Migration guide**: how to match an existing wire format

### 3e. Create `docs/articles/why-gzip-isnt-enough.md` (NEW)

Full analysis article:
- **The intuition** -- "gzip will compress the repeats away, right?"
- **The reality** -- three reasons it doesn't fully work:
  1. Repeated objects aren't byte-identical in context (different surrounding JSON)
  2. Inlining duplicates large subtrees, not just leaf strings
  3. Integer indexes are extremely cheap (2 bytes vs hundreds)
- **The benchmark** -- table with real numbers from the transport search payload
  - Raw sizes: normalized vs unnormalized
  - Gzipped sizes: normalized vs unnormalized
  - Pretty-printed sizes for comparison
- **What normalization gives beyond compression**
  - Explicit shared references
  - Simpler client state (one canonical copy per entity)
  - Easier caching, diffing, patching
- **The conclusion**: "Normalize first, gzip second. They stack."

### 3f. Update `docs/articles/toc.yml`

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

### 3g. Update `docs/articles/diagnostics.md`

Add DN1001 (unparsed config statement) and DN1002 (duplicate Collection<T>) to the diagnostics table.

---

## Implementation Order

1. **Benchmark script** -- get real numbers first (everything else depends on them)
2. **README rewrite** -- uses benchmark numbers
3. **New DocFX articles** -- naming-and-contracts.md, why-gzip-isnt-enough.md
4. **Update existing DocFX articles** -- getting-started.md, configuration.md, index.md, diagnostics.md, toc.yml
5. **Build docs site** -- verify DocFX builds cleanly
6. **Final review** -- check all links, examples compile mentally, numbers are cited correctly

---

## Out of Scope

- Generating a new NuGet package version
- Changing any library source code
- Adding automated benchmarks to CI
- Rewriting the samples project (would be a nice follow-up)
