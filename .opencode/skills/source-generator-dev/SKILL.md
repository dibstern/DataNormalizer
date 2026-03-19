---
name: source-generator-dev
description: Writing Roslyn incremental source generators for the DataNormalizer project. CRITICAL rules on IIncrementalGenerator, ForAttributeWithMetadataName, never storing SemanticModel/SyntaxNode in pipeline data, equatable models, Verify.SourceGenerators testing, and common pitfalls for type analysis.
---

# Roslyn Incremental Source Generator Development

## Fundamental Rule: IIncrementalGenerator Only

Always use `IIncrementalGenerator`, NEVER `ISourceGenerator`. The older API is deprecated and has severe performance issues (runs on every keystroke).

```csharp
[Generator(LanguageNames.CSharp)]
public sealed class NormalizeGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Pipeline setup here
    }
}
```

## CRITICAL: Never Store SemanticModel, SyntaxNode, or Compilation in Pipeline Data

This is the most important rule for incremental generators. Pipeline data must be **equatable** for incremental caching to work. `SemanticModel`, `SyntaxNode`, and `Compilation` are not equatable and will break caching, causing the generator to re-run on every keystroke.

### What to Store

Only store equatable primitives in pipeline models:
- `string`, `bool`, `int`, other value types
- `record` / `record struct` (auto-generates `Equals`/`GetHashCode`)
- `ImmutableArray<T>` where `T` is equatable
- `Location` (equatable)
- `SyntaxReference` (equatable — use this to get back to syntax later)

### What NEVER to Store

- `SemanticModel` — NOT equatable
- `SyntaxNode` / `ClassDeclarationSyntax` / etc. — NOT equatable
- `Compilation` — NOT equatable
- `ISymbol` / `INamedTypeSymbol` / etc. — NOT equatable

### Correct Pattern

```csharp
// Pipeline model — only equatable data
internal readonly record struct ConfigInfo(
    string ClassName,
    string FullyQualifiedName,
    string Namespace,
    bool IsPartial,
    Location Location,
    SyntaxReference? ConfigureMethodReference  // SyntaxReference is equatable!
);

// In the pipeline: extract primitives from syntax/semantic model
var configProvider = context.SyntaxProvider
    .ForAttributeWithMetadataName(
        "DataNormalizer.NormalizeConfigurationAttribute",
        predicate: static (node, _) => node is ClassDeclarationSyntax,
        transform: static (ctx, _) =>
        {
            var classNode = (ClassDeclarationSyntax)ctx.TargetNode;
            var symbol = ctx.SemanticModel.GetDeclaredSymbol(classNode)!;

            // Extract the Configure method's SyntaxReference
            var configureMethod = symbol.GetMembers("Configure")
                .OfType<IMethodSymbol>()
                .FirstOrDefault();

            return new ConfigInfo(
                ClassName: symbol.Name,
                FullyQualifiedName: symbol.ToDisplayString(),
                Namespace: symbol.ContainingNamespace.ToDisplayString(),
                IsPartial: classNode.Modifiers.Any(SyntaxKind.PartialKeyword),
                Location: classNode.GetLocation(),
                ConfigureMethodReference: configureMethod?.DeclaringSyntaxReferences.FirstOrDefault()
            );
        });

// In RegisterSourceOutput: reconstruct SemanticModel from Compilation
context.RegisterSourceOutput(
    configProvider.Combine(context.CompilationProvider),
    static (spc, pair) =>
    {
        var (config, compilation) = pair;

        if (!config.IsPartial)
        {
            spc.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.DN0002, config.Location));
            return;
        }

        if (config.ConfigureMethodReference is not { } syntaxRef)
            return;

        // Reconstruct SemanticModel here — inside RegisterSourceOutput
        var syntaxTree = syntaxRef.SyntaxTree;
        var semanticModel = compilation.GetSemanticModel(syntaxTree);
        var methodSyntax = syntaxRef.GetSyntax() as MethodDeclarationSyntax;

        // Now do the heavy analysis
        var model = ConfigurationParser.Parse(methodSyntax!, semanticModel);
        // ... emit code
    });
```

## ForAttributeWithMetadataName

The most efficient way to find types with specific attributes:

```csharp
var provider = context.SyntaxProvider.ForAttributeWithMetadataName(
    fullyQualifiedMetadataName: "DataNormalizer.NormalizeConfigurationAttribute",
    predicate: static (node, _) => node is ClassDeclarationSyntax,
    transform: static (ctx, ct) =>
    {
        // ctx.TargetNode is the attributed node
        // ctx.SemanticModel is available here (but don't store it!)
        // ctx.Attributes contains the matched attributes
        return ExtractPrimitiveData(ctx);
    });
```

