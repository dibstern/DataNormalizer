using System.Collections.Immutable;
using System.Linq;
using DataNormalizer.Generators.Models;
using Microsoft.CodeAnalysis;

namespace DataNormalizer.Generators.Analysis;

internal static class TypeGraphAnalyzer
{
    public static IReadOnlyList<TypeGraphNode> Analyze(
        INamedTypeSymbol rootType,
        ImmutableHashSet<string> inlinedTypes,
        ImmutableHashSet<string> explicitTypes,
        ImmutableDictionary<string, TypeConfiguration> typeConfigurations,
        bool autoDiscover
    )
    {
        var visited = new HashSet<string>();
        var dfsPath = new List<string>();
        var inProgressSet = new HashSet<string>();
        var cycleParticipants = new HashSet<string>();
        var results = new List<TypeGraphNode>();

        AnalyzeType(
            rootType,
            inlinedTypes,
            explicitTypes,
            typeConfigurations,
            autoDiscover,
            visited,
            dfsPath,
            inProgressSet,
            cycleParticipants,
            results,
            isRoot: true
        );

        return results;
    }

    private static void AnalyzeType(
        INamedTypeSymbol type,
        ImmutableHashSet<string> inlinedTypes,
        ImmutableHashSet<string> explicitTypes,
        ImmutableDictionary<string, TypeConfiguration> typeConfigurations,
        bool autoDiscover,
        HashSet<string> visited,
        List<string> dfsPath,
        HashSet<string> inProgressSet,
        HashSet<string> cycleParticipants,
        List<TypeGraphNode> results,
        bool isRoot
    )
    {
        var typeFullName = GetFullyQualifiedName(type);

        if (visited.Contains(typeFullName))
            return;

        if (inProgressSet.Contains(typeFullName))
            return;

        dfsPath.Add(typeFullName);
        inProgressSet.Add(typeFullName);

        // Get type config if it exists
        typeConfigurations.TryGetValue(typeFullName, out var typeConfig);

        var allProperties = GetAllPublicProperties(type);

        // Filter properties based on TypeConfiguration
        var properties = FilterProperties(allProperties, typeConfig);
        var analyzedProperties = ImmutableArray.CreateBuilder<AnalyzedProperty>();
        var hasCircularReference = false;

        foreach (var prop in properties)
        {
            var propType = prop.Type;
            var propTypeFullName = GetFullyQualifiedName(propType);
            var isNullable = false;

            // Unwrap Nullable<T> for classification
            var unwrappedType = UnwrapNullable(propType, out isNullable);
            var unwrappedFullName = GetFullyQualifiedName(unwrappedType);

            // Check if this is a collection of complex type
            var collectionElementType = TryGetCollectionElementType(propType, unwrappedType);
            if (collectionElementType != null)
            {
                var elementFullName = GetFullyQualifiedName(collectionElementType);

                if (IsSimpleType(collectionElementType))
                {
                    // Collection of simple type (e.g., List<string>) — treat as Simple
                    analyzedProperties.Add(
                        new AnalyzedProperty
                        {
                            Name = prop.Name,
                            TypeFullName = propTypeFullName,
                            Kind = PropertyKind.Simple,
                            IsNullable = isNullable,
                            IsCollection = false,
                            IsReferenceType = unwrappedType.IsReferenceType,
                            SourceAttributes = GetPropertyAttributes(prop),
                        }
                    );
                    continue;
                }

                // Collection of complex type
                var collectionKind = ClassifyCollectionKind(unwrappedType);
                var isCircularRef = inProgressSet.Contains(elementFullName);
                if (isCircularRef)
                {
                    hasCircularReference = true;
                    var targetIdx = dfsPath.IndexOf(elementFullName);
                    for (var j = targetIdx; j < dfsPath.Count; j++)
                        cycleParticipants.Add(dfsPath[j]);
                    analyzedProperties.Add(
                        new AnalyzedProperty
                        {
                            Name = prop.Name,
                            TypeFullName = propTypeFullName,
                            Kind = PropertyKind.Collection,
                            IsNullable = isNullable,
                            IsCollection = true,
                            CollectionElementTypeFullName = elementFullName,
                            IsCircularReference = true,
                            CollectionKind = collectionKind,
                            IsReferenceType = unwrappedType.IsReferenceType,
                            SourceAttributes = GetPropertyAttributes(prop),
                        }
                    );
                    continue;
                }

                // Recurse into element type if it should be normalized
                if (collectionElementType is INamedTypeSymbol elementNamedType)
                {
                    if (ShouldRecurse(elementFullName, inlinedTypes, explicitTypes, autoDiscover))
                    {
                        AnalyzeType(
                            elementNamedType,
                            inlinedTypes,
                            explicitTypes,
                            typeConfigurations,
                            autoDiscover,
                            visited,
                            dfsPath,
                            inProgressSet,
                            cycleParticipants,
                            results,
                            isRoot: false
                        );
                    }
                }

                analyzedProperties.Add(
                    new AnalyzedProperty
                    {
                        Name = prop.Name,
                        TypeFullName = propTypeFullName,
                        Kind = PropertyKind.Collection,
                        IsNullable = isNullable,
                        IsCollection = true,
                        CollectionElementTypeFullName = elementFullName,
                        CollectionKind = collectionKind,
                        IsReferenceType = unwrappedType.IsReferenceType,
                        SourceAttributes = GetPropertyAttributes(prop),
                    }
                );
                continue;
            }

            // Not a collection — check if simple
            if (IsSimpleType(unwrappedType))
            {
                analyzedProperties.Add(
                    new AnalyzedProperty
                    {
                        Name = prop.Name,
                        TypeFullName = propTypeFullName,
                        Kind = PropertyKind.Simple,
                        IsNullable = isNullable,
                        IsCollection = false,
                        IsReferenceType = unwrappedType.IsReferenceType,
                        SourceAttributes = GetPropertyAttributes(prop),
                    }
                );
                continue;
            }

            // Complex type — check for circular reference
            if (inProgressSet.Contains(unwrappedFullName))
            {
                hasCircularReference = true;
                var targetIdx = dfsPath.IndexOf(unwrappedFullName);
                for (var j = targetIdx; j < dfsPath.Count; j++)
                    cycleParticipants.Add(dfsPath[j]);
                analyzedProperties.Add(
                    new AnalyzedProperty
                    {
                        Name = prop.Name,
                        TypeFullName = propTypeFullName,
                        Kind = PropertyKind.Normalized,
                        IsNullable = isNullable,
                        IsCollection = false,
                        IsCircularReference = true,
                        IsReferenceType = unwrappedType.IsReferenceType,
                        SourceAttributes = GetPropertyAttributes(prop),
                    }
                );
                continue;
            }

            // Check if inlined
            if (inlinedTypes.Contains(unwrappedFullName))
            {
                analyzedProperties.Add(
                    new AnalyzedProperty
                    {
                        Name = prop.Name,
                        TypeFullName = propTypeFullName,
                        Kind = PropertyKind.Inlined,
                        IsNullable = isNullable,
                        IsCollection = false,
                        IsReferenceType = unwrappedType.IsReferenceType,
                        SourceAttributes = GetPropertyAttributes(prop),
                    }
                );
                continue;
            }

            // Check if should recurse (autoDiscover or explicit)
            if (unwrappedType is INamedTypeSymbol namedPropType)
            {
                if (ShouldRecurse(unwrappedFullName, inlinedTypes, explicitTypes, autoDiscover))
                {
                    AnalyzeType(
                        namedPropType,
                        inlinedTypes,
                        explicitTypes,
                        typeConfigurations,
                        autoDiscover,
                        visited,
                        dfsPath,
                        inProgressSet,
                        cycleParticipants,
                        results,
                        isRoot: false
                    );
                    analyzedProperties.Add(
                        new AnalyzedProperty
                        {
                            Name = prop.Name,
                            TypeFullName = propTypeFullName,
                            Kind = PropertyKind.Normalized,
                            IsNullable = isNullable,
                            IsCollection = false,
                            IsReferenceType = unwrappedType.IsReferenceType,
                            SourceAttributes = GetPropertyAttributes(prop),
                        }
                    );
                }
                else
                {
                    // Not auto-discovered and not explicit — treat as inlined
                    analyzedProperties.Add(
                        new AnalyzedProperty
                        {
                            Name = prop.Name,
                            TypeFullName = propTypeFullName,
                            Kind = PropertyKind.Inlined,
                            IsNullable = isNullable,
                            IsCollection = false,
                            IsReferenceType = unwrappedType.IsReferenceType,
                            SourceAttributes = GetPropertyAttributes(prop),
                        }
                    );
                }
            }
            else
            {
                // Fallback — treat as simple
                analyzedProperties.Add(
                    new AnalyzedProperty
                    {
                        Name = prop.Name,
                        TypeFullName = propTypeFullName,
                        Kind = PropertyKind.Simple,
                        IsNullable = isNullable,
                        IsCollection = false,
                        IsReferenceType = unwrappedType.IsReferenceType,
                        SourceAttributes = GetPropertyAttributes(prop),
                    }
                );
            }
        }

        dfsPath.RemoveAt(dfsPath.Count - 1);
        inProgressSet.Remove(typeFullName);
        visited.Add(typeFullName);

        var nodeHasCircular = hasCircularReference || cycleParticipants.Contains(typeFullName);
        results.Add(
            new TypeGraphNode
            {
                TypeFullName = typeFullName,
                TypeName = type.Name,
                Properties = analyzedProperties.ToImmutable(),
                HasCircularReference = nodeHasCircular,
            }
        );
    }

