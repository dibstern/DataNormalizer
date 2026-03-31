# Why Gzip Isn't Enough

> "If my API responses are gzipped, do I still need normalization?"

Yes. Gzip compresses repeated byte sequences; normalization removes repeated *structure*. They solve different problems, and they stack.

## The benchmark

Measured on a real transport search API response (Bicester to Sainte-Marie-aux-Mines route search) containing routes, segments, hops, shared carriers, and shared places.

### Minified JSON

| Format       | Raw      | Gzipped  | Ratio vs Normalized |
|--------------|----------|----------|---------------------|
| Normalized   | 345 KB   | 58 KB    | 1.0x                |
| Unnormalized | 713 KB   | 121 KB   | 2.1x                |

Raw savings: **368 KB** (52%).
Gzipped savings: **63 KB** (52%).

### Pretty-printed JSON

| Format       | Raw       | Gzipped  | Ratio vs Normalized |
|--------------|-----------|----------|---------------------|
| Normalized   | 538 KB    | 65 KB    | 1.0x                |
| Unnormalized | 1,482 KB  | 150 KB   | 2.3x                |

Gzip closes the gap but does not eliminate it. Even after compression, the normalized payload is **2.1x smaller**.

## Why gzip doesn't fully cancel normalization

Three structural reasons prevent gzip from recovering all the redundancy that normalization removes:

### 1. Repeated objects are not byte-identical in context

Gzip works on a sliding window of bytes. Two copies of the same carrier object appear in different positions surrounded by different JSON keys, array indices, and sibling fields. The compressor sees *similar* byte sequences, not identical ones, so it cannot always collapse them perfectly.

### 2. Inlining duplicates entire subtrees, not just leaf strings

When the same carrier appears in 30 segments, the unnormalized form repeats not just the carrier name but its entire subtree: transit images, line references, places, path coordinates. Each level of nesting multiplies the redundancy in ways that exceed gzip's window size for large payloads.

### 3. Integer indexes are extremely cheap

In a normalized payload, `"carrier": 2` is a few bytes. The full carrier object it replaces can be hundreds of bytes. Even the best compressor cannot shrink a 200-byte object down to the 1-2 bytes an integer index occupies.

## What normalization gives beyond compression

Smaller wire size is only part of the story. Normalization also provides:

- **Explicit shared references.** Each entity has one canonical copy. There is no ambiguity about which instance is authoritative.
- **Simpler client state management.** No duplicate objects to reconcile. Update a carrier once and every segment that references it sees the change.
- **Easier caching, diffing, and patching.** Flat entity tables are trivial to merge, diff, or patch incrementally.
- **Reduced parse and allocation work.** Fewer bytes means fewer objects to deserialize and less memory pressure on the client.

These benefits apply regardless of transport compression.

## The bottom line

Normalize first, gzip second. They stack.

Normalization removes structural duplication. Gzip removes byte-level redundancy in what remains. Together they produce the smallest, most efficient payload.

## Getting started

Ready to normalize your API responses?

- [Getting Started](getting-started.md) -- install DataNormalizer and normalize your first object graph.
- [Configuration Guide](configuration.md) -- control which types are normalized and how.