## Generator Project Setup

### .csproj Requirements

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <LangVersion>12.0</LangVersion>
    <Nullable>enable</Nullable>
    <EnforceExtendedAnalyzerRules>true</EnforceExtendedAnalyzerRules>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp" PrivateAssets="all" />
    <PackageReference Include="Microsoft.CodeAnalysis.Analyzers" PrivateAssets="all" />
    <PackageReference Include="PolySharp" PrivateAssets="all" />
  </ItemGroup>
</Project>
```

Key points:
- **`netstandard2.0`**: Required by Roslyn. Generators must target this.
- **`EnforceExtendedAnalyzerRules`**: Must be `true`. Catches common generator mistakes at compile time.
- **PolySharp**: Provides polyfills (`IsExternalInit`, nullable attributes, etc.) so you can use C# 12 features on netstandard2.0.
- **`PrivateAssets="all"`**: These packages are compile-time only, never shipped to consumers.

## Diagnostics

Define diagnostics with unique IDs:

```csharp
internal static class DiagnosticDescriptors
{
    public static readonly DiagnosticDescriptor DN0001 = new(
        id: "DN0001",
        title: "Circular reference detected",
        messageFormat: "Circular reference detected: {0}.{1} -> {2}",
        category: "DataNormalizer",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor DN0002 = new(
        id: "DN0002",
        title: "Configuration class must be partial",
        messageFormat: "Class '{0}' must be declared as partial to use [NormalizeConfiguration]",
        category: "DataNormalizer",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor DN0003 = new(
        id: "DN0003",
        title: "Source type has no public properties",
        messageFormat: "Type '{0}' has no public properties to normalize",
        category: "DataNormalizer",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor DN0004 = new(
        id: "DN0004",
        title: "Unmapped complex type will be inlined",
        messageFormat: "Property '{0}.{1}' of type '{2}' is not in the normalization graph and will be inlined",
        category: "DataNormalizer",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true);
}
```

Report diagnostics inside `RegisterSourceOutput`:

```csharp
spc.ReportDiagnostic(Diagnostic.Create(
    DiagnosticDescriptors.DN0001,
    property.Location,
    parentType.Name, property.Name, targetType.Name));
```

## Testing with Verify.SourceGenerators

### Test Setup

```csharp
[TestFixture]
public sealed class GeneratorTests
{
    [Test]
    public Task SimpleType_GeneratesCorrectOutput()
    {
        var source = """
            using DataNormalizer;
            using DataNormalizer.Configuration;

            namespace TestNamespace;

            public class Person
            {
                public string Name { get; set; } = "";
                public int Age { get; set; }
            }

            [NormalizeConfiguration]
            public partial class TestConfig : NormalizationConfig
            {
                protected override void Configure(NormalizeBuilder builder)
                {
                    builder.NormalizeGraph<Person>();
                }
            }
            """;

        return TestHelper.Verify(source);
    }
}
```

### Test Helper

```csharp
internal static class TestHelper
{
    public static Task Verify(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);

        var references = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(NormalizationConfig).Assembly.Location),
        };

        var compilation = CSharpCompilation.Create(
            assemblyName: "Tests",
            syntaxTrees: [syntaxTree],
            references: references);

        var generator = new NormalizeGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver = (CSharpGeneratorDriver)driver.RunGenerators(compilation);

        return Verifier.Verify(driver).UseDirectory("Snapshots");
    }
}
```

### Snapshot Files

Verify creates `.verified.cs` files in a `Snapshots/` directory. These files are committed to source control and represent the expected generator output. When the test runs:

1. Generator produces output
2. Verify compares against `.verified.cs`
3. If they match → PASS
4. If they differ → FAIL, creates `.received.cs` for diff inspection
5. To accept new output: delete `.verified.cs` and rename `.received.cs`

## Common Pitfalls

### 1. GetMembers() Only Gets Declared Members, NOT Inherited

`INamedTypeSymbol.GetMembers()` returns only members declared directly on that type. To include inherited properties, walk the `BaseType` chain:

```csharp
// WRONG - misses inherited properties
var properties = type.GetMembers().OfType<IPropertySymbol>();

