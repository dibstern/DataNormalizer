using System.Diagnostics;
using DataNormalizer.Integration.Tests.TestTypes.DeepNesting;
using NUnit.Framework;

namespace DataNormalizer.Integration.Tests;

[TestFixture]
public sealed class PerformanceTests
{
    /// <summary>
    /// Times normalizing 10,000 complex 7-layer objects.
    /// Threshold starts generous and should be tightened as performance improves.
    /// </summary>
    [Test]
    public void Normalize_10000_Complex7LayerObjects_CompletesWithinThreshold()
    {
        // Build 10,000 Universe objects with realistic shared references
        var sharedCity = new City { Name = "Washington D.C.", Population = 700000 };
        var sharedCountry = new Country { Name = "United States", Capital = sharedCity };
        var sharedContinent = new Continent { Name = "North America", MainCountry = sharedCountry };

        var universes = new Universe[10_000];
        for (var i = 0; i < universes.Length; i++)
        {
            universes[i] = new Universe
            {
                Name = $"Universe-{i}",
                MainGalaxy = new Galaxy
                {
                    Name = $"Galaxy-{i}",
                    MainSystem = new SolarSystem
                    {
                        Name = $"System-{i}",
                        MainPlanet = new Planet
                        {
                            Name = $"Planet-{i}",
                            MainContinent =
                                i % 100 == 0
                                    ? new Continent
                                    {
                                        Name = $"Continent-{i}",
                                        MainCountry = new Country
                                        {
                                            Name = $"Country-{i}",
                                            Capital = new City { Name = $"City-{i}", Population = 100000 + i },
                                        },
                                    }
                                    : sharedContinent, // 99% share the same continent (tests dedup)
                        },
                    },
                },
            };
        }

        // Warm up
        DeepNestingConfig.Normalize(universes[0]);

        // Timed run: normalize all 10,000
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < universes.Length; i++)
        {
            var result = DeepNestingConfig.Normalize(universes[i]);
            // Minimal assertion to prevent dead-code elimination
            Assert.That(result.UniverseList.Length, Is.GreaterThan(result.RootIndex));
        }
        sw.Stop();

        var totalMs = sw.ElapsedMilliseconds;
        var perObjectUs = sw.Elapsed.TotalMicroseconds / universes.Length;

        TestContext.Out.WriteLine($"Normalized {universes.Length:N0} 7-layer objects in {totalMs}ms");
        TestContext.Out.WriteLine($"Per object: {perObjectUs:F1}us");
        TestContext.Out.WriteLine($"Throughput: {universes.Length * 1000.0 / totalMs:F0} objects/sec");

        // Threshold: 200ms for 10,000 objects. CI runners are 2-5x slower than dev machines.
        // Local baseline: ~26ms. Tighten as performance improves.
        Assert.That(
            totalMs,
            Is.LessThan(200),
            $"Normalizing 10,000 objects took {totalMs}ms — should complete within 200ms"
        );
    }

    /// <summary>
    /// Times the full roundtrip (normalize + denormalize) for 1,000 objects.
    /// </summary>
    [Test]
    public void Roundtrip_1000_Complex7LayerObjects_CompletesWithinThreshold()
    {
        var universes = new Universe[1_000];
        for (var i = 0; i < universes.Length; i++)
        {
            universes[i] = new Universe
            {
                Name = $"Universe-{i}",
                MainGalaxy = new Galaxy
                {
                    Name = $"Galaxy-{i}",
                    MainSystem = new SolarSystem
                    {
                        Name = $"System-{i}",
                        MainPlanet = new Planet
                        {
                            Name = $"Planet-{i}",
                            MainContinent = new Continent
                            {
                                Name = $"Continent-{i}",
                                MainCountry = new Country
                                {
                                    Name = $"Country-{i}",
                                    Capital = new City { Name = $"City-{i}", Population = i },
                                },
                            },
                        },
                    },
                },
            };
        }

        // Warm up
        var warmup = DeepNestingConfig.Normalize(universes[0]);
        DeepNestingConfig.Denormalize(warmup);

        // Timed run: normalize + denormalize all 1,000
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < universes.Length; i++)
        {
            var result = DeepNestingConfig.Normalize(universes[i]);
            var restored = DeepNestingConfig.Denormalize(result);
            Assert.That(restored.Name, Is.EqualTo($"Universe-{i}"));
        }
        sw.Stop();

        var totalMs = sw.ElapsedMilliseconds;
        var perObjectUs = sw.Elapsed.TotalMicroseconds / universes.Length;

        TestContext.Out.WriteLine($"Roundtripped {universes.Length:N0} 7-layer objects in {totalMs}ms");
        TestContext.Out.WriteLine($"Per object: {perObjectUs:F1}us (normalize + denormalize)");

        // Threshold: 50ms for 1,000 roundtrips. CI runners are 2-5x slower than dev machines.
        // Local baseline: ~3ms. Tighten as performance improves.
        Assert.That(
            totalMs,
            Is.LessThan(50),
            $"Roundtripping 1,000 objects took {totalMs}ms — should complete within 50ms"
        );
    }
}
