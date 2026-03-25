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

    // ---- UseNaming Tests ----

    [Test]
    public void Parse_UseNaming_StringProperties_ExtractsDtoSuffixAndPrefix()
    {
        var model = ParseConfig(
            """
            builder.UseNaming(n =>
            {
                n.DtoSuffix = "Dto";
                n.DtoPrefix = "";
            });
            builder.NormalizeGraph<Person>();
            """,
            additionalTypes: """
            public class Person { public string Name { get; set; } = ""; }
            """
        );

        Assert.That(model.Naming.DtoSuffix, Is.EqualTo("Dto"));
        Assert.That(model.Naming.DtoPrefix, Is.EqualTo(""));
    }

    [Test]
    public void Parse_UseNaming_EmitJsonPropertyNamesFalse_ParsesBooleanFalse()
    {
        var model = ParseConfig(
            """
            builder.UseNaming(n =>
            {
                n.EmitJsonPropertyNames = false;
            });
            builder.NormalizeGraph<Person>();
            """,
            additionalTypes: """
            public class Person { public string Name { get; set; } = ""; }
            """
        );

        Assert.That(model.Naming.EmitJsonPropertyNames, Is.False);
    }

    [Test]
    public void Parse_UseNaming_EmitJsonPropertyNamesTrue_ParsesBooleanTrue()
    {
        var model = ParseConfig(
            """
            builder.UseNaming(n =>
            {
                n.EmitJsonPropertyNames = true;
            });
            builder.NormalizeGraph<Person>();
            """,
            additionalTypes: """
            public class Person { public string Name { get; set; } = ""; }
            """
        );

        Assert.That(model.Naming.EmitJsonPropertyNames, Is.True);
    }

    [Test]
    public void Parse_NoUseNaming_ReturnsDefaultNamingModel()
    {
        var model = ParseConfig(
            """
            builder.NormalizeGraph<Person>();
            """,
            additionalTypes: """
            public class Person { public string Name { get; set; } = ""; }
            """
        );

        Assert.That(model.Naming.DtoPrefix, Is.EqualTo(""));
        Assert.That(model.Naming.DtoSuffix, Is.EqualTo("Dto"));
        Assert.That(model.Naming.ContainerSuffix, Is.EqualTo("Dto"));
        Assert.That(model.Naming.EmitJsonPropertyNames, Is.True);
    }

    [Test]
    public void Parse_UseNaming_NonLiteralRhs_SilentlyIgnored_DefaultPreserved()
    {
        var model = ParseConfig(
            """
            var suffix = "Model";
            builder.UseNaming(n =>
            {
                n.DtoSuffix = suffix;
            });
            builder.NormalizeGraph<Person>();
            """,
            additionalTypes: """
            public class Person { public string Name { get; set; } = ""; }
            """
        );

        Assert.That(model.Naming.DtoSuffix, Is.EqualTo("Dto"));
    }

    [Test]
    public void Parse_UseNaming_CalledTwice_LastValuesWin()
    {
        var model = ParseConfig(
            """
            builder.UseNaming(n =>
            {
                n.DtoSuffix = "First";
            });
            builder.UseNaming(n =>
            {
                n.DtoSuffix = "Second";
            });
            builder.NormalizeGraph<Person>();
            """,
            additionalTypes: """
            public class Person { public string Name { get; set; } = ""; }
            """
        );

        Assert.That(model.Naming.DtoSuffix, Is.EqualTo("Second"));
    }

    // ---- UseNaming Merge Semantics Tests ----

    [Test]
    public void Parse_UseNaming_GlobalDtoSuffix_GraphContainerSuffix_BothApply()
    {
        var model = ParseConfig(
            """
            builder.UseNaming(n =>
            {
                n.DtoSuffix = "Model";
            });
            builder.NormalizeGraph<Person>(graph =>
            {
                graph.UseNaming(n =>
                {
                    n.ContainerSuffix = "Container";
                });
            });
            """,
            additionalTypes: """
            public class Person { public string Name { get; set; } = ""; }
            """
        );

        Assert.That(model.Naming.DtoSuffix, Is.EqualTo("Model"));
        Assert.That(model.Naming.ContainerSuffix, Is.EqualTo("Container"));
    }

    [Test]
    public void Parse_UseNaming_GlobalOnly_AllGlobalValuesApply()
    {
        var model = ParseConfig(
            """
            builder.UseNaming(n =>
            {
                n.DtoPrefix = "Normalized";
                n.DtoSuffix = "Model";
                n.ContainerSuffix = "Result";
                n.EmitJsonPropertyNames = false;
            });
            builder.NormalizeGraph<Person>();
            """,
            additionalTypes: """
            public class Person { public string Name { get; set; } = ""; }
            """
        );

        Assert.That(model.Naming.DtoPrefix, Is.EqualTo("Normalized"));
        Assert.That(model.Naming.DtoSuffix, Is.EqualTo("Model"));
        Assert.That(model.Naming.ContainerSuffix, Is.EqualTo("Result"));
        Assert.That(model.Naming.EmitJsonPropertyNames, Is.False);
    }

    [Test]
    public void Parse_UseNaming_GraphOverridesEverything_FullyOverridden()
    {
        var model = ParseConfig(
            """
            builder.UseNaming(n =>
            {
                n.DtoPrefix = "GlobalPrefix";
                n.DtoSuffix = "GlobalSuffix";
                n.ContainerSuffix = "GlobalContainer";
                n.EmitJsonPropertyNames = false;
            });
            builder.NormalizeGraph<Person>(graph =>
            {
                graph.UseNaming(n =>
                {
                    n.DtoPrefix = "GraphPrefix";
                    n.DtoSuffix = "GraphSuffix";
                    n.ContainerSuffix = "GraphContainer";
                    n.EmitJsonPropertyNames = true;
                });
            });
            """,
            additionalTypes: """
            public class Person { public string Name { get; set; } = ""; }
            """
        );

        Assert.That(model.Naming.DtoPrefix, Is.EqualTo("GraphPrefix"));
        Assert.That(model.Naming.DtoSuffix, Is.EqualTo("GraphSuffix"));
        Assert.That(model.Naming.ContainerSuffix, Is.EqualTo("GraphContainer"));
        Assert.That(model.Naming.EmitJsonPropertyNames, Is.True);
    }

    [Test]
    public void Parse_UseNaming_ContainerSuffix_Extracted()
    {
        var model = ParseConfig(
            """
            builder.UseNaming(n =>
            {
                n.ContainerSuffix = "Result";
            });
            builder.NormalizeGraph<Person>();
            """,
            additionalTypes: """
            public class Person { public string Name { get; set; } = ""; }
            """
        );

        Assert.That(model.Naming.ContainerSuffix, Is.EqualTo("Result"));
    }

    [Test]
    public void Parse_UseJsonNaming_SetsEmitJsonPropertyNamesTrue()
    {
        var model = ParseConfig(
            """
            builder.NormalizeGraph<Person>(graph =>
            {
                graph.UseJsonNaming(System.Text.Json.JsonNamingPolicy.CamelCase);
            });
            """,
            additionalTypes: """
            public class Person { public string Name { get; set; } = ""; }
            """
        );

        Assert.That(model.Naming.EmitJsonPropertyNames, Is.True);
    }

    // ---- UseJsonNaming + UseNaming Interaction/Ordering Tests ----

    [Test]
    public void Parse_UseJsonNamingThenUseNamingFalse_EmitJsonPropertyNamesFalse()
    {
        // UseJsonNaming bridge sets EmitJsonPropertyNames = true,
        // but subsequent UseNaming(EmitJsonPropertyNames = false) should override it.
        var model = ParseConfig(
            """
            builder.NormalizeGraph<Person>(graph =>
            {
                graph.UseJsonNaming(System.Text.Json.JsonNamingPolicy.CamelCase);
            });
            builder.UseNaming(n => { n.EmitJsonPropertyNames = false; });
            """,
            additionalTypes: """
            public class Person { public string Name { get; set; } = ""; }
            """
        );

        Assert.That(model.Naming.EmitJsonPropertyNames, Is.False);
    }

    [Test]
    public void Parse_UseNamingFalseThenUseJsonNaming_EmitJsonPropertyNamesTrue()
    {
        // UseNaming sets EmitJsonPropertyNames = false first,
        // but subsequent UseJsonNaming bridge sets it back to true.
        var model = ParseConfig(
            """
            builder.UseNaming(n => { n.EmitJsonPropertyNames = false; });
            builder.NormalizeGraph<Person>(graph =>
            {
                graph.UseJsonNaming(System.Text.Json.JsonNamingPolicy.CamelCase);
            });
            """,
            additionalTypes: """
            public class Person { public string Name { get; set; } = ""; }
            """
        );

        Assert.That(model.Naming.EmitJsonPropertyNames, Is.True);
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
