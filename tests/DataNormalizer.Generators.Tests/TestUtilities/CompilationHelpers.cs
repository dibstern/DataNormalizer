using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;

namespace DataNormalizer.Generators.Tests.TestUtilities;

internal static class CompilationHelpers
{
    public static CSharpCompilation CreateCompilation(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);

        var references = AppDomain
            .CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .Cast<MetadataReference>()
            .ToList();

        return CSharpCompilation.Create(
            "TestAssembly",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );
    }

    public static (CSharpCompilation Compilation, INamedTypeSymbol RootType) CompileAndGetType(
        string source,
        string fullyQualifiedTypeName
    )
    {
        var compilation = CreateCompilation(source);

        var rootType = compilation.GetTypeByMetadataName(fullyQualifiedTypeName);
        Assert.That(rootType, Is.Not.Null, $"Could not find type '{fullyQualifiedTypeName}' in compilation");

        return (compilation, rootType!);
    }
}
