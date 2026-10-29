using System.Text;
using System.Text.RegularExpressions;

namespace YMTPack.Core.Effects;

/// <summary>
/// Small case-insensitive glob: '*' within one folder, '**' across folders, '?' one character.
/// A pattern without '/' matches the file name only. '[' and ']' are literal, since pack
/// folders are often named like "[MALE] hoodies".
/// </summary>
public static class Glob
{
    public static bool IsMatch(string pattern, string relativePath)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        ArgumentNullException.ThrowIfNull(relativePath);

        string normalizedPattern = Normalize(pattern);
        string path = Normalize(relativePath);
        string target = normalizedPattern.Contains('/') ? path : path[(path.LastIndexOf('/') + 1)..];
        return ToRegex(normalizedPattern).IsMatch(target);
    }

    private static string Normalize(string path)
    {
        string p = path.Replace('\\', '/');
        while (p.StartsWith("./", StringComparison.Ordinal))
        {
            p = p[2..];
        }

        return p.Trim('/');
    }

    private static Regex ToRegex(string pattern)
    {
        var sb = new StringBuilder("^");
        for (int i = 0; i < pattern.Length; i++)
        {
            char c = pattern[i];
            if (c == '*' && i + 1 < pattern.Length && pattern[i + 1] == '*')
            {
                bool folderPrefix = i + 2 < pattern.Length && pattern[i + 2] == '/';
                sb.Append(folderPrefix ? "(?:.*/)?" : ".*");
                i += folderPrefix ? 2 : 1;
            }
            else
            {
                sb.Append(c switch
                {
                    '*' => "[^/]*",
                    '?' => "[^/]",
                    _ => Regex.Escape(c.ToString()),
                });
            }
        }

        return new Regex(sb.Append('$').ToString(), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
}
