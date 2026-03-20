using System.Linq.Expressions;
using System.Text.Json;
using DataNormalizer.Configuration;
using NUnit.Framework;

namespace DataNormalizer.Tests.Configuration;

[TestFixture]
public sealed class ConfigurationTests
{
    private sealed class TestPerson
    {
        public string Name { get; set; } = "";
        public int Age { get; set; }
        public string? InternalId { get; set; }
    }

    private sealed class TestAddress
    {
        public string Street { get; set; } = "";
    }

    private sealed class TestConfig : NormalizationConfig
    {
        public NormalizeBuilder? CapturedBuilder { get; private set; }

        protected override void Configure(NormalizeBuilder builder)
        {
            CapturedBuilder = builder;
        }

        public void ExecuteConfiguration()
        {
            Configure(new NormalizeBuilder());
        }
    }

    [Test]
    public void NormalizationConfig_IsAbstract()
    {
        Assert.That(typeof(NormalizationConfig).IsAbstract, Is.True);
    }

    [Test]
    public void NormalizationConfig_HasProtectedAbstractConfigureMethod()
    {
        var method = typeof(NormalizationConfig).GetMethod(
            "Configure",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
        );
        Assert.That(method, Is.Not.Null);
        Assert.That(method!.IsAbstract, Is.True);
        Assert.That(method.IsFamily, Is.True);

        var parameters = method.GetParameters();
        Assert.That(parameters, Has.Length.EqualTo(1));
        Assert.That(parameters[0].ParameterType, Is.EqualTo(typeof(NormalizeBuilder)));
    }

    [Test]
    public void TestConfig_CanOverrideConfigure_AndCallBuilderMethods()
    {
        var config = new TestConfig();
        config.ExecuteConfiguration();

        Assert.That(config.CapturedBuilder, Is.Not.Null);
    }

    [Test]
    public void NormalizeBuilder_NormalizeGraph_ReturnsGraphBuilder()
    {
        var builder = new NormalizeBuilder();

        var graphBuilder = builder.NormalizeGraph<TestPerson>();

        Assert.That(graphBuilder, Is.Not.Null);
        Assert.That(graphBuilder, Is.InstanceOf<GraphBuilder<TestPerson>>());
    }

    [Test]
    public void NormalizeBuilder_ForType_ReturnsTypeBuilder()
    {
        var builder = new NormalizeBuilder();

        var typeBuilder = builder.ForType<TestPerson>();

        Assert.That(typeBuilder, Is.Not.Null);
        Assert.That(typeBuilder, Is.InstanceOf<TypeBuilder<TestPerson>>());
    }

    [Test]
    public void GraphBuilder_AutoDiscover_ReturnsSelf()
    {
        var graphBuilder = new NormalizeBuilder().NormalizeGraph<TestPerson>();

        var result = graphBuilder.AutoDiscover();

        Assert.That(result, Is.SameAs(graphBuilder));
    }

    [Test]
    public void GraphBuilder_Inline_ReturnsSelf()
    {
        var graphBuilder = new NormalizeBuilder().NormalizeGraph<TestPerson>();

        var result = graphBuilder.Inline<TestAddress>();

        Assert.That(result, Is.SameAs(graphBuilder));
    }

    [Test]
    public void GraphBuilder_CopySourceAttributes_ReturnsSelf()
    {
        var graphBuilder = new NormalizeBuilder().NormalizeGraph<TestPerson>();

        var result = graphBuilder.CopySourceAttributes();

        Assert.That(result, Is.SameAs(graphBuilder));
    }

    [Test]
    public void GraphBuilder_UsePropertyMode_ReturnsSelf()
    {
        var graphBuilder = new NormalizeBuilder().NormalizeGraph<TestPerson>();

        var result = graphBuilder.UsePropertyMode(PropertyMode.ExplicitOnly);

        Assert.That(result, Is.SameAs(graphBuilder));
    }

    [Test]
    public void TypeBuilder_IgnoreProperty_ReturnsSelf()
    {
        var typeBuilder = new NormalizeBuilder().ForType<TestPerson>();

        var result = typeBuilder.IgnoreProperty(p => p.InternalId);

        Assert.That(result, Is.SameAs(typeBuilder));
    }

    [Test]
    public void TypeBuilder_NormalizeProperty_ReturnsSelf()
    {
        var typeBuilder = new NormalizeBuilder().ForType<TestPerson>();

        var result = typeBuilder.NormalizeProperty(p => p.Name);

        Assert.That(result, Is.SameAs(typeBuilder));
    }