    private static bool ShouldRecurse(
        string typeFullName,
        ImmutableHashSet<string> inlinedTypes,
        ImmutableHashSet<string> explicitTypes,
        bool autoDiscover
    )
    {
        if (inlinedTypes.Contains(typeFullName))
            return false;

        if (autoDiscover)
            return true;

        return explicitTypes.Contains(typeFullName);
    }

    private static ITypeSymbol UnwrapNullable(ITypeSymbol type, out bool isNullable)
    {
        if (
            type is INamedTypeSymbol namedType
            && namedType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T
            && namedType.TypeArguments.Length == 1
        )
        {
            isNullable = true;
            return namedType.TypeArguments[0];
        }

        // Also check NRT annotation for reference types
        isNullable = type.NullableAnnotation == NullableAnnotation.Annotated;
        return type;
    }

    private static bool IsSimpleType(ITypeSymbol type)
    {
        // Enums
        if (type.TypeKind == TypeKind.Enum)
            return true;

        // Built-in special types
        switch (type.SpecialType)
        {
            case SpecialType.System_Boolean:
            case SpecialType.System_Byte:
            case SpecialType.System_SByte:
            case SpecialType.System_Int16:
            case SpecialType.System_UInt16:
            case SpecialType.System_Int32:
            case SpecialType.System_UInt32:
            case SpecialType.System_Int64:
            case SpecialType.System_UInt64:
            case SpecialType.System_Single:
            case SpecialType.System_Double:
            case SpecialType.System_Decimal:
            case SpecialType.System_Char:
            case SpecialType.System_String:
            case SpecialType.System_Object:
                return true;
        }

        // Check by fully-qualified name for well-known types
        var fullName = GetFullyQualifiedName(type);
        switch (fullName)
        {
            case "System.DateTime":
            case "System.DateTimeOffset":
            case "System.TimeSpan":
            case "System.Guid":
            case "System.DateOnly":
            case "System.TimeOnly":
            case "System.Uri":
                return true;
        }

        // byte[] is simple
        if (IsByteArray(type))
            return true;

        // Dictionary<K,V> is treated as simple
        if (IsDictionary(type))
            return true;

        // Value types that aren't enums and aren't recognized above — treat as simple
        // (covers structs like user-defined value types)
        if (type.IsValueType)
            return true;

        return false;
    }

