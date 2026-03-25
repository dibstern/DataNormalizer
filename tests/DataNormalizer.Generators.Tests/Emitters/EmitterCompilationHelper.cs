using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;

namespace DataNormalizer.Generators.Tests.Emitters;

/// <summary>
/// Compiles emitter output using Roslyn to verify generated code is syntactically and semantically valid.
/// </summary>
internal static class EmitterCompilationHelper
{
    private static readonly MetadataReference[] BaseReferences;

    static EmitterCompilationHelper()
    {
        var refs = new List<MetadataReference>();

        // BCL references from Basic.Reference.Assemblies.Net90
        refs.AddRange(Basic.Reference.Assemblies.Net90.References.All);

        // DataNormalizer runtime library (NormalizationContext etc.)
        refs.Add(
            MetadataReference.CreateFromFile(typeof(DataNormalizer.Runtime.NormalizationContext).Assembly.Location)
        );

        BaseReferences = refs.ToArray();
    }

    /// <summary>
    /// Asserts that the provided source strings compile without errors.
    /// </summary>
    public static void AssertCompiles(params string[] sources)
    {
        var trees = sources.Select(s => CSharpSyntaxTree.ParseText(s)).ToArray();

        var compilation = CSharpCompilation.Create(
            "EmitterTest",
            trees,
            BaseReferences,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithNullableContextOptions(NullableContextOptions.Enable)
        );

        var diagnostics = compilation
            .GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToArray();

        Assert.That(
            diagnostics,
            Is.Empty,
            () =>
                $"Generated code has compilation errors:\n{string.Join("\n", diagnostics.Select(d => d.ToString()))}"
        );
    }

    /// <summary>
    /// Asserts that generated sources compile when combined with stub type definitions.
    /// Use for normalizer/denormalizer code that references user-defined types.
    /// </summary>
    public static void AssertCompilesWithStubs(string[] generated, string[] stubs)
    {
        AssertCompiles(generated.Concat(stubs).ToArray());
    }

    /// <summary>
    /// Asserts that source code does NOT compile (used for negative tests).
    /// </summary>
    public static void AssertDoesNotCompile(params string[] sources)
    {
        var trees = sources.Select(s => CSharpSyntaxTree.ParseText(s)).ToArray();

        var compilation = CSharpCompilation.Create(
            "EmitterTest",
            trees,
            BaseReferences,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithNullableContextOptions(NullableContextOptions.Enable)
        );

        var diagnostics = compilation
            .GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToArray();

        Assert.That(diagnostics, Is.Not.Empty, "Expected compilation errors but code compiled successfully");
    }
}
