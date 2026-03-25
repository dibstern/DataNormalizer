using System.Reflection;
using DataNormalizer.Attributes;
using DataNormalizer.Configuration;
using NUnit.Framework;

namespace DataNormalizer.Tests.Attributes;

[TestFixture]
public sealed class AttributeTests
{
    [Test]
    public void NormalizeConfigurationAttribute_TargetsClass()
    {
        var usage = typeof(NormalizeConfigurationAttribute).GetCustomAttribute<AttributeUsageAttribute>();
        Assert.That(usage, Is.Not.Null);
        Assert.That(usage!.ValidOn, Is.EqualTo(AttributeTargets.Class));
    }

    [Test]
    public void NormalizeConfigurationAttribute_IsNotInherited()
    {
        var usage = typeof(NormalizeConfigurationAttribute).GetCustomAttribute<AttributeUsageAttribute>();
        Assert.That(usage!.Inherited, Is.False);
    }

    [Test]
    public void NormalizeConfigurationAttribute_DoesNotAllowMultiple()
    {
        var usage = typeof(NormalizeConfigurationAttribute).GetCustomAttribute<AttributeUsageAttribute>();
        Assert.That(usage!.AllowMultiple, Is.False);
    }

    [Test]
    public void NormalizeIgnoreAttribute_TargetsProperty()
    {
        var usage = typeof(NormalizeIgnoreAttribute).GetCustomAttribute<AttributeUsageAttribute>();
        Assert.That(usage, Is.Not.Null);
        Assert.That(usage!.ValidOn, Is.EqualTo(AttributeTargets.Property));
    }

    [Test]
    public void NormalizeIncludeAttribute_TargetsProperty()
    {
        var usage = typeof(NormalizeIncludeAttribute).GetCustomAttribute<AttributeUsageAttribute>();
        Assert.That(usage, Is.Not.Null);
        Assert.That(usage!.ValidOn, Is.EqualTo(AttributeTargets.Property));
    }

    [Test]
    public void NormalizeConfigurationAttribute_IsSealed()
    {
        Assert.That(typeof(NormalizeConfigurationAttribute).IsSealed, Is.True);
    }

    [Test]
    public void NormalizeIgnoreAttribute_IsSealed()
    {
        Assert.That(typeof(NormalizeIgnoreAttribute).IsSealed, Is.True);
    }

    [Test]
    public void NormalizeIncludeAttribute_IsSealed()
    {
        Assert.That(typeof(NormalizeIncludeAttribute).IsSealed, Is.True);
    }

    [Test]
    public void PropertyMode_HasExpectedValues()
    {
        Assert.That(Enum.GetValues<PropertyMode>(), Has.Length.EqualTo(2));
        Assert.That((int)PropertyMode.IncludeAll, Is.EqualTo(0));
        Assert.That((int)PropertyMode.ExplicitOnly, Is.EqualTo(1));
    }

    [Test]
    public void PropertyMode_DefaultIsIncludeAll()
    {
        Assert.That(default(PropertyMode), Is.EqualTo(PropertyMode.IncludeAll));
    }

    [Test]
    public void NormalizeIgnoreAttribute_IsNotInherited()
    {
        var usage = typeof(NormalizeIgnoreAttribute).GetCustomAttribute<AttributeUsageAttribute>();
        Assert.That(usage!.Inherited, Is.False);
    }

    [Test]
    public void NormalizeIgnoreAttribute_DoesNotAllowMultiple()
    {
        var usage = typeof(NormalizeIgnoreAttribute).GetCustomAttribute<AttributeUsageAttribute>();
        Assert.That(usage!.AllowMultiple, Is.False);
    }

    [Test]
    public void NormalizeIncludeAttribute_IsNotInherited()
    {
        var usage = typeof(NormalizeIncludeAttribute).GetCustomAttribute<AttributeUsageAttribute>();
        Assert.That(usage!.Inherited, Is.False);
    }

    [Test]
    public void NormalizeIncludeAttribute_DoesNotAllowMultiple()
    {
        var usage = typeof(NormalizeIncludeAttribute).GetCustomAttribute<AttributeUsageAttribute>();
        Assert.That(usage!.AllowMultiple, Is.False);
    }

    [Test]
    public void NormalizeJsonNameAttribute_StoresName()
    {
        var attr = new NormalizeJsonNameAttribute("custom_name");

        Assert.That(attr.Name, Is.EqualTo("custom_name"));
    }

    [Test]
    public void NormalizeJsonNameAttribute_Name_ReturnsConstructorValue()
    {
        var name = "my_json_property";
        var attr = new NormalizeJsonNameAttribute(name);

        Assert.That(attr.Name, Is.EqualTo(name));
    }

    [Test]
    public void NormalizeJsonNameAttribute_TargetsProperty()
    {
        var usage = typeof(NormalizeJsonNameAttribute).GetCustomAttribute<AttributeUsageAttribute>();
        Assert.That(usage, Is.Not.Null);
        Assert.That(usage!.ValidOn, Is.EqualTo(AttributeTargets.Property));
    }

    [Test]
    public void NormalizeJsonNameAttribute_IsNotInherited()
    {
        var usage = typeof(NormalizeJsonNameAttribute).GetCustomAttribute<AttributeUsageAttribute>();
        Assert.That(usage!.Inherited, Is.False);
    }

    [Test]
    public void NormalizeJsonNameAttribute_IsSealed()
    {
        Assert.That(typeof(NormalizeJsonNameAttribute).IsSealed, Is.True);
    }

    [Test]
    public void NormalizeJsonNameAttribute_DoesNotAllowMultiple()
    {
        var usage = typeof(NormalizeJsonNameAttribute).GetCustomAttribute<AttributeUsageAttribute>();
        Assert.That(usage!.AllowMultiple, Is.False);
    }
}
