using System.Collections.Immutable;
using DataNormalizer.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;

namespace DataNormalizer.Generators.Tests;

[TestFixture]
public sealed class GeneratorEndToEndTests
{
    [Test]
    public void E2E_SimpleAutoDiscover_GeneratesDtoAndMethods()
    {
        var source = """
            using DataNormalizer.Attributes;
            using DataNormalizer.Configuration;

            namespace TestApp;

            public class Address
            {
                public string Street { get; set; } = "";
                public string City { get; set; } = "";
            }

            public class Person
            {
                public string Name { get; set; } = "";
                public int Age { get; set; }
                public Address HomeAddress { get; set; } = new();
            }

            [NormalizeConfiguration]
            public partial class TestConfig : NormalizationConfig
            {
                protected override void Configure(NormalizeBuilder builder)
                {
                    builder.NormalizeGraph<Person>();
                }
            }
            """;

        var result = RunGenerator(source);

        // Should generate DTOs
        Assert.That(
            result.GeneratedSources,
            Has.Count.GreaterThanOrEqualTo(3),
            "Expected at least 3 generated sources (NormalizedPerson, NormalizedAddress, config methods)"
        );

        var allSource = string.Join("\n", result.GeneratedSources.Select(s => s.source));

        // DTO classes
        Assert.That(allSource, Does.Contain("class NormalizedPerson"));
        Assert.That(allSource, Does.Contain("class NormalizedAddress"));

        // Normalize method
        Assert.That(allSource, Does.Contain("Normalize(TestApp.Person source)"));

        // Denormalize method
        Assert.That(allSource, Does.Contain("Denormalize("));

        // Generated code compiles
        Assert.That(
            result.CompilationErrors,
            Is.Empty,
            $"Generated code has errors:\n{string.Join("\n", result.CompilationErrors)}"
        );
    }

    [Test]
    public void E2E_WithInlinedType_InlinedTypeNotInGraph()
    {
        var source = """
            using DataNormalizer.Attributes;
            using DataNormalizer.Configuration;

            namespace TestApp;

            public class Metadata { public string Key { get; set; } = ""; }
            public class Person
            {
                public string Name { get; set; } = "";
                public Metadata Meta { get; set; } = new();
            }

            [NormalizeConfiguration]
            public partial class TestConfig : NormalizationConfig
            {
                protected override void Configure(NormalizeBuilder builder)
                {
                    builder.NormalizeGraph<Person>(graph =>
                    {
                        graph.Inline<Metadata>();
                    });
                }
            }
            """;

        var result = RunGenerator(source);
        var allSource = string.Join("\n", result.GeneratedSources.Select(s => s.source));

        // NormalizedPerson exists, NormalizedMetadata does NOT
        Assert.That(allSource, Does.Contain("class NormalizedPerson"));
        Assert.That(allSource, Does.Not.Contain("class NormalizedMetadata"));
        Assert.That(result.CompilationErrors, Is.Empty);
    }

    [Test]
    public void E2E_WithIgnoredProperty_PropertyExcluded()
    {
        var source = """
            using DataNormalizer.Attributes;
            using DataNormalizer.Configuration;

            namespace TestApp;

            public class Person
            {
                public string Name { get; set; } = "";
                public string InternalId { get; set; } = "";
            }

            [NormalizeConfiguration]
            public partial class TestConfig : NormalizationConfig
            {
                protected override void Configure(NormalizeBuilder builder)
                {
                    builder.NormalizeGraph<Person>();
                    builder.ForType<Person>(p =>
                    {
                        p.IgnoreProperty(x => x.InternalId);
                    });
                }
            }
            """;

        var result = RunGenerator(source);
        var allSource = string.Join("\n", result.GeneratedSources.Select(s => s.source));

        Assert.That(allSource, Does.Contain("class NormalizedPerson"));
        Assert.That(allSource, Does.Contain("Name"));
        // InternalId should NOT appear in the DTO
        Assert.That(allSource, Does.Not.Contain("InternalId"));
        Assert.That(result.CompilationErrors, Is.Empty);
    }

    [Test]
    public void E2E_CircularReference_ReportsDN0001()
    {
        var source = """
            using DataNormalizer.Attributes;
            using DataNormalizer.Configuration;
            using System.Collections.Generic;

            namespace TestApp;

            public class TreeNode
            {
                public string Label { get; set; } = "";
                public TreeNode? Parent { get; set; }
                public List<TreeNode> Children { get; set; } = new();
            }

            [NormalizeConfiguration]
            public partial class TestConfig : NormalizationConfig
            {
                protected override void Configure(NormalizeBuilder builder)
                {
                    builder.NormalizeGraph<TreeNode>();
                }
            }
            """;

        var result = RunGenerator(source);

        // Should generate output AND report DN0001 warning
        var dn0001 = result.Diagnostics.Where(d => d.Id == "DN0001").ToArray();
        Assert.That(dn0001, Has.Length.GreaterThanOrEqualTo(1));
        Assert.That(dn0001[0].Severity, Is.EqualTo(DiagnosticSeverity.Warning));

        var allSource = string.Join("\n", result.GeneratedSources.Select(s => s.source));
        Assert.That(allSource, Does.Contain("class NormalizedTreeNode"));
        Assert.That(allSource, Does.Contain("var keyDto = new")); // two-DTO pattern for circular types
        Assert.That(allSource, Does.Contain("var fullDto = new")); // full DTO with all properties
        Assert.That(result.CompilationErrors, Is.Empty);
    }