// CORRECT - includes inherited properties
static IEnumerable<IPropertySymbol> GetAllPublicProperties(INamedTypeSymbol type)
{
    var current = type;
    while (current is not null)
    {
        foreach (var member in current.GetMembers())
        {
            if (member is IPropertySymbol { DeclaredAccessibility: Accessibility.Public } prop)
                yield return prop;
        }
        current = current.BaseType;
    }
}
```

### 2. IsSimpleType Must Handle Enums and Nullable<T>

```csharp
// WRONG - misses enums and Nullable<T>
static bool IsSimpleType(ITypeSymbol type) =>
    type.SpecialType != SpecialType.None;

// CORRECT
static bool IsSimpleType(ITypeSymbol type)
{
    // Enums are simple
    if (type.TypeKind == TypeKind.Enum)
        return true;

    // Special types (int, string, bool, etc.)
    if (type.SpecialType != SpecialType.None)
        return true;

    // Nullable<T> — unwrap and check inner type
    if (type is INamedTypeSymbol { IsValueType: true, OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } named)
        return IsSimpleType(named.TypeArguments[0]);

    // DateTime, DateTimeOffset, Guid, decimal, TimeSpan, etc.
    var fullName = type.ToDisplayString();
    return fullName is "System.DateTime" or "System.DateTimeOffset" or "System.Guid"
        or "System.Decimal" or "System.TimeSpan" or "System.DateOnly" or "System.TimeOnly"
        or "System.Uri";
}
```

### 3. Collection Detection Must Use AllInterfaces

```csharp
// WRONG - string prefix matching
static bool IsCollection(ITypeSymbol type) =>
    type.ToDisplayString().StartsWith("System.Collections.Generic.List<");

// CORRECT - check AllInterfaces for IEnumerable<T>
static bool TryGetCollectionElementType(ITypeSymbol type, out ITypeSymbol? elementType)
{
    elementType = null;

    // Don't treat string as a collection
    if (type.SpecialType == SpecialType.System_String)
        return false;

    // Check arrays
    if (type is IArrayTypeSymbol array)
    {
        elementType = array.ElementType;
        return true;
    }

    // Check IEnumerable<T> in AllInterfaces
    foreach (var iface in type.AllInterfaces)
    {
        if (iface.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T)
        {
            elementType = iface.TypeArguments[0];
            return true;
        }
    }

    return false;
}
```

### 4. Circular Reference Detection

```csharp
// Track types currently being analyzed to detect cycles
private readonly HashSet<string> _inProgress = new();

public TypeGraphNode Analyze(INamedTypeSymbol type)
{
    var fullName = type.ToDisplayString();

    if (_inProgress.Contains(fullName))
    {
        // Circular reference detected!
        return new TypeGraphNode
        {
            TypeName = type.Name,
            FullyQualifiedName = fullName,
            Properties = [],
            HasCircularReference = true,
        };
    }

    _inProgress.Add(fullName);
    try
    {
        // Analyze properties recursively
        var properties = AnalyzeProperties(type);
        return new TypeGraphNode { /* ... */ };
    }
    finally
    {
        _inProgress.Remove(fullName);
    }
}
```

### 5. Generated Code Must Be Valid C#

Always test that generated code compiles:

```csharp
[Test]
public void GeneratedCode_Compiles_WithoutErrors()
{
    var driver = CSharpGeneratorDriver.Create(new NormalizeGenerator());
    driver = (CSharpGeneratorDriver)driver.RunGenerators(compilation);

    var result = driver.GetRunResult();
    var newCompilation = compilation.AddSyntaxTrees(
        result.GeneratedTrees.ToArray());

    var diagnostics = newCompilation.GetDiagnostics()
        .Where(d => d.Severity == DiagnosticSeverity.Error);

    Assert.That(diagnostics, Is.Empty);
}
```

## Debugging Generators

### Attach Debugger

Add this temporarily to your generator's `Initialize` method:

```csharp
#if DEBUG
if (!System.Diagnostics.Debugger.IsAttached)
    System.Diagnostics.Debugger.Launch();
#endif
```

### Log Output

Use `context.RegisterPostInitializationOutput` for diagnostic output during development:

```csharp
context.RegisterPostInitializationOutput(ctx =>
{
    ctx.AddSource("Debug.g.cs", $"// Generator ran at {DateTime.Now}");
});
```

### Restart IDE After Generator Changes

Source generators are loaded into the compiler process. After modifying generator code, you often need to restart your IDE (or run `dotnet build` from the command line) to pick up changes.
