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

    // ---- UseJsonContract Tests ----

    [Test]
    public void Parse_UseJsonContract_RootPropertyName_Extracted()
    {
        var model = ParseConfig(
            """
            builder.NormalizeGraph<Person>(graph =>
            {
                graph.UseJsonContract(c => { c.RootPropertyName = "result"; });
            });
            """,
            additionalTypes: """
            public class Person { public string Name { get; set; } = ""; }
            """
        );

        Assert.That(model.JsonContract.RootPropertyName, Is.EqualTo("result"));
    }

    [Test]
    public void Parse_UseJsonContract_Collection_ExtractsJsonName()
    {
        var model = ParseConfig(
            """
            builder.NormalizeGraph<Person>(graph =>
            {
                graph.UseJsonContract(c => { c.Collection<SearchLine>("lines"); });
            });
            """,
            additionalTypes: """
            public class Person { public string Name { get; set; } = ""; }
            public class SearchLine { public string Text { get; set; } = ""; }
            """
        );

        Assert.That(model.JsonContract.CollectionJsonNames, Has.Count.EqualTo(1));
        Assert.That(model.JsonContract.CollectionJsonNames.Values, Does.Contain("lines"));
    }

    [Test]
    public void Parse_UseJsonContract_MultipleCollections_AllStored()
    {
        var model = ParseConfig(
            """
            builder.NormalizeGraph<Person>(graph =>
            {
                graph.UseJsonContract(c =>
                {
                    c.Collection<SearchLine>("lines");
                    c.Collection<Address>("addresses");
                });
            });
            """,
            additionalTypes: """
            public class Person { public string Name { get; set; } = ""; }
            public class SearchLine { public string Text { get; set; } = ""; }
            public class Address { public string Street { get; set; } = ""; }
            """
        );

        Assert.That(model.JsonContract.CollectionJsonNames, Has.Count.EqualTo(2));
        Assert.That(model.JsonContract.CollectionJsonNames.Values, Does.Contain("lines"));
        Assert.That(model.JsonContract.CollectionJsonNames.Values, Does.Contain("addresses"));
    }

    [Test]
    public void Parse_UseJsonContract_RootPropertyNameEmptyString_StoredAsEmpty()
    {
        var model = ParseConfig(
            """
            builder.NormalizeGraph<Person>(graph =>
            {
                graph.UseJsonContract(c => { c.RootPropertyName = ""; });
            });
            """,
            additionalTypes: """
            public class Person { public string Name { get; set; } = ""; }
            """
        );

        Assert.That(model.JsonContract.RootPropertyName, Is.EqualTo(""));
    }

    [Test]
    public void Parse_NoUseJsonContract_ReturnsDefaultJsonContract()
    {
        var model = ParseConfig(
            """
            builder.NormalizeGraph<Person>();
            """,
            additionalTypes: """
            public class Person { public string Name { get; set; } = ""; }
            """
        );

        Assert.That(model.JsonContract.RootPropertyName, Is.Null);
        Assert.That(model.JsonContract.CollectionJsonNames, Is.Empty);
    }

    [Test]
    public void Parse_UseJsonContract_BothRootAndCollections_AllExtracted()
    {
        var model = ParseConfig(
            """
            builder.NormalizeGraph<Person>(graph =>
            {
                graph.UseJsonContract(c =>
                {
                    c.RootPropertyName = "result";
                    c.Collection<SearchLine>("lines");
                    c.Collection<Address>("addresses");
                });
            });
            """,
            additionalTypes: """
            public class Person { public string Name { get; set; } = ""; }
            public class SearchLine { public string Text { get; set; } = ""; }
            public class Address { public string Street { get; set; } = ""; }
            """
        );

        Assert.That(model.JsonContract.RootPropertyName, Is.EqualTo("result"));
        Assert.That(model.JsonContract.CollectionJsonNames, Has.Count.EqualTo(2));
        Assert.That(model.JsonContract.CollectionJsonNames.Values, Does.Contain("lines"));
        Assert.That(model.JsonContract.CollectionJsonNames.Values, Does.Contain("addresses"));
    }

    [Test]
    public void Parse_UseJsonContract_EmptyLambdaBody_NoCrash_DefaultJsonContract()
    {
        var model = ParseConfig(
            """
            builder.NormalizeGraph<Person>(graph =>
            {
                graph.UseJsonContract(c => { });
            });
            """,
            additionalTypes: """
            public class Person { public string Name { get; set; } = ""; }
            """
        );

        Assert.That(model.JsonContract.RootPropertyName, Is.Null);
        Assert.That(model.JsonContract.CollectionJsonNames, Is.Empty);
    }

    [Test]
    public void Parse_UseJsonContract_CalledTwice_LastWins()
    {
        var model = ParseConfig(
            """
            builder.NormalizeGraph<Person>(graph =>
            {
                graph.UseJsonContract(c => { c.RootPropertyName = "first"; });
                graph.UseJsonContract(c => { c.RootPropertyName = "second"; });
            });
            """,
            additionalTypes: """
            public class Person { public string Name { get; set; } = ""; }
            """
        );

        Assert.That(model.JsonContract.RootPropertyName, Is.EqualTo("second"));
    }

    [Test]
    public void Parse_UseJsonContract_CollectionWithNonLiteralArg_SilentlySkipped()
    {
        var model = ParseConfig(
            """
            builder.NormalizeGraph<Person>(graph =>
            {
                var name = "lines";
                graph.UseJsonContract(c => { c.Collection<SearchLine>(name); });
            });
            """,
            additionalTypes: """
            public class Person { public string Name { get; set; } = ""; }
            public class SearchLine { public string Text { get; set; } = ""; }
            """
        );

        Assert.That(model.JsonContract.CollectionJsonNames, Is.Empty);
    }

    [Test]
    public void Parse_UseJsonContract_CollectionWithDifferentNamespaceType_FqnStoredCorrectly()
    {
        var model = ParseConfig(
            """
            builder.NormalizeGraph<Person>(graph =>
            {
                graph.UseJsonContract(c => { c.Collection<OtherNamespace.Widget>("widgets"); });
            });
            """,
            additionalTypes: """
            public class Person { public string Name { get; set; } = ""; }
            """,
            extraNamespaceTypes: """
            namespace OtherNamespace
            {
                public class Widget { public string Id { get; set; } = ""; }
            }
            """
        );

        Assert.That(model.JsonContract.CollectionJsonNames, Has.Count.EqualTo(1));
        Assert.That(model.JsonContract.CollectionJsonNames.ContainsKey("OtherNamespace.Widget"), Is.True);
        Assert.That(model.JsonContract.CollectionJsonNames["OtherNamespace.Widget"], Is.EqualTo("widgets"));
    }

    [Test]
    public void Parse_UseJsonContract_DuplicateCollectionSameType_DiagnosticDN1002()
    {
        var model = ParseConfig(
            """
            builder.NormalizeGraph<Person>(graph =>
            {
                graph.UseJsonContract(c =>
                {
                    c.Collection<SearchLine>("lines");
                    c.Collection<SearchLine>("search_lines");
                });
            });
            """,
            additionalTypes: """
            public class Person { public string Name { get; set; } = ""; }
            public class SearchLine { public string Text { get; set; } = ""; }
            """
        );

        // Last value wins for the collection
        Assert.That(model.JsonContract.CollectionJsonNames["TestApp.SearchLine"], Is.EqualTo("search_lines"));

        // DN1002 diagnostic should be reported
        Assert.That(model.Diagnostics, Has.Length.EqualTo(1));
        Assert.That(model.Diagnostics[0].Id, Is.EqualTo("DN1002"));
        Assert.That(model.Diagnostics[0].TypeName, Does.Contain("SearchLine"));
    }

    // ---- Reference().JsonName() Tests ----

    [Test]
    public void Parse_ReferenceJsonName_FluentChain_StoresPropertyJsonNameOverride()
    {
        var model = ParseConfig(
            """
            builder.ForType<Person>(x =>
            {
                x.Reference(p => p.Line).JsonName("line");
            });
            """,
            additionalTypes: """
            public class Person { public string Name { get; set; } = ""; public SearchLine? Line { get; set; } }
            public class SearchLine { public string Text { get; set; } = ""; }
            """
        );

        Assert.That(model.PropertyJsonNameOverrides, Has.Count.EqualTo(1));
        Assert.That(model.PropertyJsonNameOverrides["TestApp.Person.Line"], Is.EqualTo("line"));
    }

    [Test]
    public void Parse_ReferenceCollectionJsonName_FluentChain_StoresPropertyJsonNameOverride()
    {
        var model = ParseConfig(
            """
            builder.ForType<Person>(x =>
            {
                x.ReferenceCollection(p => p.Items).JsonName("items");
            });
            """,
            additionalTypes: """
            public class Person { public string Name { get; set; } = ""; public System.Collections.Generic.List<Item> Items { get; set; } = new(); }
            public class Item { public int Id { get; set; } }
            """
        );

        Assert.That(model.PropertyJsonNameOverrides, Has.Count.EqualTo(1));
        Assert.That(model.PropertyJsonNameOverrides["TestApp.Person.Items"], Is.EqualTo("items"));
    }

    [Test]
    public void Parse_MultipleReferenceJsonName_OnSameTypeBuilder_AllStored()
    {
        var model = ParseConfig(
            """
            builder.ForType<Person>(x =>
            {
                x.Reference(p => p.Line).JsonName("line");
                x.Reference(p => p.Address).JsonName("addr");
            });
            """,
            additionalTypes: """
            public class Person { public string Name { get; set; } = ""; public SearchLine? Line { get; set; } public Address? Address { get; set; } }
            public class SearchLine { public string Text { get; set; } = ""; }
            public class Address { public string Street { get; set; } = ""; }
            """
        );

        Assert.That(model.PropertyJsonNameOverrides, Has.Count.EqualTo(2));
        Assert.That(model.PropertyJsonNameOverrides["TestApp.Person.Line"], Is.EqualTo("line"));
        Assert.That(model.PropertyJsonNameOverrides["TestApp.Person.Address"], Is.EqualTo("addr"));
    }

    [Test]
    public void Parse_ReferenceWithoutJsonName_NoEntry_NoCrash()
    {
        var model = ParseConfig(
            """
            builder.ForType<Person>(x =>
            {
                x.Reference(p => p.Line);
            });
            """,
            additionalTypes: """
            public class Person { public string Name { get; set; } = ""; public SearchLine? Line { get; set; } }
            public class SearchLine { public string Text { get; set; } = ""; }
            """
        );

        Assert.That(model.PropertyJsonNameOverrides, Is.Empty);
    }

    [Test]
    public void Parse_ReferenceJsonName_SplitStatement_TrackedViaLocalVariable()
    {
        var model = ParseConfig(
            """
            builder.ForType<Person>(x =>
            {
                var r = x.Reference(p => p.Line);
                r.JsonName("line");
            });
            """,
            additionalTypes: """
            public class Person { public string Name { get; set; } = ""; public SearchLine? Line { get; set; } }
            public class SearchLine { public string Text { get; set; } = ""; }
            """
        );

        Assert.That(model.PropertyJsonNameOverrides, Has.Count.EqualTo(1));
        Assert.That(model.PropertyJsonNameOverrides["TestApp.Person.Line"], Is.EqualTo("line"));
    }

    [Test]
    public void Parse_ConsecutiveReferences_OnlyOneWithJsonName_OnlyThatOneStored()
    {
        var model = ParseConfig(
            """
            builder.ForType<Person>(x =>
            {
                x.Reference(p => p.Line);
                x.Reference(p => p.Address).JsonName("addr");
            });
            """,
            additionalTypes: """
            public class Person { public string Name { get; set; } = ""; public SearchLine? Line { get; set; } public Address? Address { get; set; } }
            public class SearchLine { public string Text { get; set; } = ""; }
            public class Address { public string Street { get; set; } = ""; }
            """
        );

        Assert.That(model.PropertyJsonNameOverrides, Has.Count.EqualTo(1));
        Assert.That(model.PropertyJsonNameOverrides.ContainsKey("TestApp.Person.Address"), Is.True);
        Assert.That(model.PropertyJsonNameOverrides["TestApp.Person.Address"], Is.EqualTo("addr"));
    }

    [Test]
    public void Parse_ReferenceJsonName_EmptyString_StoredAsEmpty()
    {
        var model = ParseConfig(
            """
            builder.ForType<Person>(x =>
            {
                x.Reference(p => p.Line).JsonName("");
            });
            """,
            additionalTypes: """
            public class Person { public string Name { get; set; } = ""; public SearchLine? Line { get; set; } }
            public class SearchLine { public string Text { get; set; } = ""; }
            """
        );

        Assert.That(model.PropertyJsonNameOverrides, Has.Count.EqualTo(1));
        Assert.That(model.PropertyJsonNameOverrides["TestApp.Person.Line"], Is.EqualTo(""));
    }

    [Test]
    public void Parse_ReferenceJsonName_NonLiteralArg_SilentlySkipped()
    {
        var model = ParseConfig(
            """
            builder.ForType<Person>(x =>
            {
                var name = "line";
                x.Reference(p => p.Line).JsonName(name);
            });
            """,
            additionalTypes: """
            public class Person { public string Name { get; set; } = ""; public SearchLine? Line { get; set; } }
            public class SearchLine { public string Text { get; set; } = ""; }
            """
        );

        Assert.That(model.PropertyJsonNameOverrides, Is.Empty);
    }

    [Test]
    public void Parse_TwoForTypeBlocks_WithReferenceJsonName_BothStoredIsolated()
    {
        var model = ParseConfig(
            """
            builder.ForType<Person>(x =>
            {
                x.Reference(p => p.Line).JsonName("line");
            });
            builder.ForType<Order>(x =>
            {
                x.Reference(p => p.Customer).JsonName("customer");
            });
            """,
            additionalTypes: """
            public class Person { public string Name { get; set; } = ""; public SearchLine? Line { get; set; } }
            public class SearchLine { public string Text { get; set; } = ""; }
            public class Order { public int Id { get; set; } public Person? Customer { get; set; } }
            """
        );

        Assert.That(model.PropertyJsonNameOverrides, Has.Count.EqualTo(2));
        Assert.That(model.PropertyJsonNameOverrides["TestApp.Person.Line"], Is.EqualTo("line"));
        Assert.That(model.PropertyJsonNameOverrides["TestApp.Order.Customer"], Is.EqualTo("customer"));
    }

    // ---- DN1001: Unparsed Config Statement Tests ----

    [Test]
    public void Parse_IfStatementInsideLambda_EmitsDN1001()
    {
        var model = ParseConfig(
            """
            builder.NormalizeGraph<Person>(graph =>
            {
                graph.UseNaming(n =>
                {
                    if (true) { n.DtoSuffix = "Dto"; }
                });
            });
            """,
            additionalTypes: """
            public class Person { public string Name { get; set; } = ""; }
            """
        );

        Assert.That(model.Diagnostics.Where(d => d.Id == "DN1001"), Has.Exactly(1).Items);
    }

    [Test]
    public void Parse_ConsoleWriteLineInsideLambda_EmitsDN1001()
    {
        var model = ParseConfig(
            """
            builder.NormalizeGraph<Person>(graph =>
            {
                graph.UseNaming(n =>
                {
                    System.Console.WriteLine("debug");
                });
            });
            """,
            additionalTypes: """
            public class Person { public string Name { get; set; } = ""; }
            """
        );

        // Console.WriteLine is an invocation on System.Console, not a known receiver.
        // The ExpressionStatementSyntax with InvocationExpression will match the invocation
        // case in ProcessStatements but AnalyzeInvocation returns null (unknown receiver).
        // That currently falls through without emitting a diagnostic.
        // After DN1001, it should be captured by the default case.
        Assert.That(model.Diagnostics.Where(d => d.Id == "DN1001"), Has.Exactly(1).Items);
    }

    [Test]
    public void Parse_ForeachInsideLambda_EmitsDN1001()
    {
        var model = ParseConfig(
            """
            builder.NormalizeGraph<Person>(graph =>
            {
                graph.UseNaming(n =>
                {
                    foreach (var x in new[] { "a" }) { }
                });
            });
            """,
            additionalTypes: """
            public class Person { public string Name { get; set; } = ""; }
            """
        );

        Assert.That(model.Diagnostics.Where(d => d.Id == "DN1001"), Has.Exactly(1).Items);
    }

    [Test]
    public void Parse_ValidStatements_NoDN1001()
    {
        var model = ParseConfig(
            """
            builder.NormalizeGraph<Person>(graph =>
            {
                graph.UseNaming(n =>
                {
                    n.DtoSuffix = "Dto";
                    n.DtoPrefix = "";
                });
            });
            """,
            additionalTypes: """
            public class Person { public string Name { get; set; } = ""; }
            """
        );

        Assert.That(model.Diagnostics.Where(d => d.Id == "DN1001"), Is.Empty);
    }

    [Test]
    public void Parse_UnrecognizedStatementOutsideLambda_NoDN1001()
    {
        // Statements directly in Configure body that don't match are outside any builder lambda.
        // They should NOT produce DN1001 (only statements inside builder lambdas should).
        var model = ParseConfig(
            """
            var x = 42;
            builder.NormalizeGraph<Person>();
            """,
            additionalTypes: """
            public class Person { public string Name { get; set; } = ""; }
            """
        );

        Assert.That(model.Diagnostics.Where(d => d.Id == "DN1001"), Is.Empty);
    }

    [Test]
    public void Parse_DeeplyNestedLambda_EmitsDN1001()
    {
        var model = ParseConfig(
            """
            builder.NormalizeGraph<Person>(graph =>
            {
                graph.UseJsonContract(c =>
                {
                    foreach (var x in new[] { "a" }) { }
                });
            });
            """,
            additionalTypes: """
            public class Person { public string Name { get; set; } = ""; }
            """
        );

        Assert.That(model.Diagnostics.Where(d => d.Id == "DN1001"), Has.Exactly(1).Items);
    }

    [Test]
    public void Parse_MultipleUnparsedStatements_MultipleDN1001()
    {
        var model = ParseConfig(
            """
            builder.NormalizeGraph<Person>(graph =>
            {
                graph.UseNaming(n =>
                {
                    if (true) { }
                    foreach (var x in new[] { "a" }) { }
                    while (false) { }
                });
            });
            """,
            additionalTypes: """
            public class Person { public string Name { get; set; } = ""; }
            """
        );

        Assert.That(model.Diagnostics.Where(d => d.Id == "DN1001"), Has.Exactly(3).Items);
    }

    [Test]
    public void Parse_DuplicateCollectionSameType_StillEmitsDN1002()
    {
        // Verify DN1002 still works after DN1001 changes
        var model = ParseConfig(
            """
            builder.NormalizeGraph<Person>(graph =>
            {
                graph.UseJsonContract(c =>
                {
                    c.Collection<SearchLine>("lines");
                    c.Collection<SearchLine>("search_lines");
                });
            });
            """,
            additionalTypes: """
            public class Person { public string Name { get; set; } = ""; }
            public class SearchLine { public string Text { get; set; } = ""; }
            """
        );

        Assert.That(model.Diagnostics.Where(d => d.Id == "DN1002"), Has.Exactly(1).Items);
        Assert.That(model.Diagnostics.Where(d => d.Id == "DN1001"), Is.Empty);
    }

    [Test]
    public void Parse_AssignmentWithMethodCallRhs_NoDN1001()
    {
        // n.DtoSuffix = GetSuffix() matches AssignmentExpressionSyntax — not unparsed
        var model = ParseConfig(
            """
            builder.NormalizeGraph<Person>(graph =>
            {
                graph.UseNaming(n =>
                {
                    n.DtoSuffix = GetSuffix();
                });
            });
            """,
            additionalTypes: """
            public class Person { public string Name { get; set; } = ""; }
            """,
            extraNamespaceTypes: """
            namespace TestApp
            {
                public static class Helpers
                {
                    public static string GetSuffix() => "Dto";
                }
            }
            """
        );

        // Assignment expressions are recognized even with non-literal RHS
        Assert.That(model.Diagnostics.Where(d => d.Id == "DN1001"), Is.Empty);
    }

    [Test]
    public void Parse_UnparsedStatementInForTypeLambda_EmitsDN1001()
    {
        var model = ParseConfig(
            """
            builder.ForType<Person>(p =>
            {
                if (true) { }
            });
            """,
            additionalTypes: """
            public class Person { public string Name { get; set; } = ""; }
            """
        );

        Assert.That(model.Diagnostics.Where(d => d.Id == "DN1001"), Has.Exactly(1).Items);
    }

    [Test]
    public void Parse_UnparsedStatementInGraphLambda_EmitsDN1001()
    {
        var model = ParseConfig(
            """
            builder.NormalizeGraph<Person>(graph =>
            {
                if (true) { }
            });
            """,
            additionalTypes: """
            public class Person { public string Name { get; set; } = ""; }
            """
        );

        Assert.That(model.Diagnostics.Where(d => d.Id == "DN1001"), Has.Exactly(1).Items);
    }

    [Test]
    public void Parse_DN1001_TruncatesLongStatements()
    {
        var longStatement = new string('x', 200);
        var model = ParseConfig(
            $$"""
            builder.NormalizeGraph<Person>(graph =>
            {
                graph.UseNaming(n =>
                {
                    if ({{longStatement}} == "") { }
                });
            });
            """,
            additionalTypes: """
            public class Person { public string Name { get; set; } = ""; }
            """
        );

        var dn1001 = model.Diagnostics.Where(d => d.Id == "DN1001").ToArray();
        Assert.That(dn1001, Has.Length.EqualTo(1));
        Assert.That(dn1001[0].TypeName.Length, Is.LessThanOrEqualTo(103)); // 100 + "..."
        Assert.That(dn1001[0].TypeName, Does.EndWith("..."));
    }

    [Test]
    public void Parse_UnknownInvocationInsideLambda_EmitsDN1001()
    {
        // An invocation on an unknown receiver inside a builder lambda
        var model = ParseConfig(
            """
            builder.NormalizeGraph<Person>(graph =>
            {
                graph.UseNaming(n =>
                {
                    SomeUnknownMethod();
                });
            });
            """,
            additionalTypes: """
            public class Person { public string Name { get; set; } = ""; }
            """
        );

        Assert.That(model.Diagnostics.Where(d => d.Id == "DN1001"), Has.Exactly(1).Items);
    }

    // ---- Test Helper ----

    private static NormalizationModel ParseConfig(
        string configureBody,
        string additionalTypes,
        string extraNamespaceTypes = ""
    )
    {
        string source;
        if (string.IsNullOrEmpty(extraNamespaceTypes))
        {
            source = $$"""
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
        }
        else
        {
            source = $$"""
                using DataNormalizer.Attributes;
                using DataNormalizer.Configuration;

                namespace TestApp
                {
                    {{additionalTypes}}

                    [NormalizeConfiguration]
                    public partial class TestConfig : NormalizationConfig
                    {
                        protected override void Configure(NormalizeBuilder builder)
                        {
                            {{configureBody}}
                        }
                    }
                }

                {{extraNamespaceTypes}}
                """;
        }

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
