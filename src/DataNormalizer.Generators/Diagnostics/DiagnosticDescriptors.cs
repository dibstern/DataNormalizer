using Microsoft.CodeAnalysis;

namespace DataNormalizer.Generators.Diagnostics;

internal static class DiagnosticDescriptors
{
    public static readonly DiagnosticDescriptor CircularReference = new(
        id: "DN0001",
        title: "Circular reference detected",
        messageFormat: "Circular reference detected: {0}",
        category: "DataNormalizer",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor ConfigClassMustBePartial = new(
        id: "DN0002",
        title: "Configuration class must be partial",
        messageFormat: "Configuration class '{0}' must be declared as partial",
        category: "DataNormalizer",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor NoPublicProperties = new(
        id: "DN0003",
        title: "Type has no public properties",
        messageFormat: "Type '{0}' has no public properties to normalize",
        category: "DataNormalizer",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor UnmappedComplexType = new(
        id: "DN0004",
        title: "Unmapped complex type will be inlined",
        messageFormat: "Complex type '{0}' is not in the normalization graph and will be inlined",
        category: "DataNormalizer",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor UnparsedConfigStatement = new(
        id: "DN1001",
        title: "Unparsed configuration statement",
        messageFormat: "Configuration statement could not be parsed and will be ignored: '{0}'",
        category: "DataNormalizer",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor DuplicateCollectionType = new(
        id: "DN1002",
        title: "Duplicate Collection<T> type",
        messageFormat: "Collection<{0}> was already configured in this UseJsonContract block. Only the last value will be used.",
        category: "DataNormalizer",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );
}
