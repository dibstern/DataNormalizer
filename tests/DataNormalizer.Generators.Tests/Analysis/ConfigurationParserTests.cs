using DataNormalizer.Generators.Analysis;
using DataNormalizer.Generators.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NUnit.Framework;

namespace DataNormalizer.Generators.Tests.Analysis;

[TestFixture]
public sealed class ConfigurationParserTests
{
    [Test]
    public void Parse_NormalizeGraph_ExtractsRootType()
    {
        var model = ParseConfig(
            """
            builder.NormalizeGraph<Person>();
            """,
            additionalTypes: """
            public class Person { public string Name { get; set; } = ""; }
            """
        );

        Assert.That(model.RootTypes, Has.Length.EqualTo(1));
        Assert.That(model.RootTypes[0].FullyQualifiedName, Does.EndWith("Person"));
        Assert.That(model.AutoDiscover, Is.True);
    }

    [Test]
    public void Parse_ForType_ExtractsExplicitType()
    {
        var model = ParseConfig(
            """
            builder.ForType<Person>();
            """,
            additionalTypes: """
            public class Person { public string Name { get; set; } = ""; }
            """
        );

        Assert.That(model.ExplicitTypes, Does.Contain("TestApp.Person"));
    }

    [Test]
    public void Parse_InlineType_AddsToInlinedSet()
    {
        var model = ParseConfig(
            """
            builder.NormalizeGraph<Person>(graph =>
            {
                graph.Inline<Metadata>();
            });
            """,
            additionalTypes: """
            public class Person { public string Name { get; set; } = ""; public Metadata Meta { get; set; } = new(); }
            public class Metadata { public string Key { get; set; } = ""; }
            """
        );

        Assert.That(model.InlinedTypes, Does.Contain("TestApp.Metadata"));
    }

    [Test]
    public void Parse_IgnoreProperty_AddsToIgnoredSet()
    {
        var model = ParseConfig(
            """
            builder.ForType<Person>(p =>
            {
                p.IgnoreProperty(x => x.InternalId);
            });
            """,
            additionalTypes: """
            public class Person { public string Name { get; set; } = ""; public string? InternalId { get; set; } }
            """
        );

        var config = model.TypeConfigurations["TestApp.Person"];
        Assert.That(config.IgnoredProperties, Does.Contain("InternalId"));
    }

    [Test]
    public void Parse_CopySourceAttributes_SetsFlag()
    {
        var model = ParseConfig(
            """
            builder.NormalizeGraph<Person>(graph =>
            {
                graph.CopySourceAttributes();
            });
            """,
            additionalTypes: """
            public class Person { public string Name { get; set; } = ""; }
            """
        );

        Assert.That(model.CopySourceAttributes, Is.True);
    }

    [Test]
    public void Parse_ChainedCalls_BothPropertiesIgnored()
    {
        var model = ParseConfig(
            """
            builder.ForType<Person>(p =>
            {
                p.IgnoreProperty(x => x.Name).IgnoreProperty(x => x.Age);
            });
            """,
            additionalTypes: """
            public class Person { public string Name { get; set; } = ""; public int Age { get; set; } }
            """
        );

        var config = model.TypeConfigurations["TestApp.Person"];
        Assert.That(config.IgnoredProperties, Does.Contain("Name"));
        Assert.That(config.IgnoredProperties, Does.Contain("Age"));
    }

    [Test]
    public void Parse_LocalVariablePattern_CorrectlyAssociated()
    {
        var model = ParseConfig(
            """
            var graph = builder.NormalizeGraph<Person>();
            graph.Inline<Metadata>();
            """,
            additionalTypes: """
            public class Person { public string Name { get; set; } = ""; public Metadata Meta { get; set; } = new(); }
            public class Metadata { public string Key { get; set; } = ""; }
            """
        );

        Assert.That(model.InlinedTypes, Does.Contain("TestApp.Metadata"));
    }

    [Test]
    public void Parse_ParenthesizedLambda_WorksSameAsSimple()
    {
        var model = ParseConfig(
            """
            builder.NormalizeGraph<Person>((graph) =>
            {
                graph.Inline<Metadata>();
            });
            """,
            additionalTypes: """
            public class Person { public string Name { get; set; } = ""; public Metadata Meta { get; set; } = new(); }
            public class Metadata { public string Key { get; set; } = ""; }
            """
        );

        Assert.That(model.InlinedTypes, Does.Contain("TestApp.Metadata"));
    }

