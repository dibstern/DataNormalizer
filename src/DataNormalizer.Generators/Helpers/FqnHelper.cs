namespace DataNormalizer.Generators.Helpers;

internal static class FqnHelper
{
    public static string NormalizeFqn(string fqn)
    {
        return fqn.StartsWith("global::", System.StringComparison.Ordinal) ? fqn.Substring("global::".Length) : fqn;
    }

    public static string BuildPropertyKey(string typeFqn, string propertyName)
    {
        return NormalizeFqn($"{typeFqn}.{propertyName}");
    }
}
