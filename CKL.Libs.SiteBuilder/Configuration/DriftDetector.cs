namespace CKL.Libs.SiteBuilder.Configuration;

internal static class DriftDetector
{
    public static IReadOnlyList<string> Detect(IEnumerable<string> discoveredRelativeSources, NavMap navMap)
    {
        var discovered = discoveredRelativeSources.ToArray();
        var placed = CollectPlacedSources(discovered, navMap);

        return discovered
            .Where(source => !placed.Contains(WildcardMatcher.Normalize(source)))
            .OrderBy(source => source, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static ISet<string> CollectPlacedSources(IEnumerable<string> discoveredRelativeSources, NavMap navMap)
    {
        var discovered = discoveredRelativeSources.ToArray();
        var placed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in navMap.Entries)
            CollectPlacedSources(entry, discovered, placed);
        return placed;
    }

    static void CollectPlacedSources(
        NavMapEntry entry,
        IReadOnlyList<string> discoveredRelativeSources,
        ISet<string> placed)
    {
        if (!string.IsNullOrWhiteSpace(entry.Source))
        {
            if (entry.Source.Contains('*') || entry.Source.Contains('?'))
            {
                foreach (var discovered in discoveredRelativeSources.Where(source => WildcardMatcher.IsMatch(source, entry.Source)))
                    placed.Add(WildcardMatcher.Normalize(discovered));

                foreach (var excluded in entry.Exclude)
                    placed.Add(WildcardMatcher.Normalize(excluded));
            }
            else
            {
                placed.Add(WildcardMatcher.Normalize(entry.Source));
            }
        }

        foreach (var child in entry.Children)
            CollectPlacedSources(child, discoveredRelativeSources, placed);
    }
}
