using System.Collections.Immutable;
using System.Diagnostics;
using DataNormalizer.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;

namespace DataNormalizer.Generators.Tests;

[TestFixture]
public sealed class GeneratorPerformanceTests
{
    /// <summary>
    /// Times the full generator pipeline for a complex 7-layer type graph.
    /// Threshold starts generous and should be tightened as performance improves.
    /// </summary>
    [Test]
    public void Generator_Complex7LayerTypeGraph_CompletesWithinThreshold()
    {
        var source = """
            using DataNormalizer.Attributes;
            using DataNormalizer.Configuration;
            using System.Collections.Generic;

            namespace PerfTest;

            public class City
            {
                public string Name { get; set; } = "";
                public int Population { get; set; }
                public string Country { get; set; } = "";
                public double Latitude { get; set; }
                public double Longitude { get; set; }
            }

            public class Country
            {
                public string Name { get; set; } = "";
                public string Code { get; set; } = "";
                public City Capital { get; set; } = new();
                public List<City> MajorCities { get; set; } = new();
            }

            public class Continent
            {
                public string Name { get; set; } = "";
                public int Area { get; set; }
                public Country LargestCountry { get; set; } = new();
                public List<Country> Countries { get; set; } = new();
            }

            public class Planet
            {
                public string Name { get; set; } = "";
                public double Mass { get; set; }
                public double Radius { get; set; }
                public Continent LargestContinent { get; set; } = new();
                public List<Continent> Continents { get; set; } = new();
            }

            public class SolarSystem
            {
                public string Name { get; set; } = "";
                public int PlanetCount { get; set; }
                public Planet MainPlanet { get; set; } = new();
                public List<Planet> Planets { get; set; } = new();
            }

            public class Galaxy
            {
                public string Name { get; set; } = "";
                public long StarCount { get; set; }
                public string Type { get; set; } = "";
                public SolarSystem MainSystem { get; set; } = new();
                public List<SolarSystem> Systems { get; set; } = new();
            }

            public class Universe
            {
                public string Name { get; set; } = "";
                public double Age { get; set; }
                public Galaxy MainGalaxy { get; set; } = new();
                public List<Galaxy> Galaxies { get; set; } = new();
            }

            [NormalizeConfiguration]
            public partial class PerfConfig : NormalizationConfig
            {
                protected override void Configure(NormalizeBuilder builder)
                {
                    builder.NormalizeGraph<Universe>();
                }
            }
            """;

        // Warm up: run once to JIT compile everything
        RunGenerator(source);

        // Timed run
        const int iterations = 5;
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++)
        {
            var result = RunGenerator(source);
            Assert.That(result.CompilationErrors, Is.Empty, "Generated code must compile");
        }
        sw.Stop();

        var avgMs = sw.ElapsedMilliseconds / (double)iterations;

        TestContext.Out.WriteLine($"Generator avg time for 7-layer graph: {avgMs:F1}ms ({iterations} iterations)");
        TestContext.Out.WriteLine($"Total: {sw.ElapsedMilliseconds}ms");

        // Threshold: 500ms per run. CI runners are 2-5x slower than dev machines.
        // Local baseline: ~58ms. Tighten as performance improves.
        Assert.That(
            avgMs,
            Is.LessThan(500),
            $"Generator took {avgMs:F1}ms avg — should complete within 500ms for a 7-layer type graph"
        );
    }

    // Reuse the RunGenerator helper from GeneratorEndToEndTests (or copy it)
    private static GeneratorRunResult RunGenerator(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);

        var references = AppDomain
            .CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .Cast<MetadataReference>()
            .ToList();

        var runtimeAssembly = typeof(DataNormalizer.Attributes.NormalizeConfigurationAttribute).Assembly.Location;
        if (!string.IsNullOrEmpty(runtimeAssembly))
            references.Add(MetadataReference.CreateFromFile(runtimeAssembly));

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );

        var generator = new NormalizeGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        var generatedSources = new List<(string hintName, string source)>();
        var runResult = driver.GetRunResult();
        foreach (var genResult in runResult.Results)
        foreach (var genSource in genResult.GeneratedSources)
            generatedSources.Add((genSource.HintName, genSource.SourceText.ToString()));

        var compilationErrors = outputCompilation
            .GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Select(d => $"{d.Id}: {d.GetMessage()} at {d.Location}")
            .ToArray();

        return new GeneratorRunResult(diagnostics, generatedSources, compilationErrors);
    }

    private sealed record GeneratorRunResult(
        ImmutableArray<Diagnostic> Diagnostics,
        List<(string hintName, string source)> GeneratedSources,
        string[] CompilationErrors
    );
}
