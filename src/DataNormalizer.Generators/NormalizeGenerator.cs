using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
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
        var configs = context
            .SyntaxProvider.ForAttributeWithMetadataName(
                "DataNormalizer.Attributes.NormalizeConfigurationAttribute",
                predicate: static (node, _) => node is ClassDeclarationSyntax,
                transform: static (ctx, ct) => TransformConfig(ctx, ct)
            )
            .Where(static result => result is not null)
            .Select(static (result, _) => result!.Value);

        context.RegisterSourceOutput(configs, static (spc, output) => Emit(spc, output));
    }

    private static GeneratorOutput? TransformConfig(GeneratorAttributeSyntaxContext ctx, CancellationToken ct)
    {
        if (ctx.TargetNode is not ClassDeclarationSyntax classDecl)
            return null;

        if (ctx.TargetSymbol is not INamedTypeSymbol symbol)
            return null;

        var isPartial = classDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword));
        var className = symbol.Name;
        var ns = symbol.ContainingNamespace.IsGlobalNamespace ? "" : symbol.ContainingNamespace.ToDisplayString();

        if (!isPartial)
        {
            return new GeneratorOutput(
                className,
                ns,
                IsPartial: false,
                Sources: ImmutableArray<GeneratorSourceEntry>.Empty,
                Diagnostics: ImmutableArray<GeneratorDiagnosticInfo>.Empty
            );
        }

        // Parse configuration — SemanticModel is available from the context
        var semanticModel = ctx.SemanticModel;
        var model = ConfigurationParser.Parse(classDecl, semanticModel);

        if (model.RootTypes.Length == 0)
        {
            return new GeneratorOutput(
                className,
                ns,
                IsPartial: true,
                Sources: ImmutableArray<GeneratorSourceEntry>.Empty,
                Diagnostics: ImmutableArray<GeneratorDiagnosticInfo>.Empty
            );
        }

        // Analyze type graphs for all root types, deduplicate nodes
        var allNodes = new List<TypeGraphNode>();
        var emittedTypes = new HashSet<string>();

        foreach (var rootType in model.RootTypes)
        {
            ct.ThrowIfCancellationRequested();

            var nodes = TypeGraphAnalyzer.Analyze(
                rootType.TypeSymbol,
                model.InlinedTypes,
                model.ExplicitTypes,
                model.TypeConfigurations,
                model.AutoDiscover,
                model.CopySourceAttributes
            );

            foreach (var node in nodes)
            {
                if (emittedTypes.Add(node.TypeFullName))
                {
                    allNodes.Add(node);
                }
            }
        }

        // Generate all source strings and collect diagnostics
        var sources = ImmutableArray.CreateBuilder<GeneratorSourceEntry>();
        var diagnostics = ImmutableArray.CreateBuilder<GeneratorDiagnosticInfo>();

        // Report diagnostics for nodes
        foreach (var node in allNodes)
        {
            if (node.HasCircularReference)
            {
                diagnostics.Add(new GeneratorDiagnosticInfo("DN0001", node.TypeName));
            }

            if (node.Properties.Length == 0)
            {
                diagnostics.Add(new GeneratorDiagnosticInfo("DN0003", node.TypeName));
            }
        }

        // Emit DTO classes (one file per type, scoped by config class to avoid hint name collisions)
        foreach (var node in allNodes)
        {
            ct.ThrowIfCancellationRequested();

            if (node.Properties.Length == 0)
                continue;

            var dtoSource = DtoEmitter.Emit(node, model.CopySourceAttributes, model.JsonNamingPolicy);
            var dtoHintPrefix = string.IsNullOrEmpty(model.ConfigNamespace)
                ? model.ConfigClassName
                : $"{model.ConfigNamespace}.{model.ConfigClassName}";
            var hintName = string.IsNullOrEmpty(EmitterHelpers.GetNamespace(node.TypeFullName))
                ? $"{dtoHintPrefix}.Normalized{node.TypeName}.g.cs"
                : $"{dtoHintPrefix}.{EmitterHelpers.GetNamespace(node.TypeFullName)}.Normalized{node.TypeName}.g.cs";
            sources.Add(new GeneratorSourceEntry(hintName, dtoSource));
        }

        // Emit Normalizer partial class
        var normalizerSource = NormalizerEmitter.Emit(model, allNodes);
        var normalizerHint = string.IsNullOrEmpty(model.ConfigNamespace)
            ? $"{model.ConfigClassName}.Normalizer.g.cs"
            : $"{model.ConfigNamespace}.{model.ConfigClassName}.Normalizer.g.cs";
        sources.Add(new GeneratorSourceEntry(normalizerHint, normalizerSource));

        // Emit Denormalizer partial class
        var denormalizerSource = DenormalizerEmitter.Emit(model, allNodes);
        var denormalizerHint = string.IsNullOrEmpty(model.ConfigNamespace)
            ? $"{model.ConfigClassName}.Denormalizer.g.cs"
            : $"{model.ConfigNamespace}.{model.ConfigClassName}.Denormalizer.g.cs";
        sources.Add(new GeneratorSourceEntry(denormalizerHint, denormalizerSource));

        return new GeneratorOutput(
            className,
            ns,
            IsPartial: true,
            Sources: sources.ToImmutable(),
            Diagnostics: diagnostics.ToImmutable()
        );
    }

    private static void Emit(SourceProductionContext spc, GeneratorOutput output)
    {
        if (!output.IsPartial)
        {
            spc.ReportDiagnostic(
                Diagnostic.Create(DiagnosticDescriptors.ConfigClassMustBePartial, Location.None, output.ClassName)
            );
            return;
        }

        foreach (var diag in output.Diagnostics)
        {
            var descriptor = diag.Id switch
            {
                "DN0001" => DiagnosticDescriptors.CircularReference,
                "DN0003" => DiagnosticDescriptors.NoPublicProperties,
                _ => DiagnosticDescriptors.ConfigClassMustBePartial,
            };
            spc.ReportDiagnostic(Diagnostic.Create(descriptor, Location.None, diag.TypeName));
        }

        foreach (var entry in output.Sources)
        {
            spc.AddSource(entry.HintName, entry.Source);
        }
    }
}

internal readonly record struct GeneratorSourceEntry(string HintName, string Source);

internal readonly record struct GeneratorDiagnosticInfo(string Id, string TypeName);

internal readonly record struct GeneratorOutput(
    string ClassName,
    string Namespace,
    bool IsPartial,
    ImmutableArray<GeneratorSourceEntry> Sources,
    ImmutableArray<GeneratorDiagnosticInfo> Diagnostics
);
