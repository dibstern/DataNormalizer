using System.Collections.Immutable;
using DataNormalizer.Generators.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
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
            AutoDiscover = autoDiscover,
            UseReferenceTrackingForCycles = context.UseReferenceTrackingForCycles,
            Naming = new NamingModel
            {
                DtoPrefix = context.GraphDtoPrefix ?? context.GlobalDtoPrefix,
                DtoSuffix = context.GraphDtoSuffix ?? context.GlobalDtoSuffix,
                ContainerSuffix = context.GraphContainerSuffix ?? context.GlobalContainerSuffix,
                EmitJsonPropertyNames =
                    context.GraphEmitJsonPropertyNames ?? context.GlobalEmitJsonPropertyNames,
            },
            JsonContract = new JsonContractModel
            {
                RootPropertyName = context.RootPropertyName,
                CollectionJsonNames = context.CollectionJsonNames.ToImmutableDictionary(),
            },
            Diagnostics = context.Diagnostics.ToImmutableArray(),
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

                case ExpressionStatementSyntax exprStmt
                    when exprStmt.Expression is AssignmentExpressionSyntax assignment:
                    ProcessAssignment(assignment, context);
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

            case "UseNaming" when receiverKind == ReceiverKind.NormalizeBuilder:
                ProcessUseNamingLambda(invocation, context, isGraph: false);
                return ReceiverKind.NormalizeBuilder;

            case "UseNaming" when receiverKind == ReceiverKind.GraphBuilder:
                ProcessUseNamingLambda(invocation, context, isGraph: true);
                return ReceiverKind.GraphBuilder;

            case "UseJsonContract" when receiverKind == ReceiverKind.GraphBuilder:
                ProcessJsonContractLambda(invocation, context);
                return ReceiverKind.GraphBuilder;

            case "UseJsonNaming" when receiverKind == ReceiverKind.GraphBuilder:
                context.GlobalEmitJsonPropertyNames = true;
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

            case "Collection" when receiverKind == ReceiverKind.JsonContractBuilder:
            {
                var typeFqn = GetTypeArgumentSymbol(memberAccess, context.SemanticModel);
                var jsonName = ExtractStringArgument(invocation);
                if (typeFqn != null && jsonName != null)
                {
                    var normalizedFqn = NormalizeFqn(
                        typeFqn.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                    );
                    if (!context.SeenCollectionTypes.Add(normalizedFqn))
                    {
                        context.Diagnostics.Add(new GeneratorDiagnosticInfo("DN1002", normalizedFqn));
                    }
                    context.CollectionJsonNames[normalizedFqn] = jsonName;
                }
                return ReceiverKind.JsonContractBuilder;
            }

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

    private static void ProcessUseNamingLambda(
        InvocationExpressionSyntax invocation,
        ParseContext context,
        bool isGraph
    )
    {
        if (invocation.ArgumentList.Arguments.Count == 0)
            return;

        var lambdaArg = invocation.ArgumentList.Arguments[0].Expression;
        var lambdaParamName = GetLambdaParameterName(lambdaArg);
        if (lambdaParamName is null)
            return;

        context.ReceiverMap[lambdaParamName] = ReceiverKind.NamingBuilder;
        context.IsParsingGraphNaming = isGraph;

        try
        {
            var body = GetLambdaBody(lambdaArg);
            if (body is BlockSyntax block)
            {
                ProcessStatements(block.Statements, context);
            }
        }
        finally
        {
            context.IsParsingGraphNaming = false;
        }
    }

    private static void ProcessJsonContractLambda(InvocationExpressionSyntax invocation, ParseContext context)
    {
        if (invocation.ArgumentList.Arguments.Count == 0)
            return;

        var lambdaArg = invocation.ArgumentList.Arguments[0].Expression;
        var lambdaParamName = GetLambdaParameterName(lambdaArg);
        if (lambdaParamName is null)
            return;

        context.ReceiverMap[lambdaParamName] = ReceiverKind.JsonContractBuilder;

        var body = GetLambdaBody(lambdaArg);
        if (body is BlockSyntax block)
        {
            ProcessStatements(block.Statements, context);
        }
    }

    private static void ProcessAssignment(AssignmentExpressionSyntax assignment, ParseContext context)
    {
        // Extract receiver and property name from left-hand side: n.DtoSuffix
        if (assignment.Left is not MemberAccessExpressionSyntax memberAccess)
            return;

        if (memberAccess.Expression is not IdentifierNameSyntax receiverId)
            return;

        var receiverName = receiverId.Identifier.Text;
        if (!context.ReceiverMap.TryGetValue(receiverName, out var receiverKind))
            return;

        var propertyName = memberAccess.Name.Identifier.Text;

        switch (receiverKind)
        {
            case ReceiverKind.NamingBuilder:
                switch (propertyName)
                {
                    case "DtoPrefix":
                    case "DtoSuffix":
                    case "ContainerSuffix":
                        if (assignment.Right is not LiteralExpressionSyntax stringLiteral)
                            return;
                        var stringValue = stringLiteral.Token.ValueText;
                        SetNamingStringProperty(propertyName, stringValue, context);
                        break;

                    case "EmitJsonPropertyNames":
                        if (assignment.Right.IsKind(SyntaxKind.TrueLiteralExpression))
                            SetNamingBoolProperty(propertyName, true, context);
                        else if (assignment.Right.IsKind(SyntaxKind.FalseLiteralExpression))
                            SetNamingBoolProperty(propertyName, false, context);
                        // Non-literal RHS: silently ignored
                        break;
                }
                break;

            case ReceiverKind.JsonContractBuilder:
                if (
                    propertyName == "RootPropertyName"
                    && assignment.Right is LiteralExpressionSyntax rootLit
                    && rootLit.IsKind(SyntaxKind.StringLiteralExpression)
                )
                    context.RootPropertyName = rootLit.Token.ValueText;
                break;

            default:
                return;
        }
    }

    private static void SetNamingStringProperty(string propertyName, string value, ParseContext context)
    {
        if (context.IsParsingGraphNaming)
        {
            switch (propertyName)
            {
                case "DtoPrefix":
                    context.GraphDtoPrefix = value;
                    break;
                case "DtoSuffix":
                    context.GraphDtoSuffix = value;
                    break;
                case "ContainerSuffix":
                    context.GraphContainerSuffix = value;
                    break;
            }
        }
        else
        {
            switch (propertyName)
            {
                case "DtoPrefix":
                    context.GlobalDtoPrefix = value;
                    break;
                case "DtoSuffix":
                    context.GlobalDtoSuffix = value;
                    break;
                case "ContainerSuffix":
                    context.GlobalContainerSuffix = value;
                    break;
            }
        }
    }

    private static void SetNamingBoolProperty(string propertyName, bool value, ParseContext context)
    {
        if (propertyName != "EmitJsonPropertyNames")
            return;

        if (context.IsParsingGraphNaming)
            context.GraphEmitJsonPropertyNames = value;
        else
            context.GlobalEmitJsonPropertyNames = value;
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

    private static string? ExtractStringArgument(InvocationExpressionSyntax invocation)
    {
        var args = invocation.ArgumentList.Arguments;
        if (
            args.Count > 0
            && args[0].Expression is LiteralExpressionSyntax literal
            && literal.IsKind(SyntaxKind.StringLiteralExpression)
        )
            return literal.Token.ValueText;
        return null;
    }

    private enum ReceiverKind
    {
        NormalizeBuilder,
        GraphBuilder,
        TypeBuilder,
        NamingBuilder,
        JsonContractBuilder,
    }

    private sealed class ParseContext(SemanticModel semanticModel)
    {
        public SemanticModel SemanticModel { get; } = semanticModel;

        public ImmutableArray<RootTypeInfo>.Builder RootTypes { get; } = ImmutableArray.CreateBuilder<RootTypeInfo>();

        public Dictionary<string, TypeConfiguration> TypeConfigs { get; } = new();

        public ImmutableHashSet<string>.Builder InlinedTypes { get; } = ImmutableHashSet.CreateBuilder<string>();

        public ImmutableHashSet<string>.Builder ExplicitTypes { get; } = ImmutableHashSet.CreateBuilder<string>();

        public bool CopySourceAttributes { get; set; }

        public bool UseReferenceTrackingForCycles { get; set; }

        // Global naming values (mutable during parse)
        public string GlobalDtoPrefix { get; set; } = "";
        public string GlobalDtoSuffix { get; set; } = "Dto";
        public string GlobalContainerSuffix { get; set; } = "Dto";
        public bool GlobalEmitJsonPropertyNames { get; set; } = true;

        // Per-graph overrides (null = not set, use global).
        // NOTE: These are never cleared between multiple NormalizeGraph calls. Phase 1 assumes
        // a single graph per config. When per-graph naming is supported, these will need to be
        // reset (or moved to a per-graph structure) before processing each graph lambda.
        public string? GraphDtoPrefix { get; set; }
        public string? GraphDtoSuffix { get; set; }
        public string? GraphContainerSuffix { get; set; }
        public bool? GraphEmitJsonPropertyNames { get; set; }

        // Track whether current NamingBuilder lambda is global or graph-level
        public bool IsParsingGraphNaming { get; set; }

        /// <summary>
        /// Maps variable/parameter names to their receiver kind.
        /// </summary>
        public Dictionary<string, ReceiverKind> ReceiverMap { get; } = new();

        /// <summary>
        /// Maps TypeBuilder lambda parameter names to the fully-qualified type name they configure.
        /// </summary>
        public Dictionary<string, string> TypeBuilderMap { get; } = new();

        // JsonContract fields
        public string? RootPropertyName { get; set; }
        public Dictionary<string, string> CollectionJsonNames { get; } = new();
        public HashSet<string> SeenCollectionTypes { get; } = new();

        // Diagnostics collected during parse
        public List<GeneratorDiagnosticInfo> Diagnostics { get; } = new();
    }
}
