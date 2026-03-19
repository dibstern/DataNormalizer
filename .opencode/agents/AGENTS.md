# DataNormalizer - Agent Context

## Project Overview
DataNormalizer is a .NET source generator NuGet package that normalizes nested
object graphs into flat, deduplicated representations.

## Repository Structure
- `src/DataNormalizer/` - Runtime library (net8.0;net9.0;net10.0)
- `src/DataNormalizer.Generators/` - Roslyn source generator (netstandard2.0)
- `tests/DataNormalizer.Tests/` - Runtime unit tests (NUnit 4)
- `tests/DataNormalizer.Generators.Tests/` - Generator snapshot tests (Verify)
- `tests/DataNormalizer.Integration.Tests/` - End-to-end tests
- `samples/DataNormalizer.Samples/` - Example usage
- `docs/plans/` - Design and implementation plans

## Build & Test Commands
- Build: `dotnet build`
- Test all: `dotnet test`
- Format check: `dotnet tool restore && dotnet csharpier check .`
- Format fix: `dotnet csharpier format .`
- Pack: `dotnet pack src/DataNormalizer/DataNormalizer.csproj -c Release`

## Key Architectural Decisions
1. Pure source generator (no runtime reflection/expression trees)
2. Fluent configuration API parsed syntactically by the generator
3. Zero runtime dependencies
4. Equality-based deduplication (not hash-only) for correctness
5. Two-pass denormalization for circular reference support
6. Central Package Management (Directory.Packages.props)
7. CSharpier for formatting (v1.2.6 subcommand syntax)
8. NUnit 4 for all tests
9. Incremental generator (never store SemanticModel in pipeline)

## Skills Available
Check `.opencode/skills/` for domain-specific guidance on:
dotnet-guidelines, dotnet-patterns, dotnet-architecture, dotnet-backend-patterns,
ddd-patterns, dotnet-tdd, centralized-packages, nuget-packaging, csharpier,
source-generator-dev, legacy-normalizer, testcontainers, dotnet-versions
