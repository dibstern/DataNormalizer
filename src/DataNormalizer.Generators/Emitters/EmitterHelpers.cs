using System.Collections.Generic;
using DataNormalizer.Generators.Models;

namespace DataNormalizer.Generators.Emitters;

internal static class EmitterHelpers
{
    public const string GeneratorName = "DataNormalizer";
    public const string GeneratorVersion = "1.0.0";
    public const string CamelCasePolicy = "CamelCase";
    public const int HashSeed = 17;
    public const int HashMultiplier = 397;

    public static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return name;
        }

        // Find the end of the uppercase prefix
        // "Person" → "person", "PhoneNumber" → "phoneNumber", "ID" → "id", "XMLParser" → "xmlParser"
        var i = 0;
        while (i < name.Length && char.IsUpper(name[i]))
        {
            i++;
        }

        if (i == 0)
        {
            return name;
        }

        if (i == 1)
        {
            return char.ToLowerInvariant(name[0]) + name.Substring(1);
        }

        // Multiple uppercase: lowercase all but the last one (unless the whole string is uppercase)
        if (i == name.Length)
        {
            return name.ToLowerInvariant();
        }

        return name.Substring(0, i - 1).ToLowerInvariant() + name.Substring(i - 1);
    }

    public static string GetNamespace(string typeFullName)
    {
        var lastDot = typeFullName.LastIndexOf('.');
        return lastDot > 0 ? typeFullName.Substring(0, lastDot) : "";
    }

    public static string GetDtoFullName(string typeFullName, string typeName)
    {
        var ns = GetNamespace(typeFullName);
        var dtoName = $"Normalized{typeName}";
        return string.IsNullOrEmpty(ns) ? dtoName : $"{ns}.{dtoName}";
    }

    public static string GetShortTypeName(string typeFullName)
    {
        var lastDot = typeFullName.LastIndexOf('.');
        return lastDot >= 0 ? typeFullName.Substring(lastDot + 1) : typeFullName;
    }

    public static TypeGraphNode? FindNode(IReadOnlyList<TypeGraphNode> allNodes, string typeFullName)
    {
        for (var i = 0; i < allNodes.Count; i++)
        {
            if (allNodes[i].TypeFullName == typeFullName)
            {
                return allNodes[i];
            }
        }

        return null;
    }

    public static string GetTypeKey(TypeGraphNode node, NormalizationModel model)
    {
        if (model.TypeConfigurations.TryGetValue(node.TypeFullName, out var config) && config.CustomName != null)
        {
            return config.CustomName;
        }

        return node.TypeName;
    }

    public static string GetListPropertyName(TypeGraphNode node, IReadOnlyList<TypeGraphNode> allNodes)
    {
        var count = 0;
        for (var i = 0; i < allNodes.Count; i++)
        {
            if (allNodes[i].TypeName == node.TypeName)
            {
                count++;
            }
        }

        if (count <= 1)
        {
            return $"{node.TypeName}List";
        }

        var ns = GetNamespace(node.TypeFullName);
        if (string.IsNullOrEmpty(ns))
        {
            return $"{node.TypeName}List";
        }

        var prefix = ns.Replace(".", "");
        return $"{prefix}{node.TypeName}List";
    }

    public static string ToPlural(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return name;
        }

        if (
            name.EndsWith("s")
            || name.EndsWith("x")
            || name.EndsWith("z")
            || name.EndsWith("sh")
            || name.EndsWith("ch")
        )
        {
            return name + "es";
        }

        if (name.EndsWith("y") && name.Length > 1 && !IsVowel(name[name.Length - 2]))
        {
            return name.Substring(0, name.Length - 1) + "ies";
        }

        return name + "s";
    }

    public static string GetContainerFullName(string typeFullName, string typeName)
    {
        var ns = GetNamespace(typeFullName);
        var containerName = $"Normalized{typeName}Result";
        return string.IsNullOrEmpty(ns) ? containerName : $"{ns}.{containerName}";
    }

    private static bool IsVowel(char c)
    {
        return "aeiouAEIOU".IndexOf(c) >= 0;
    }
}
