using DataNormalizer.Integration.Tests.TestTypes.DeepNesting;
using NUnit.Framework;

namespace DataNormalizer.Integration.Tests;

[TestFixture]
public sealed class DeepNestingTests
{
    [Test]
    public void Normalize_7LevelChain_AllTypesNormalized()
    {
        var universe = CreateDeepUniverse();

        var result = DeepNestingConfig.Normalize(universe);
        var root = result.UniverseList[0];

        Assert.That(root, Is.Not.Null);
        Assert.That(root.Name, Is.EqualTo("Observable Universe"));

        // Should have collections for all 7 types
        Assert.That(result.UniverseList, Has.Length.GreaterThanOrEqualTo(1));
        Assert.That(result.GalaxyList, Has.Length.GreaterThanOrEqualTo(1));
        Assert.That(result.SolarSystemList, Has.Length.GreaterThanOrEqualTo(1));
        Assert.That(result.PlanetList, Has.Length.GreaterThanOrEqualTo(1));
        Assert.That(result.ContinentList, Has.Length.GreaterThanOrEqualTo(1));
        Assert.That(result.CountryList, Has.Length.GreaterThanOrEqualTo(1));
        Assert.That(result.CityList, Has.Length.GreaterThanOrEqualTo(1));
    }

    [Test]
    public void Roundtrip_7LevelChain_AllValuesPreserved()
    {
        var universe = CreateDeepUniverse();

        var result = DeepNestingConfig.Normalize(universe);
        var restored = DeepNestingConfig.Denormalize(result);

        Assert.That(restored.Name, Is.EqualTo("Observable Universe"));
        Assert.That(restored.MainGalaxy.Name, Is.EqualTo("Milky Way"));
        Assert.That(restored.MainGalaxy.MainSystem.Name, Is.EqualTo("Solar System"));
        Assert.That(restored.MainGalaxy.MainSystem.MainPlanet.Name, Is.EqualTo("Earth"));
        Assert.That(restored.MainGalaxy.MainSystem.MainPlanet.MainContinent.Name, Is.EqualTo("North America"));
        Assert.That(
            restored.MainGalaxy.MainSystem.MainPlanet.MainContinent.MainCountry.Name,
            Is.EqualTo("United States")
        );
        Assert.That(
            restored.MainGalaxy.MainSystem.MainPlanet.MainContinent.MainCountry.Capital.Name,
            Is.EqualTo("Washington D.C.")
        );
        Assert.That(
            restored.MainGalaxy.MainSystem.MainPlanet.MainContinent.MainCountry.Capital.Population,
            Is.EqualTo(700000)
        );
    }

    private static Universe CreateDeepUniverse()
    {
        return new Universe
        {
            Name = "Observable Universe",
            MainGalaxy = new Galaxy
            {
                Name = "Milky Way",
                MainSystem = new SolarSystem
                {
                    Name = "Solar System",
                    MainPlanet = new Planet
                    {
                        Name = "Earth",
                        MainContinent = new Continent
                        {
                            Name = "North America",
                            MainCountry = new Country
                            {
                                Name = "United States",
                                Capital = new City { Name = "Washington D.C.", Population = 700000 },
                            },
                        },
                    },
                },
            },
        };
    }
}