    private static bool IsByteArray(ITypeSymbol type)
    {
        return type is IArrayTypeSymbol arrayType && arrayType.ElementType.SpecialType == SpecialType.System_Byte;
    }

    private static bool IsDictionary(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol namedType)
        {
            var originalDef = namedType.OriginalDefinition;
            var name = GetFullyQualifiedName(originalDef);
            if (
                name == "System.Collections.Generic.Dictionary<TKey, TValue>"
                || name == "System.Collections.Generic.IDictionary<TKey, TValue>"
                || name == "System.Collections.Generic.IReadOnlyDictionary<TKey, TValue>"
            )
            {
                return true;
            }
        }

        // Check interfaces
        foreach (var iface in type.AllInterfaces)
        {
            var ifaceName = GetFullyQualifiedName(iface.OriginalDefinition);
            if (
                ifaceName == "System.Collections.Generic.IDictionary<TKey, TValue>"
                || ifaceName == "System.Collections.Generic.IReadOnlyDictionary<TKey, TValue>"
            )
            {
                return true;
            }
        }

        return false;
    }

    private static ITypeSymbol? TryGetCollectionElementType(ITypeSymbol originalType, ITypeSymbol unwrappedType)
    {
        // Exclude string (implements IEnumerable<char>)
        if (unwrappedType.SpecialType == SpecialType.System_String)
            return null;

        // Exclude byte[]
        if (IsByteArray(unwrappedType))
            return null;

        // Exclude Dictionary
        if (IsDictionary(unwrappedType))
            return null;

        // Array — T[]
        if (unwrappedType is IArrayTypeSymbol arrayType)
            return arrayType.ElementType;

        // IEnumerable<T> from interfaces
        if (unwrappedType is INamedTypeSymbol namedType)
        {
            // Check if the type itself is IEnumerable<T>
            var enumerableElement = GetIEnumerableElementType(namedType);
            if (enumerableElement != null)
                return enumerableElement;

            // Check interfaces
            foreach (var iface in namedType.AllInterfaces)
            {
                enumerableElement = GetIEnumerableElementType(iface);
                if (enumerableElement != null)
                    return enumerableElement;
            }
        }

        return null;
    }

    private static ITypeSymbol? GetIEnumerableElementType(INamedTypeSymbol type)
    {
        var originalDef = type.OriginalDefinition;
        var name = GetFullyQualifiedName(originalDef);
        if (name == "System.Collections.Generic.IEnumerable<T>" && type.TypeArguments.Length == 1)
        {
            return type.TypeArguments[0];
        }

        return null;
    }

    private static List<IPropertySymbol> FilterProperties(
        List<IPropertySymbol> allProperties,
        TypeConfiguration? typeConfig
    )
    {
        if (typeConfig is null)
            return allProperties;

        // ExplicitOnly mode: only include properties in IncludedProperties
        if (typeConfig.PropertyMode == GeneratorPropertyMode.ExplicitOnly)
        {
            return allProperties.Where(p => typeConfig.IncludedProperties.Contains(p.Name)).ToList();
        }

        // IncludeAll mode (default): exclude ignored properties
        return allProperties.Where(p => !typeConfig.IgnoredProperties.Contains(p.Name)).ToList();
    }

    private static List<IPropertySymbol> GetAllPublicProperties(INamedTypeSymbol type)
    {
        var props = new List<IPropertySymbol>();
        var current = type;
        while (current != null)
        {
            foreach (var member in current.GetMembers())
            {
                if (
                    member is IPropertySymbol prop
                    && prop.DeclaredAccessibility == Accessibility.Public
                    && !prop.IsStatic
                    && !prop.IsIndexer
                    && prop.GetMethod != null
                )
                {
                    props.Add(prop);
                }
            }

            current = current.BaseType;
        }

        return props;
    }

    private static CollectionTypeKind ClassifyCollectionKind(ITypeSymbol type)
    {
        // Array — T[]
        if (type is IArrayTypeSymbol)
            return CollectionTypeKind.Array;

        if (type is INamedTypeSymbol namedType)
        {
            var originalDef = namedType.OriginalDefinition;
            var defName = GetFullyQualifiedName(originalDef);

            // Check concrete type name first
            switch (defName)
            {
                case "System.Collections.Generic.List<T>":
                    return CollectionTypeKind.List;
                case "System.Collections.Generic.HashSet<T>":
                    return CollectionTypeKind.HashSet;
                case "System.Collections.Immutable.ImmutableList<T>":
                    return CollectionTypeKind.ImmutableList;
                case "System.Collections.Immutable.ImmutableArray<T>":
                    return CollectionTypeKind.ImmutableArray;
                case "System.Collections.Generic.IList<T>":
                    return CollectionTypeKind.IList;
                case "System.Collections.Generic.ICollection<T>":
                    return CollectionTypeKind.ICollection;
                case "System.Collections.Generic.IReadOnlyList<T>":
                    return CollectionTypeKind.IReadOnlyList;
                case "System.Collections.Generic.IEnumerable<T>":
                    return CollectionTypeKind.IEnumerable;
            }
        }

        return CollectionTypeKind.None;
    }

    private static ImmutableArray<string> GetPropertyAttributes(IPropertySymbol prop)
    {
        var attrs = ImmutableArray.CreateBuilder<string>();
        foreach (var attrData in prop.GetAttributes())
        {
            // Skip compiler-generated attributes
            var ns = attrData.AttributeClass?.ContainingNamespace?.ToDisplayString() ?? "";
            if (ns.StartsWith("System.Runtime.CompilerServices"))
                continue;

            var syntax = attrData.ApplicationSyntaxReference?.GetSyntax()?.ToFullString()?.Trim();
            if (!string.IsNullOrEmpty(syntax))
                attrs.Add(syntax!);
        }

        return attrs.ToImmutable();
    }

    private static string GetFullyQualifiedName(ITypeSymbol type)
    {
        var display = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        if (display.StartsWith("global::"))
            return display.Substring("global::".Length);
        return display;
    }
}
