using System.Collections.Immutable;
using DataNormalizer.Generators.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DataNormalizer.Generators.Analysis;

internal static class ConfigurationParser
{
    public static NormalizationModel Parse(ClassDeclarationSyntax configClass, SemanticModel semanticModel)
    {
        var classSymbol = semanticModel.GetDeclaredSymbol(configClass) as INamedTypeSymbol;
        var configClassName = classSymbol?.Name ?? configClass.Identifier.Text;
        var configNamespace = classSymbol?.ContainingNamespace is { IsGlobalNamespace: false } ns
            ? ns.ToDisplayString()
            : "";

        // Find the Configure method
        var configureMethod = configClass
            .Members.OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => m.Identifier.Text == "Configure");

        if (configureMethod?.Body is null)
            return new NormalizationModel { ConfigClassName = configClassName, ConfigNamespace = configNamespace };

        var context = new ParseContext(semanticModel);

        // Find the builder parameter name
        var builderParam = configureMethod.ParameterList.Parameters.FirstOrDefault();
        var builderParamName = builderParam?.Identifier.Text ?? "builder";

        // Register the builder parameter as a known receiver
        context.ReceiverMap[builderParamName] = ReceiverKind.NormalizeBuilder;

        ProcessStatements(configureMethod.Body.Statements, context);

        // If we have root types via NormalizeGraph, set autoDiscover
        var autoDiscover = context.RootTypes.Count > 0;

