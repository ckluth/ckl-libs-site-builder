using CKL.Libs.ResultPattern;
using CKL.Libs.SiteBuilder.Assembly;
using CKL.Libs.SiteBuilder.Metadata;
using CKL.Libs.SiteBuilder.Model;

namespace CKL.Libs.SiteBuilder.Configuration;

internal static class NavMapScaffolder
{
    public static Result EnsureExists(string navMapPath, IReadOnlyList<string> scanRoots, IMetadataInference inference)
    {
        try
        {
            if (File.Exists(navMapPath))
                return Result.Success;

            var scaffold = BuildInMemory(scanRoots, inference);
            if (!scaffold.Succeeded) return scaffold.ToResult();

            return NavMapFile.Write(navMapPath, scaffold.Value);
        }
        catch (Exception ex)
        {
            return Result.Fail(ex);
        }
    }

    /// <summary>Builds a nav map from discovered pages without persisting it, for direct-options builds and internal scaffolding.</summary>
    public static Result<NavMap> BuildInMemory(IReadOnlyList<string> scanRoots, IMetadataInference inference)
    {
        try
        {
            var discovered = SiteAssembler.DiscoverPagesForScaffold(scanRoots, inference);
            if (!discovered.Succeeded) return discovered.ToResult<NavMap>();

            return BuildScaffold(discovered.Value);
        }
        catch (Exception ex)
        {
            return Result<NavMap>.Fail(ex);
        }
    }

    static NavMap BuildScaffold(IReadOnlyList<SiteNode> pages)
    {
        var root = new DirectoryNode("");

        foreach (var page in pages.Where(p => p.SourcePath is not null))
            AddPage(root, page);

        var entries = BuildRootEntries(root);
        return new NavMap(entries);
    }

    static void AddPage(DirectoryNode root, SiteNode page)
    {
        var parts = page.RelativeSource.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
        var directory = root;

        for (var i = 0; i < parts.Length - 1; i++)
        {
            if (!directory.Directories.TryGetValue(parts[i], out var child))
            {
                child = new DirectoryNode(parts[i]);
                directory.Directories[parts[i]] = child;
            }

            directory = child;
        }

        directory.Files.Add(page);
    }

    static IReadOnlyList<NavMapEntry> BuildRootEntries(DirectoryNode root)
    {
        var entries = new List<NavMapEntry>();

        entries.AddRange(root.Files
            .OrderBy(file => file.RelativeSource, StringComparer.OrdinalIgnoreCase)
            .Select(file => new NavMapEntry(file.Title, file.RelativeSource, [], false)));

        foreach (var directory in root.Directories.Values.OrderBy(dir => dir.Name, StringComparer.OrdinalIgnoreCase))
        {
            var section = BuildSection(directory);
            if (section is not null)
                entries.Add(section);
        }

        return entries;
    }

    static NavMapEntry? BuildSection(DirectoryNode directory)
    {
        var children = new List<NavMapEntry>();

        children.AddRange(directory.Files
            .OrderBy(file => file.RelativeSource, StringComparer.OrdinalIgnoreCase)
            .Select(file => new NavMapEntry(file.Title, file.RelativeSource, [], false)));

        foreach (var childDirectory in directory.Directories.Values.OrderBy(dir => dir.Name, StringComparer.OrdinalIgnoreCase))
        {
            var childSection = BuildSection(childDirectory);
            if (childSection is not null)
                children.Add(childSection);
        }

        return children.Count == 0
            ? null
            : new NavMapEntry(SiteAssembler.FormatFolderName(directory.Name), null, children, false);
    }

    sealed class DirectoryNode(string name)
    {
        public string Name { get; } = name;
        public Dictionary<string, DirectoryNode> Directories { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<SiteNode> Files { get; } = [];
    }
}
