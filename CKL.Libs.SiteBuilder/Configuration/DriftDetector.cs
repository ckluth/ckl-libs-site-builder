namespace CKL.Libs.SiteBuilder.Configuration;

internal static class DriftDetector
{
    public static IReadOnlyList<string> Detect(IEnumerable<string> discoveredRelativeSources, NavMap navMap)
    {
        var discovered = new HashSet<string>(discoveredRelativeSources, StringComparer.OrdinalIgnoreCase);
        var placed = CollectPlacedSources(navMap);

        return discovered
            .Where(source => !placed.Contains(source))
            .OrderBy(source => source, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static ISet<string> CollectPlacedSources(NavMap navMap)
    {
        var placed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in navMap.Entries)
            CollectPlacedSources(entry, placed);
        return placed;
    }

    static void CollectPlacedSources(NavMapEntry entry, ISet<string> placed)
    {
        if (!string.IsNullOrWhiteSpace(entry.Source))
            placed.Add(entry.Source);

        foreach (var child in entry.Children)
            CollectPlacedSources(child, placed);
    }
}