    [Test]
    public void E2E_MultipleRootTypes_SharedTypesEmittedOnce()
    {
        var source = """
            using DataNormalizer.Attributes;
            using DataNormalizer.Configuration;

            namespace TestApp;

            public class Address { public string Street { get; set; } = ""; }
            public class Person
            {
                public string Name { get; set; } = "";
                public Address HomeAddress { get; set; } = new();
            }
            public class Order
            {
                public int Id { get; set; }
                public Address ShippingAddress { get; set; } = new();
            }

            [NormalizeConfiguration]
            public partial class TestConfig : NormalizationConfig
            {
                protected override void Configure(NormalizeBuilder builder)
                {
                    builder.NormalizeGraph<Person>();
                    builder.NormalizeGraph<Order>();
                }
            }
            """;

        var result = RunGenerator(source);
        var allSource = string.Join("\n", result.GeneratedSources.Select(s => s.source));

        // Both root types have Normalize methods
        Assert.That(allSource, Does.Contain("Normalize(TestApp.Person source)"));
        Assert.That(allSource, Does.Contain("Normalize(TestApp.Order source)"));

        // Address DTO emitted exactly once (check file names)
        var addressFiles = result.GeneratedSources.Count(s => s.hintName.Contains("NormalizedAddress"));
        Assert.That(addressFiles, Is.EqualTo(1), "NormalizedAddress should be emitted exactly once");

        Assert.That(result.CompilationErrors, Is.Empty);
    }