    [Test]
    public void Parse_EmptyConfigureBody_ReturnsEmptyModel()
    {
        var model = ParseConfig("", additionalTypes: "");

        Assert.That(model.RootTypes, Is.Empty);
        Assert.That(model.ExplicitTypes, Is.Empty);
        Assert.That(model.InlinedTypes, Is.Empty);
        Assert.That(model.TypeConfigurations, Is.Empty);
    }

    [Test]
    public void Parse_MultipleNormalizeGraphCalls_BothRootsExtracted()
    {
        var model = ParseConfig(
            """
            builder.NormalizeGraph<Person>();
            builder.NormalizeGraph<Order>();
            """,
            additionalTypes: """
            public class Person { public string Name { get; set; } = ""; }
            public class Order { public int Id { get; set; } }
            """
        );

        Assert.That(model.RootTypes, Has.Length.EqualTo(2));
        var rootNames = model.RootTypes.Select(r => r.FullyQualifiedName).ToArray();
        Assert.That(rootNames, Has.One.EndsWith("Person"));
        Assert.That(rootNames, Has.One.EndsWith("Order"));
    }

    [Test]
    public void Parse_GraphForType_NestedConfigOnGraphBuilder()
    {
        var model = ParseConfig(
            """
            builder.NormalizeGraph<Person>(graph =>
            {
                graph.ForType<Person>(p =>
                {
                    p.IgnoreProperty(x => x.InternalId);
                });
            });
            """,
            additionalTypes: """
            public class Person { public string Name { get; set; } = ""; public string? InternalId { get; set; } }
            """
        );

        var config = model.TypeConfigurations["TestApp.Person"];
        Assert.That(config.IgnoredProperties, Does.Contain("InternalId"));
    }

    [Test]
    public void Parse_ExtractsConfigClassName()
    {
        var model = ParseConfig(
            "builder.NormalizeGraph<Person>();",
            additionalTypes: """
            public class Person { public string Name { get; set; } = ""; }
            """
        );

        Assert.That(model.ConfigClassName, Is.EqualTo("TestConfig"));
    }

    [Test]
    public void Parse_ExtractsConfigNamespace()
    {
        var model = ParseConfig(
            "builder.NormalizeGraph<Person>();",
            additionalTypes: """
            public class Person { public string Name { get; set; } = ""; }
            """
        );

        Assert.That(model.ConfigNamespace, Is.EqualTo("TestApp"));
    }

    [Test]
    public void Parse_EmptyBody_StillHasConfigClassInfo()
    {
        var model = ParseConfig("", additionalTypes: "");

        Assert.That(model.ConfigClassName, Is.EqualTo("TestConfig"));
        Assert.That(model.ConfigNamespace, Is.EqualTo("TestApp"));
    }

    [Test]
    public void Parse_UseReferenceTrackingForCycles_SetsFlag()
    {
        var model = ParseConfig(
            """
            builder.NormalizeGraph<Person>(graph =>
            {
                graph.UseReferenceTrackingForCycles();
            });
            """,
            additionalTypes: """
            public class Person { public string Name { get; set; } = ""; }
            """
        );

        Assert.That(model.UseReferenceTrackingForCycles, Is.True);
    }

    // ---- Test Helper ----

    private static NormalizationModel ParseConfig(string configureBody, string additionalTypes)
    {
        var source = $$"""
            using DataNormalizer.Attributes;
            using DataNormalizer.Configuration;

            namespace TestApp;

            {{additionalTypes}}

            [NormalizeConfiguration]
            public partial class TestConfig : NormalizationConfig
            {
                protected override void Configure(NormalizeBuilder builder)
                {
                    {{configureBody}}
                }
            }
            """;

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

        var semanticModel = compilation.GetSemanticModel(syntaxTree);

        // Find the config class declaration
        var configClass = syntaxTree
            .GetRoot()
            .DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .First(c => c.Identifier.Text == "TestConfig");

        return ConfigurationParser.Parse(configClass, semanticModel);
    }
}
