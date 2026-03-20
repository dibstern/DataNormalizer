using System.Collections.Immutable;
using DataNormalizer.Generators.Models;

namespace DataNormalizer.Generators.Tests.TestUtilities;

internal static class ModelFactories
{
    public static TypeGraphNode CreateNode(string fullName, string name, params AnalyzedProperty[] props)
    {
        return new TypeGraphNode
        {
            TypeFullName = fullName,
            TypeName = name,
            Properties = props.ToImmutableArray(),
        };
    }

    public static TypeGraphNode CreateNode(
        string fullName,
        string name,
        bool hasCircularReference,
        params AnalyzedProperty[] props
    )
    {
        return new TypeGraphNode
        {
            TypeFullName = fullName,
            TypeName = name,
            Properties = props.ToImmutableArray(),
            HasCircularReference = hasCircularReference,
        };
    }

    public static TypeGraphNode CreateFlatNode(string fullName, string name, params string[] simpleProps)
    {
        var props = simpleProps.Select(p => SimpleProp(p, "string", isRef: true)).ToArray();
        return CreateNode(fullName, name, props);
    }

    public static AnalyzedProperty SimpleProp(string name, string type, bool isRef)
    {
        return new AnalyzedProperty
        {
            Name = name,
            TypeFullName = type,
            Kind = PropertyKind.Simple,
            IsReferenceType = isRef,
        };
    }

    public static AnalyzedProperty NormalizedProp(string name, string type, bool nullable, bool isCircular = false)
    {
        return new AnalyzedProperty
        {
            Name = name,
            TypeFullName = type,
            Kind = PropertyKind.Normalized,
            IsNullable = nullable,
            IsReferenceType = true,
            IsCircularReference = isCircular,
        };
    }

    public static AnalyzedProperty CollectionProp(
        string name,
        string type,
        string elementType,
        CollectionTypeKind kind = CollectionTypeKind.List,
        bool isCircular = false
    )
    {
        return new AnalyzedProperty
        {
            Name = name,
            TypeFullName = type,
            Kind = PropertyKind.Collection,
            IsCollection = true,
            CollectionElementTypeFullName = elementType,
            CollectionKind = kind,
            IsReferenceType = true,
            IsCircularReference = isCircular,
        };
    }

    public static AnalyzedProperty InlinedProp(string name, string type, bool isRef)
    {
        return new AnalyzedProperty
        {
            Name = name,
            TypeFullName = type,
            Kind = PropertyKind.Inlined,
            IsReferenceType = isRef,
        };
    }

    public static NormalizationModel CreateModel(
        string configClassName,
        string configNamespace,
        string rootTypeFullyQualifiedName
    )
    {
        return new NormalizationModel
        {
            ConfigClassName = configClassName,
            ConfigNamespace = configNamespace,
            RootTypes = ImmutableArray.Create(
                new RootTypeInfo { TypeSymbol = null!, FullyQualifiedName = rootTypeFullyQualifiedName }
            ),
            AutoDiscover = true,
        };
    }

    public static int CountOccurrences(string text, string pattern)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(pattern, index, System.StringComparison.Ordinal)) != -1)
        {
            count++;
            index += pattern.Length;
        }
        return count;
    }
}