    [Test]
    public void E2E_EmptyConfigureBody_NoOutput()
    {
        var source = """
            using DataNormalizer.Attributes;
            using DataNormalizer.Configuration;

            namespace TestApp;

            [NormalizeConfiguration]
            public partial class TestConfig : NormalizationConfig
            {
                protected override void Configure(NormalizeBuilder builder)
                {
                }
            }
            """;

        var result = RunGenerator(source);

        Assert.That(result.GeneratedSources, Is.Empty);
        Assert.That(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error), Is.Empty);
    }

    [Test]
    public void E2E_NonPartialClass_DN0002()
    {
        var source = """
            using DataNormalizer.Attributes;
            using DataNormalizer.Configuration;

            namespace TestApp;

            public class Person { public string Name { get; set; } = ""; }

            [NormalizeConfiguration]
            public class TestConfig : NormalizationConfig
            {
                protected override void Configure(NormalizeBuilder builder)
                {
                    builder.NormalizeGraph<Person>();
                }
            }
            """;

        var result = RunGenerator(source);

        Assert.That(result.Diagnostics.Any(d => d.Id == "DN0002"), Is.True);
        Assert.That(result.GeneratedSources, Is.Empty);
    }

    [Test]
    public void E2E_GeneratedCodeCompilesWithoutErrors()
    {
        var source = """
            using DataNormalizer.Attributes;
            using DataNormalizer.Configuration;
            using System.Collections.Generic;

            namespace TestApp;

            public class Address
            {
                public string Street { get; set; } = "";
            }
            public class PhoneNumber
            {
                public string Number { get; set; } = "";
            }
            public class Person
            {
                public string Name { get; set; } = "";
                public int Age { get; set; }
                public Address HomeAddress { get; set; } = new();
                public Address? WorkAddress { get; set; }
                public List<PhoneNumber> PhoneNumbers { get; set; } = new();
            }

            [NormalizeConfiguration]
            public partial class TestConfig : NormalizationConfig
            {
                protected override void Configure(NormalizeBuilder builder)
                {
                    builder.NormalizeGraph<Person>();
                }
            }
            """;

        var result = RunGenerator(source);

        Assert.That(
            result.CompilationErrors,
            Is.Empty,
            $"Generated code has compilation errors:\n{string.Join("\n", result.CompilationErrors)}"
        );

        var allSource = string.Join("\n", result.GeneratedSources.Select(s => s.source));
        // Verify key structures
        Assert.That(allSource, Does.Contain("NormalizedPerson"));
        Assert.That(allSource, Does.Contain("NormalizedAddress"));
        Assert.That(allSource, Does.Contain("NormalizedPhoneNumber"));
        Assert.That(allSource, Does.Contain("HomeAddressIndex"));
        Assert.That(allSource, Does.Contain("WorkAddressIndex"));
        Assert.That(allSource, Does.Contain("PhoneNumbersIndices"));
    }

    [Test]
    public void E2E_NormalizerAndDenormalizerPartialClassesMergeCleanly()
    {
        var source = """
            using DataNormalizer.Attributes;
            using DataNormalizer.Configuration;

            namespace TestApp;

            public class Person { public string Name { get; set; } = ""; }

            [NormalizeConfiguration]
            public partial class TestConfig : NormalizationConfig
            {
                protected override void Configure(NormalizeBuilder builder)
                {
                    builder.NormalizeGraph<Person>();
                }
            }
            """;

        var result = RunGenerator(source);

        var allSource = string.Join("\n", result.GeneratedSources.Select(s => s.source));
        // Both Normalize and Denormalize present
        Assert.That(allSource, Does.Contain("Normalize(TestApp.Person source)"));
        Assert.That(allSource, Does.Contain("Denormalize("));
        // No duplicate member errors
        Assert.That(result.CompilationErrors, Is.Empty);
    }

    [Test]
    public void E2E_MultipleRoots_ContainersOnlyHaveReachableEntityLists()
    {
        var source = """
            using DataNormalizer.Attributes;
            using DataNormalizer.Configuration;

            namespace TestApp;

            public class Address { public string Street { get; set; } = ""; }
            public class PhoneNumber { public string Number { get; set; } = ""; }
            public class Person
            {
                public string Name { get; set; } = "";
                public Address HomeAddress { get; set; } = new();
                public PhoneNumber Phone { get; set; } = new();
            }
            public class Order
            {
                public int Id { get; set; }
                public Address ShippingAddress { get; set; } = new();
            }

            [NormalizeConfiguration]
            public partial class TestConfig : NormalizationConfig
            {
                protected override void Configure(NormalizeBuilder builder)
                {
                    builder.NormalizeGraph<Person>();
                    builder.NormalizeGraph<Order>();
                }
            }
            """;

        var result = RunGenerator(source);

        // Find the container source for Person and Order
        var personContainerSource = result
            .GeneratedSources.First(s => s.hintName.Contains("NormalizedPersonResult"))
            .source;
        var orderContainerSource = result
            .GeneratedSources.First(s => s.hintName.Contains("NormalizedOrderResult"))
            .source;

        // Person container should have Person, Address, PhoneNumber lists
        Assert.That(personContainerSource, Does.Contain("PersonList"));
        Assert.That(personContainerSource, Does.Contain("AddressList"));
        Assert.That(personContainerSource, Does.Contain("PhoneNumberList"));
        // Person container should NOT have Order list
        Assert.That(personContainerSource, Does.Not.Contain("OrderList"));

        // Order container should have Order and Address lists
        Assert.That(orderContainerSource, Does.Contain("OrderList"));
        Assert.That(orderContainerSource, Does.Contain("AddressList"));
        // Order container should NOT have Person or PhoneNumber lists
        Assert.That(orderContainerSource, Does.Not.Contain("PersonList"));
        Assert.That(orderContainerSource, Does.Not.Contain("PhoneNumberList"));

        // Normalizer: Person's Normalize method should populate Person, Address, PhoneNumber lists only
        var normalizerSource = result.GeneratedSources.First(s => s.hintName.Contains("Normalizer.g.cs")).source;
        // Extract the Person normalize method (from "Normalize(TestApp.Person" to the next public method or end)
        var personNormalizeStart = normalizerSource.IndexOf("Normalize(TestApp.Person source)");
        var orderNormalizeStart = normalizerSource.IndexOf("Normalize(TestApp.Order source)");
        var personNormalizeMethod = normalizerSource.Substring(
            personNormalizeStart,
            orderNormalizeStart - personNormalizeStart
        );
        Assert.That(personNormalizeMethod, Does.Contain("result.PersonList"));
        Assert.That(personNormalizeMethod, Does.Contain("result.AddressList"));
        Assert.That(personNormalizeMethod, Does.Contain("result.PhoneNumberList"));
        Assert.That(personNormalizeMethod, Does.Not.Contain("result.OrderList"));

        // Order's Normalize method should populate Order and Address lists only
        var orderNormalizeMethod = normalizerSource.Substring(orderNormalizeStart);
        // Cut it at the first private method
        var privateMethodStart = orderNormalizeMethod.IndexOf("    private static int Normalize");
        if (privateMethodStart > 0)
            orderNormalizeMethod = orderNormalizeMethod.Substring(0, privateMethodStart);
        Assert.That(orderNormalizeMethod, Does.Contain("result.OrderList"));
        Assert.That(orderNormalizeMethod, Does.Contain("result.AddressList"));
        Assert.That(orderNormalizeMethod, Does.Not.Contain("result.PersonList"));
        Assert.That(orderNormalizeMethod, Does.Not.Contain("result.PhoneNumberList"));

        // Generated code should compile without errors
        Assert.That(
            result.CompilationErrors,
            Is.Empty,
            $"Generated code has compilation errors:\n{string.Join("\n", result.CompilationErrors)}"
        );
    }

    // ---- Test Infrastructure ----

    private static GeneratorRunResult RunGenerator(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);

        var references = AppDomain
            .CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .Cast<MetadataReference>()
            .ToList();

        // Add the DataNormalizer runtime assembly (for attributes and config types)
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
        {
            foreach (var genSource in genResult.GeneratedSources)
            {
                generatedSources.Add((genSource.HintName, genSource.SourceText.ToString()));
            }
        }

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
