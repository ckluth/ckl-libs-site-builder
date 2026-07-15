using System.Text.RegularExpressions;

namespace CKL.Libs.SiteBuilder.Metadata;

/// <summary>
/// Extracts a document's <b>structural</b> metadata — the authoritative source in
/// the ADR 0020 §1 / ADR 0018 precedence chain — from its location and header.
/// Never overwritten by a later stage (frontmatter, AI): whatever this stage
/// resolves is final.
/// </summary>
internal static class StructuralExtractor
{
    static readonly Dictionary<string, string> TypedFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ideas"] = "idea",
        ["decisions"] = "decision",
        ["plans"] = "plan",
    };

    // Strips a typed artifact's "Family NNNN:" H1 prefix (e.g. "Idea 0014:", "ADR-0018:", "Plan 0014:").
    static readonly Regex FamilyPrefix = new(@"^[A-Za-z]+[-\s]?\d+:\s*", RegexOptions.Compiled);

    static readonly Regex DateHeader = new(@"^-\s*Date:\s*(.+)$", RegexOptions.Compiled);
    static readonly Regex StatusHeader = new(@"^-\s*Status:\s*(.+)$", RegexOptions.Compiled);
    static readonly Regex TagsHeader = new(@"^-\s*Tags:\s*(.+)$", RegexOptions.Compiled);

    /// <summary>
    /// Extracts structural metadata for a document at <paramref name="relativeSourcePath"/>
    /// (relative to the scan root, forward- or backslash-separated) given its content.
    /// </summary>
    public static DocumentMetadata Extract(string relativeSourcePath, string content)
    {
        var type = ExtractType(relativeSourcePath);
        var isTypedArtifact = TypedFolders.Values.Contains(type);

        var lines = content.Split('\n');
        string? title = null;
        string? date = null;
        string? state = null;
        IReadOnlyList<string>? tags = null;

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r').TrimStart();

            if (title is null && line.StartsWith("# "))
            {
                var h1 = line[2..].Trim();
                title = isTypedArtifact ? FamilyPrefix.Replace(h1, "") : h1;
                continue;
            }

            var dateMatch = DateHeader.Match(line);
            if (date is null && dateMatch.Success) { date = dateMatch.Groups[1].Value.Trim(); continue; }

            var statusMatch = StatusHeader.Match(line);
            if (state is null && statusMatch.Success) { state = statusMatch.Groups[1].Value.Trim(); continue; }

            var tagsMatch = TagsHeader.Match(line);
            if (tags is null && tagsMatch.Success)
            {
                tags = tagsMatch.Groups[1].Value
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToList();
            }
        }

        return new DocumentMetadata(Type: type, Title: title, Date: date, State: state, Tags: tags);
    }

    static string ExtractType(string relativeSourcePath)
    {
        var normalized = relativeSourcePath.Replace('\\', '/');
        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);

        // Look for a typed folder anywhere in the path (e.g. "docs/decisions/0018-....md").
        foreach (var segment in segments)
        {
            if (TypedFolders.TryGetValue(segment, out var typedType))
                return typedType;
        }

        // Otherwise, the immediate meaningful folder name (the file's parent folder), or a generic "doc".
        var parent = segments.Length > 1 ? segments[^2] : null;
        return string.IsNullOrEmpty(parent) ? "doc" : parent;
    }
}
