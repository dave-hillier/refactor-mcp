using System;
using System.Linq;
using System.Reflection;
using System.Text;

internal static class ToolNameHelper
{
    public static bool Matches(string toolName, MethodInfo method)
    {
        if (string.IsNullOrWhiteSpace(toolName))
            return false;

        return method.Name.Equals(toolName, StringComparison.OrdinalIgnoreCase)
               || ToKebabCase(method.Name).Equals(toolName, StringComparison.OrdinalIgnoreCase)
               || method.Name.Equals(ToPascalCase(toolName), StringComparison.OrdinalIgnoreCase);
    }

    public static string ToKebabCase(string name)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c) && i > 0)
                sb.Append('-');
            sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }

    public static string ToPascalCase(string name)
    {
        if (!name.Contains('-'))
            return name;

        var parts = name.Split('-', StringSplitOptions.RemoveEmptyEntries);
        return string.Concat(parts.Select(part =>
            part.Length == 0
                ? string.Empty
                : char.ToUpperInvariant(part[0]) + part.Substring(1)));
    }
}