    [Test]
    public void TypeBuilder_InlineProperty_ReturnsSelf()
    {
        var typeBuilder = new NormalizeBuilder().ForType<TestPerson>();

        var result = typeBuilder.InlineProperty(p => p.Name);

        Assert.That(result, Is.SameAs(typeBuilder));
    }

    [Test]
    public void TypeBuilder_IncludeProperty_ReturnsSelf()
    {
        var typeBuilder = new NormalizeBuilder().ForType<TestPerson>();

        var result = typeBuilder.IncludeProperty(p => p.Name);

        Assert.That(result, Is.SameAs(typeBuilder));
    }

    [Test]
    public void TypeBuilder_WithName_ReturnsSelf()
    {
        var typeBuilder = new NormalizeBuilder().ForType<TestPerson>();

        var result = typeBuilder.WithName("PersonDto");

        Assert.That(result, Is.SameAs(typeBuilder));
    }

    [Test]
    public void TypeBuilder_UsePropertyMode_ReturnsSelf()
    {
        var typeBuilder = new NormalizeBuilder().ForType<TestPerson>();

        var result = typeBuilder.UsePropertyMode(PropertyMode.ExplicitOnly);

        Assert.That(result, Is.SameAs(typeBuilder));
    }

    [Test]
    public void FluentChaining_TypeBuilder_ReturnsTypeBuilder()
    {
        var builder = new NormalizeBuilder();

        var result = builder
            .ForType<TestPerson>()
            .IgnoreProperty(p => p.InternalId)
            .WithName("PersonDto")
            .UsePropertyMode(PropertyMode.ExplicitOnly);

        Assert.That(result, Is.InstanceOf<TypeBuilder<TestPerson>>());
    }

    [Test]
    public void NormalizeBuilder_NormalizeGraphWithLambda_ReturnsGraphBuilder()
    {
        var builder = new NormalizeBuilder();
        var lambdaCalled = false;

        var result = builder.NormalizeGraph<TestPerson>(graph =>
        {
            lambdaCalled = true;
            graph.AutoDiscover().CopySourceAttributes();
        });

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.InstanceOf<GraphBuilder<TestPerson>>());
        Assert.That(lambdaCalled, Is.True);
    }

    [Test]
    public void GraphBuilder_ForType_ReturnsTypeBuilder()
    {
        var graphBuilder = new NormalizeBuilder().NormalizeGraph<TestPerson>();

        TypeBuilder<TestPerson>? capturedBuilder = null;
        var result = graphBuilder.ForType<TestPerson>(tb =>
        {
            capturedBuilder = tb;
        });

        Assert.That(result, Is.SameAs(graphBuilder));
        Assert.That(capturedBuilder, Is.Not.Null);
        Assert.That(capturedBuilder, Is.InstanceOf<TypeBuilder<TestPerson>>());
    }

    [Test]
    public void GraphBuilder_ForType_WithConfiguration_ExecutesAction()
    {
        var graphBuilder = new NormalizeBuilder().NormalizeGraph<TestPerson>();
        var actionCalled = false;

        graphBuilder.ForType<TestPerson>(tb =>
        {
            actionCalled = true;
            tb.IgnoreProperty(p => p.InternalId);
        });

        Assert.That(actionCalled, Is.True);
    }

    [Test]
    public void GraphBuilder_UseJsonNaming_ReturnsSelf()
    {
        var graphBuilder = new NormalizeBuilder().NormalizeGraph<TestPerson>();

        var result = graphBuilder.UseJsonNaming(System.Text.Json.JsonNamingPolicy.CamelCase);

        Assert.That(result, Is.SameAs(graphBuilder));
    }

    [Test]
    public void GraphBuilder_UseReferenceTrackingForCycles_ReturnsSelf()
    {
        var graphBuilder = new NormalizeBuilder().NormalizeGraph<TestPerson>();

        var result = graphBuilder.UseReferenceTrackingForCycles();

        Assert.That(result, Is.SameAs(graphBuilder));
    }

    [Test]
    public void GraphBuilder_FullChain_WithForTypeAndJsonNaming()
    {
        var graphBuilder = new NormalizeBuilder().NormalizeGraph<TestPerson>();

        var result = graphBuilder
            .AutoDiscover()
            .CopySourceAttributes()
            .UseJsonNaming(System.Text.Json.JsonNamingPolicy.CamelCase)
            .ForType<TestPerson>(p =>
            {
                p.IgnoreProperty(x => x.InternalId);
            });

        Assert.That(result, Is.SameAs(graphBuilder));
    }
}
