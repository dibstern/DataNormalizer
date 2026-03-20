using System.Collections.Generic;
using DataNormalizer.Generators.Analysis;
using DataNormalizer.Generators.Diagnostics;
using DataNormalizer.Generators.Emitters;
using DataNormalizer.Generators.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DataNormalizer.Generators;

[Generator(LanguageNames.CSharp)]
public sealed class NormalizeGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var configClasses = context
            .SyntaxProvider.ForAttributeWithMetadataName(
                "DataNormalizer.Attributes.NormalizeConfigurationAttribute",
                predicate: static (node, _) => node is ClassDeclarationSyntax,
                transform: static (ctx, _) => ExtractConfigInfo(ctx)
            )
            .Where(static info => info is not null)
            .Select(static (info, _) => info!.Value);

        context.RegisterSourceOutput(
            configClasses.Combine(context.CompilationProvider),
            static (spc, pair) => Execute(spc, pair.Left, pair.Right)
        );
    }

    private static ConfigInfo? ExtractConfigInfo(GeneratorAttributeSyntaxContext ctx)
    {
        if (ctx.TargetNode is not ClassDeclarationSyntax classDecl)
            return null;

        if (ctx.TargetSymbol is not INamedTypeSymbol symbol)
            return null;

        var isPartial = classDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword));

        // Find the Configure method
        SyntaxReference? configureMethodRef = null;
        foreach (var member in symbol.GetMembers("Configure"))
        {
            if (
                member is IMethodSymbol method
                && method.Parameters.Length == 1
                && method.Parameters[0].Type.Name == "NormalizeBuilder"
            )
            {
                configureMethodRef = method.DeclaringSyntaxReferences.FirstOrDefault();
                break;
            }
        }

        return new ConfigInfo(
            ClassName: symbol.Name,
            FullyQualifiedName: symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            Namespace: symbol.ContainingNamespace.IsGlobalNamespace ? "" : symbol.ContainingNamespace.ToDisplayString(),
            IsPartial: isPartial,
            Location: classDecl.GetLocation(),
            ConfigureMethodReference: configureMethodRef,
            ClassSyntaxReference: symbol.DeclaringSyntaxReferences.FirstOrDefault()
        );
    }

    private static void Execute(SourceProductionContext spc, ConfigInfo config, Compilation compilation)
    {
        if (!config.IsPartial)
        {
            spc.ReportDiagnostic(
                Diagnostic.Create(DiagnosticDescriptors.ConfigClassMustBePartial, config.Location, config.ClassName)
            );
            return;
        }

        // 1. Get the class declaration syntax from ClassSyntaxReference
        if (config.ClassSyntaxReference is null)
            return;

        var classSyntax = config.ClassSyntaxReference.GetSyntax() as ClassDeclarationSyntax;
        if (classSyntax is null)
            return;

        var semanticModel = compilation.GetSemanticModel(classSyntax.SyntaxTree);

        // 2. Parse configuration
        var model = ConfigurationParser.Parse(classSyntax, semanticModel);

        if (model.RootTypes.Length == 0)
            return; // Empty Configure body — nothing to generate

        // 3. Analyze type graphs for all root types, deduplicate nodes
        var allNodes = new List<TypeGraphNode>();
        var emittedTypes = new HashSet<string>();

        foreach (var rootType in model.RootTypes)
        {
            var nodes = TypeGraphAnalyzer.Analyze(
                rootType.TypeSymbol,
                model.InlinedTypes,
                model.ExplicitTypes,
                model.TypeConfigurations,
                model.AutoDiscover
            );

            foreach (var node in nodes)
            {
                if (emittedTypes.Add(node.TypeFullName))
                {
                    allNodes.Add(node);
                }
            }
        }

        // 4. Report diagnostics
        foreach (var node in allNodes)
        {
            if (node.HasCircularReference)
            {
                spc.ReportDiagnostic(
                    Diagnostic.Create(DiagnosticDescriptors.CircularReference, config.Location, node.TypeName)
                );
            }

            if (node.Properties.Length == 0)
            {
                spc.ReportDiagnostic(
                    Diagnostic.Create(DiagnosticDescriptors.NoPublicProperties, config.Location, node.TypeName)
                );
            }
        }

        // 5. Emit DTO classes (one file per type, scoped by config class to avoid hint name collisions)
        foreach (var node in allNodes)
        {
            if (node.Properties.Length == 0)
                continue; // Skip types with no properties

            var dtoSource = DtoEmitter.Emit(node, model.CopySourceAttributes, model.JsonNamingPolicy);
            var dtoHintPrefix = string.IsNullOrEmpty(model.ConfigNamespace)
                ? model.ConfigClassName
                : $"{model.ConfigNamespace}.{model.ConfigClassName}";
            var hintName = string.IsNullOrEmpty(GetNamespace(node.TypeFullName))
                ? $"{dtoHintPrefix}.Normalized{node.TypeName}.g.cs"
                : $"{dtoHintPrefix}.{GetNamespace(node.TypeFullName)}.Normalized{node.TypeName}.g.cs";
            spc.AddSource(hintName, dtoSource);
        }

        // 6. Emit Normalizer partial class
        var normalizerSource = NormalizerEmitter.Emit(model, allNodes);
        var normalizerHint = string.IsNullOrEmpty(model.ConfigNamespace)
            ? $"{model.ConfigClassName}.Normalizer.g.cs"
            : $"{model.ConfigNamespace}.{model.ConfigClassName}.Normalizer.g.cs";
        spc.AddSource(normalizerHint, normalizerSource);

        // 7. Emit Denormalizer partial class
        var denormalizerSource = DenormalizerEmitter.Emit(model, allNodes);
        var denormalizerHint = string.IsNullOrEmpty(model.ConfigNamespace)
            ? $"{model.ConfigClassName}.Denormalizer.g.cs"
            : $"{model.ConfigNamespace}.{model.ConfigClassName}.Denormalizer.g.cs";
        spc.AddSource(denormalizerHint, denormalizerSource);
    }

    private static string GetNamespace(string typeFullName)
    {
        var lastDot = typeFullName.LastIndexOf('.');
        return lastDot > 0 ? typeFullName.Substring(0, lastDot) : "";
    }
}

internal readonly record struct ConfigInfo(
    string ClassName,
    string FullyQualifiedName,
    string Namespace,
    bool IsPartial,
    Location Location,
    SyntaxReference? ConfigureMethodReference,
    SyntaxReference? ClassSyntaxReference
);
