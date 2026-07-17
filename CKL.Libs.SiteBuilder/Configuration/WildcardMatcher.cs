using System.Text.RegularExpressions;
using System.Text;

namespace CKL.Libs.SiteBuilder.Configuration;

internal static class WildcardMatcher
{
    public static bool IsMatch(string relativeSource, string pattern)
    {
        var normalizedSource = Normalize(relativeSource);
        var normalizedPattern = Normalize(pattern);
        var regexPattern = ToRegexPattern(normalizedPattern);

        return Regex.IsMatch(
            normalizedSource,
            $"^{regexPattern}$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    public static string Normalize(string path) => path.Replace('\\', '/');

    static string ToRegexPattern(string normalizedPattern)
    {
        var builder = new StringBuilder();

        for (var i = 0; i < normalizedPattern.Length; i++)
        {
            var current = normalizedPattern[i];
            if (current == '*')
            {
                var isDoubleStar = i + 1 < normalizedPattern.Length && normalizedPattern[i + 1] == '*';
                if (isDoubleStar)
                {
                    var followedBySlash = i + 2 < normalizedPattern.Length && normalizedPattern[i + 2] == '/';
                    if (followedBySlash)
                    {
                        builder.Append("(?:.*/)?");
                        i += 2;
                    }
                    else
                    {
                        builder.Append(".*");
                        i++;
                    }

                    continue;
                }

                builder.Append("[^/]*");
                continue;
            }

            if (current == '?')
            {
                builder.Append("[^/]");
                continue;
            }

            builder.Append(Regex.Escape(current.ToString()));
        }

        return builder.ToString();
    }
}
