using System.Text;

namespace EntityForge.Core;

public static class NamingHelper
{
    public static string ToKebabCase(string str)
    {
        if (string.IsNullOrEmpty(str)) return str;
        var sb = new StringBuilder(str.Length + 8);
        for (var i = 0; i < str.Length; i++)
        {
            var ch = str[i];
            if (char.IsUpper(ch))
            {
                if (i > 0) sb.Append('-');
                sb.Append(char.ToLowerInvariant(ch));
            }
            else
            {
                sb.Append(ch);
            }
        }

        return sb.ToString();
    }

    public static string ToSnakeCase(string str)
    {
        if (string.IsNullOrEmpty(str)) return str;
        return string.Concat(str.Select((x, i) =>
            i > 0 && char.IsUpper(x) ? "_" + char.ToLowerInvariant(x) : char.ToLowerInvariant(x).ToString()));
    }

    public static string GetPluralName(string name, string? pluralName)
    {
        if (!string.IsNullOrWhiteSpace(pluralName)) return pluralName;
        if (string.IsNullOrWhiteSpace(name)) return name;
        return name + "s";
    }

    public static string GetDbName(string serviceName)
    {
        return $"{serviceName.ToLowerInvariant()}_db";
    }

    public static string ToCamelCase(string str)
    {
        if (string.IsNullOrEmpty(str)) return str;
        return char.ToLowerInvariant(str[0]) + str.Substring(1);
    }
}