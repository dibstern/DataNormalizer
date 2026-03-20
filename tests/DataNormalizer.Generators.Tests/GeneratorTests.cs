using System.Collections.Immutable;
using DataNormalizer.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;

namespace DataNormalizer.Generators.Tests;

[TestFixture]
public sealed class GeneratorTests
{
    [Test]
    public void Generator_WithNoConfiguration_ProducesNoOutput()
    {
        var source = """
            namespace TestApp;

            public class Foo
            {
                public string Name { get; set; } = "";
            }
            """;

        var (diagnostics, generatedTrees) = RunGenerator(source);

        Assert.That(diagnostics, Is.Empty);
        Assert.That(generatedTrees, Is.Empty);
    }

    [Test]
    public void Generator_WithNonPartialConfigClass_ProducesDN0002Error()
    {
        var source = """
            using DataNormalizer.Attributes;
            using DataNormalizer.Configuration;

            namespace TestApp;

            public class Person
            {
                public string Name { get; set; } = "";
            }

            [NormalizeConfiguration]
            public class TestConfig : NormalizationConfig
            {
                protected override void Configure(NormalizeBuilder builder)
                {
                    builder.NormalizeGraph<Person>();
                }
            }
            """;

        var (diagnostics, _) = RunGenerator(source);

        Assert.That(diagnostics, Has.Length.EqualTo(1));
        Assert.That(diagnostics[0].Id, Is.EqualTo("DN0002"));
        Assert.That(diagnostics[0].Severity, Is.EqualTo(DiagnosticSeverity.Error));
        Assert.That(diagnostics[0].GetMessage(), Does.Contain("TestConfig"));
    }

    [Test]
    public void Generator_WithPartialConfigClass_ProducesNoDN0002()
    {
        var source = """
            using DataNormalizer.Attributes;
            using DataNormalizer.Configuration;

            namespace TestApp;

            public class Person
            {
                public string Name { get; set; } = "";
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

        var (diagnostics, _) = RunGenerator(source);

        var dn0002 = diagnostics.Where(d => d.Id == "DN0002").ToArray();
        Assert.That(dn0002, Is.Empty);
    }

    private static (ImmutableArray<Diagnostic> Diagnostics, ImmutableArray<SyntaxTree> GeneratedTrees) RunGenerator(
        string source
    )
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

        var generatedTrees = outputCompilation.SyntaxTrees.Except([syntaxTree]).ToImmutableArray();

        return (diagnostics, generatedTrees);
    }
}