        return new NormalizationModel
        {
            ConfigClassName = configClassName,
            ConfigNamespace = configNamespace,
            RootTypes = context.RootTypes.ToImmutable(),
            TypeConfigurations = context.TypeConfigs.ToImmutableDictionary(),
            InlinedTypes = context.InlinedTypes.ToImmutable(),
            ExplicitTypes = context.ExplicitTypes.ToImmutable(),
            CopySourceAttributes = context.CopySourceAttributes,
            JsonNamingPolicy = context.JsonNamingPolicy,
            AutoDiscover = autoDiscover,
            UseReferenceTrackingForCycles = context.UseReferenceTrackingForCycles,
        };
    }

    private static void ProcessStatements(SyntaxList<StatementSyntax> statements, ParseContext context)
    {
        foreach (var statement in statements)
        {
            switch (statement)
            {
                case LocalDeclarationStatementSyntax localDecl:
                    ProcessLocalDeclaration(localDecl, context);
                    break;

                case ExpressionStatementSyntax exprStmt when exprStmt.Expression is InvocationExpressionSyntax inv:
                    ProcessTopLevelInvocation(inv, context);
                    break;
            }
        }
    }

    private static void ProcessLocalDeclaration(LocalDeclarationStatementSyntax localDecl, ParseContext context)
    {
        foreach (var variable in localDecl.Declaration.Variables)
        {
            if (variable.Initializer?.Value is not InvocationExpressionSyntax invocation)
                continue;

            var varName = variable.Identifier.Text;

            // Process the invocation and figure out what kind of builder it returns
            var result = AnalyzeInvocation(invocation, context);
            if (result is not null)
            {
                // Map the variable to the same receiver kind as what the call returns
                context.ReceiverMap[varName] = result.Value;
            }
        }
    }

    private static void ProcessTopLevelInvocation(InvocationExpressionSyntax invocation, ParseContext context)
    {
        AnalyzeInvocation(invocation, context);
    }

    /// <summary>
    /// Analyzes an invocation expression, processing its effects and returning
    /// the <see cref="ReceiverKind"/> of the result (for variable assignment tracking).
    /// </summary>
    private static ReceiverKind? AnalyzeInvocation(InvocationExpressionSyntax invocation, ParseContext context)
    {
        // Handle chained calls: p.IgnoreProperty(x => x.A).IgnoreProperty(x => x.B)
        // Process the inner invocation first if the receiver is another invocation.
        if (
            invocation.Expression is MemberAccessExpressionSyntax outerAccess
            && outerAccess.Expression is InvocationExpressionSyntax innerInvocation
        )
        {
            AnalyzeInvocation(innerInvocation, context);
        }

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
            return null;

        var methodName = GetMethodName(memberAccess);
        var receiverName = GetUltimateReceiverName(memberAccess);

        if (methodName is null || receiverName is null)
            return null;

        // Check if the receiver is a known builder/receiver
        if (!context.ReceiverMap.TryGetValue(receiverName, out var receiverKind))
            return null;

        switch (methodName)
        {
            case "NormalizeGraph" when receiverKind == ReceiverKind.NormalizeBuilder:
                return ProcessNormalizeGraph(invocation, memberAccess, context);

            case "ForType" when receiverKind is ReceiverKind.NormalizeBuilder or ReceiverKind.GraphBuilder:
                ProcessForType(invocation, memberAccess, context);
                return receiverKind; // ForType on builder returns builder, on graph returns graph

            case "Inline" when receiverKind == ReceiverKind.GraphBuilder:
                ProcessInline(memberAccess, context);
                return ReceiverKind.GraphBuilder;

            case "CopySourceAttributes" when receiverKind == ReceiverKind.GraphBuilder:
                context.CopySourceAttributes = true;
                return ReceiverKind.GraphBuilder;

            case "UseJsonNaming" when receiverKind == ReceiverKind.GraphBuilder:
                context.JsonNamingPolicy = "CamelCase";
                return ReceiverKind.GraphBuilder;

            case "UseReferenceTrackingForCycles" when receiverKind == ReceiverKind.GraphBuilder:
                context.UseReferenceTrackingForCycles = true;
                return ReceiverKind.GraphBuilder;

            case "IgnoreProperty" when receiverKind == ReceiverKind.TypeBuilder:
                ProcessPropertyAction(invocation, receiverName, "Ignore", context);
                return ReceiverKind.TypeBuilder;

            case "IncludeProperty" when receiverKind == ReceiverKind.TypeBuilder:
                ProcessPropertyAction(invocation, receiverName, "Include", context);
                return ReceiverKind.TypeBuilder;

            case "NormalizeProperty" when receiverKind == ReceiverKind.TypeBuilder:
                ProcessPropertyAction(invocation, receiverName, "Normalize", context);
                return ReceiverKind.TypeBuilder;

            case "InlineProperty" when receiverKind == ReceiverKind.TypeBuilder:
                ProcessPropertyAction(invocation, receiverName, "Inline", context);
                return ReceiverKind.TypeBuilder;

            case "WithName" when receiverKind == ReceiverKind.TypeBuilder:
                ProcessWithName(invocation, receiverName, context);
                return ReceiverKind.TypeBuilder;

            default:
                return null;
        }
    }

    private static ReceiverKind ProcessNormalizeGraph(
        InvocationExpressionSyntax invocation,
        MemberAccessExpressionSyntax memberAccess,
        ParseContext context
    )
    {
        var typeSymbol = GetTypeArgumentSymbol(memberAccess, context.SemanticModel);
        if (typeSymbol is not null)
        {
            var fqn = NormalizeFqn(typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
            context.RootTypes.Add(new RootTypeInfo { TypeSymbol = typeSymbol, FullyQualifiedName = fqn });
        }

        // Process the lambda argument if present (graph => { ... })
        ProcessGraphLambdaArgument(invocation, context);

        return ReceiverKind.GraphBuilder;
    }

    private static void ProcessForType(
        InvocationExpressionSyntax invocation,
        MemberAccessExpressionSyntax memberAccess,
        ParseContext context
    )
    {
        var typeSymbol = GetTypeArgumentSymbol(memberAccess, context.SemanticModel);
        if (typeSymbol is null)
            return;

        var fqn = NormalizeFqn(typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));

        context.ExplicitTypes.Add(fqn);
        EnsureTypeConfig(fqn, context);

        // Process the lambda argument if present (p => { ... })
        if (invocation.ArgumentList.Arguments.Count > 0)
        {
            var lambdaArg = invocation.ArgumentList.Arguments[0].Expression;
            var lambdaParamName = GetLambdaParameterName(lambdaArg);

            if (lambdaParamName is not null)
            {
                // Register this lambda parameter as a TypeBuilder for this type
                context.ReceiverMap[lambdaParamName] = ReceiverKind.TypeBuilder;
                context.TypeBuilderMap[lambdaParamName] = fqn;

                var body = GetLambdaBody(lambdaArg);
                if (body is BlockSyntax block)
                {
                    ProcessStatements(block.Statements, context);
                }
            }
        }
    }

    private static void ProcessInline(MemberAccessExpressionSyntax memberAccess, ParseContext context)
    {
        var typeSymbol = GetTypeArgumentSymbol(memberAccess, context.SemanticModel);
        if (typeSymbol is null)
            return;

        var fqn = NormalizeFqn(typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
        context.InlinedTypes.Add(fqn);
    }

    private static void ProcessPropertyAction(
        InvocationExpressionSyntax invocation,
        string receiverName,
        string actionKind,
        ParseContext context
    )
    {
        // Look up what type this receiver is associated with
        if (!context.TypeBuilderMap.TryGetValue(receiverName, out var typeFqn))
            return;

        // Extract property name from lambda argument
        var propertyName = ExtractPropertyNameFromLambdaArg(invocation);
        if (propertyName is null)
            return;

        EnsureTypeConfig(typeFqn, context);
        var existing = context.TypeConfigs[typeFqn];

        context.TypeConfigs[typeFqn] = actionKind switch
        {
            "Ignore" => new TypeConfiguration
            {
                FullyQualifiedName = existing.FullyQualifiedName,
                IgnoredProperties = existing.IgnoredProperties.Add(propertyName),
                IncludedProperties = existing.IncludedProperties,
                NormalizedProperties = existing.NormalizedProperties,
                InlinedProperties = existing.InlinedProperties,
                CustomName = existing.CustomName,
                PropertyMode = existing.PropertyMode,
            },
            "Include" => new TypeConfiguration
            {
                FullyQualifiedName = existing.FullyQualifiedName,
                IgnoredProperties = existing.IgnoredProperties,
                IncludedProperties = existing.IncludedProperties.Add(propertyName),
                NormalizedProperties = existing.NormalizedProperties,
                InlinedProperties = existing.InlinedProperties,
                CustomName = existing.CustomName,
                PropertyMode = existing.PropertyMode,
            },
            "Normalize" => new TypeConfiguration
            {
                FullyQualifiedName = existing.FullyQualifiedName,
                IgnoredProperties = existing.IgnoredProperties,
                IncludedProperties = existing.IncludedProperties,
                NormalizedProperties = existing.NormalizedProperties.Add(propertyName),
                InlinedProperties = existing.InlinedProperties,
                CustomName = existing.CustomName,
                PropertyMode = existing.PropertyMode,
            },
            "Inline" => new TypeConfiguration
            {
                FullyQualifiedName = existing.FullyQualifiedName,
                IgnoredProperties = existing.IgnoredProperties,
                IncludedProperties = existing.IncludedProperties,
                NormalizedProperties = existing.NormalizedProperties,
                InlinedProperties = existing.InlinedProperties.Add(propertyName),
                CustomName = existing.CustomName,
                PropertyMode = existing.PropertyMode,
            },
            _ => existing,
        };
    }

    private static void ProcessWithName(
        InvocationExpressionSyntax invocation,
        string receiverName,
        ParseContext context
    )
    {
        if (!context.TypeBuilderMap.TryGetValue(receiverName, out var typeFqn))
            return;

        // Extract the string argument from WithName("SomeName")
        if (invocation.ArgumentList.Arguments.Count == 0)
            return;

        var argExpr = invocation.ArgumentList.Arguments[0].Expression;
        if (argExpr is not LiteralExpressionSyntax literal)
            return;

        var customName = literal.Token.ValueText;
        if (string.IsNullOrEmpty(customName))
            return;

        EnsureTypeConfig(typeFqn, context);
        var existing = context.TypeConfigs[typeFqn];
        context.TypeConfigs[typeFqn] = new TypeConfiguration
        {
            FullyQualifiedName = existing.FullyQualifiedName,
            IgnoredProperties = existing.IgnoredProperties,
            IncludedProperties = existing.IncludedProperties,
            NormalizedProperties = existing.NormalizedProperties,
            InlinedProperties = existing.InlinedProperties,
            CustomName = customName,
            PropertyMode = existing.PropertyMode,
        };
    }

    private static void ProcessGraphLambdaArgument(InvocationExpressionSyntax invocation, ParseContext context)
    {
        if (invocation.ArgumentList.Arguments.Count == 0)
            return;

        var lambdaArg = invocation.ArgumentList.Arguments[0].Expression;
        var lambdaParamName = GetLambdaParameterName(lambdaArg);
        if (lambdaParamName is null)
            return;

        // Map the lambda parameter as a GraphBuilder
        context.ReceiverMap[lambdaParamName] = ReceiverKind.GraphBuilder;

        var body = GetLambdaBody(lambdaArg);
        if (body is BlockSyntax block)
        {
            ProcessStatements(block.Statements, context);
        }
    }

    private static INamedTypeSymbol? GetTypeArgumentSymbol(
        MemberAccessExpressionSyntax memberAccess,
        SemanticModel semanticModel
    )
    {
        if (memberAccess.Name is not GenericNameSyntax genericName)
            return null;

        if (genericName.TypeArgumentList.Arguments.Count == 0)
            return null;

        var typeArgSyntax = genericName.TypeArgumentList.Arguments[0];
        var typeInfo = semanticModel.GetTypeInfo(typeArgSyntax);

        return typeInfo.Type as INamedTypeSymbol;
    }

    private static string? GetMethodName(MemberAccessExpressionSyntax memberAccess)
    {
        return memberAccess.Name switch
        {
            GenericNameSyntax genericName => genericName.Identifier.Text,
            IdentifierNameSyntax identifierName => identifierName.Identifier.Text,
            _ => null,
        };
    }

    /// <summary>
    /// Gets the ultimate receiver name by walking through chained invocations.
    /// For <c>p.IgnoreProperty(x => x.A).IgnoreProperty(x => x.B)</c>,
    /// the ultimate receiver is <c>p</c>.
    /// </summary>
    private static string? GetUltimateReceiverName(MemberAccessExpressionSyntax memberAccess)
    {
        return memberAccess.Expression switch
        {
            IdentifierNameSyntax id => id.Identifier.Text,
            InvocationExpressionSyntax inv when inv.Expression is MemberAccessExpressionSyntax innerAccess =>
                GetUltimateReceiverName(innerAccess),
            _ => null,
        };
    }

    private static string? GetLambdaParameterName(ExpressionSyntax? expression)
    {
        return expression switch
        {
            SimpleLambdaExpressionSyntax simpleLambda => simpleLambda.Parameter.Identifier.Text,
            ParenthesizedLambdaExpressionSyntax parenLambda when parenLambda.ParameterList.Parameters.Count > 0 =>
                parenLambda.ParameterList.Parameters[0].Identifier.Text,
            _ => null,
        };
    }

    private static SyntaxNode? GetLambdaBody(ExpressionSyntax? expression)
    {
        return expression switch
        {
            SimpleLambdaExpressionSyntax simpleLambda => simpleLambda.Body,
            ParenthesizedLambdaExpressionSyntax parenLambda => parenLambda.Body,
            _ => null,
        };
    }

    private static string? ExtractPropertyNameFromLambdaArg(InvocationExpressionSyntax invocation)
    {
        if (invocation.ArgumentList.Arguments.Count == 0)
            return null;

        var argExpr = invocation.ArgumentList.Arguments[0].Expression;
        var body = GetLambdaBody(argExpr);

        // The body should be a MemberAccessExpression (x => x.PropertyName)
        return body switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.Text,
            _ => null,
        };
    }

    private static string NormalizeFqn(string fqn)
    {
        return fqn.StartsWith("global::", StringComparison.Ordinal) ? fqn.Substring("global::".Length) : fqn;
    }

    private static void EnsureTypeConfig(string typeFqn, ParseContext context)
    {
        if (!context.TypeConfigs.ContainsKey(typeFqn))
        {
            context.TypeConfigs[typeFqn] = new TypeConfiguration { FullyQualifiedName = typeFqn };
        }
    }

    private enum ReceiverKind
    {
        NormalizeBuilder,
        GraphBuilder,
        TypeBuilder,
    }

    private sealed class ParseContext(SemanticModel semanticModel)
    {
        public SemanticModel SemanticModel { get; } = semanticModel;

        public ImmutableArray<RootTypeInfo>.Builder RootTypes { get; } = ImmutableArray.CreateBuilder<RootTypeInfo>();

        public Dictionary<string, TypeConfiguration> TypeConfigs { get; } = new();

        public ImmutableHashSet<string>.Builder InlinedTypes { get; } = ImmutableHashSet.CreateBuilder<string>();

        public ImmutableHashSet<string>.Builder ExplicitTypes { get; } = ImmutableHashSet.CreateBuilder<string>();

        public bool CopySourceAttributes { get; set; }

        public string? JsonNamingPolicy { get; set; }

        public bool UseReferenceTrackingForCycles { get; set; }

        /// <summary>
        /// Maps variable/parameter names to their receiver kind.
        /// </summary>
        public Dictionary<string, ReceiverKind> ReceiverMap { get; } = new();

        /// <summary>
        /// Maps TypeBuilder lambda parameter names to the fully-qualified type name they configure.
        /// </summary>
        public Dictionary<string, string> TypeBuilderMap { get; } = new();
    }
}
