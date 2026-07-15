namespace CKL.Libs.SiteBuilder.Metadata;

/// <summary>
/// The outcome of applying <see cref="FrontmatterOverlay"/>: the metadata after
/// overlay, plus any defects found (a frontmatter value contradicting a
/// structural one — surfaced, never applied as an override; ADR 0018 Knob A).
/// </summary>
internal sealed record FrontmatterOverlayResult(DocumentMetadata Metadata, IReadOnlyList<string> Defects);

/// <summary>
/// Overlays a leading YAML frontmatter block onto structurally-resolved metadata
/// (ADR 0020 §2). Frontmatter is <b>never required</b> and is honoured at
/// <b>lower precedence than structure</b>: it fills a field only when structure
/// left it empty. A frontmatter value that contradicts a structural value is a
/// defect to surface, not an override (ADR 0018 Knob A).
/// </summary>
internal static class FrontmatterOverlay
{
    static readonly string[] RecognizedKeys = ["type", "title", "date", "state", "tags", "summary", "perspective"];

    public static FrontmatterOverlayResult Apply(DocumentMetadata structural, string content)
    {
        var frontmatter = ParseFrontmatter(content);
        var frontmatterLists = ParseFrontmatterLists(content);
        if (frontmatter.Count == 0 && frontmatterLists.Count == 0)
            return new FrontmatterOverlayResult(structural, []);

        var defects = new List<string>();

        string? Overlay(string key, string? structuralValue)
        {
            if (!frontmatter.TryGetValue(key, out var fmValue) || string.IsNullOrWhiteSpace(fmValue))
                return structuralValue;

            if (!string.IsNullOrWhiteSpace(structuralValue))
            {
                if (!string.Equals(structuralValue, fmValue, StringComparison.OrdinalIgnoreCase))
                    defects.Add($"Frontmatter '{key}: {fmValue}' contradicts structural value '{structuralValue}'.");
                return structuralValue; // structure wins — ADR 0018 Knob A
            }

            return fmValue;
        }

        IReadOnlyList<string>? OverlayTags()
        {
            var fmTags = frontmatterLists.TryGetValue("tags", out var listItems) && listItems.Count > 0
                ? listItems
                : frontmatter.TryGetValue("tags", out var scalarTags) && !string.IsNullOrWhiteSpace(scalarTags)
                    ? SplitTags(scalarTags)
                    : null;

            if (structural.Tags is { Count: > 0 })
            {
                if (fmTags is not null && !fmTags.SequenceEqual(structural.Tags, StringComparer.OrdinalIgnoreCase))
                    defects.Add($"Frontmatter 'tags: {string.Join(", ", fmTags)}' contradicts structural value '{string.Join(", ", structural.Tags)}'.");
                return structural.Tags;
            }

            return fmTags ?? structural.Tags;
        }

        var resolved = structural with
        {
            Type = Overlay("type", structural.Type),
            Title = Overlay("title", structural.Title),
            Date = Overlay("date", structural.Date),
            State = Overlay("state", structural.State),
            Tags = OverlayTags(),
            Summary = Overlay("summary", structural.Summary),
            Perspective = Overlay("perspective", structural.Perspective),
        };

        return new FrontmatterOverlayResult(resolved, defects);
    }

    static List<string> SplitTags(string raw)
    {
        var trimmed = raw.Trim();
        if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
            trimmed = trimmed[1..^1];

        return [.. trimmed
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => t.Trim('"', '\''))];
    }

    /// <summary>Parses a leading <c>---</c>-delimited frontmatter block into flat scalar key/value pairs.</summary>
    static Dictionary<string, string> ParseFrontmatter(string content)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var block = ExtractFrontmatterBlock(content);
        if (block is null) return result;

        foreach (var line in block)
        {
            var trimmed = line.TrimEnd('\r');
            if (trimmed.TrimStart().StartsWith('-')) continue; // list item — handled by ParseFrontmatterLists
            var colon = trimmed.IndexOf(':');
            if (colon <= 0) continue;

            var key = trimmed[..colon].Trim().ToLowerInvariant();
            if (!RecognizedKeys.Contains(key)) continue;

            var value = trimmed[(colon + 1)..].Trim().Trim('"', '\'');
            if (value.Length > 0) result[key] = value;
        }

        return result;
    }

    /// <summary>Parses <c>key:</c> followed by <c>- item</c> lines (a YAML block list) for recognized keys.</summary>
    static Dictionary<string, List<string>> ParseFrontmatterLists(string content)
    {
        var result = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var block = ExtractFrontmatterBlock(content);
        if (block is null) return result;

        string? currentKey = null;
        foreach (var line in block)
        {
            var trimmed = line.TrimEnd('\r');
            var stripped = trimmed.TrimStart();

            if (stripped.StartsWith("- ") && currentKey is not null)
            {
                result.TryAdd(currentKey, []);
                result[currentKey].Add(stripped[2..].Trim().Trim('"', '\''));
                continue;
            }

            var colon = trimmed.IndexOf(':');
            if (colon <= 0) { currentKey = null; continue; }

            var key = trimmed[..colon].Trim().ToLowerInvariant();
            var value = trimmed[(colon + 1)..].Trim();
            currentKey = RecognizedKeys.Contains(key) && value.Length == 0 ? key : null;
        }

        return result;
    }

    static List<string>? ExtractFrontmatterBlock(string content)
    {
        var lines = content.Replace("\r\n", "\n").Split('\n');
        if (lines.Length == 0 || lines[0].Trim() != "---") return null;

        var block = new List<string>();
        for (var i = 1; i < lines.Length; i++)
        {
            if (lines[i].Trim() == "---") return block;
            block.Add(lines[i]);
        }

        return null; // no closing delimiter — not a valid frontmatter block
    }
}
