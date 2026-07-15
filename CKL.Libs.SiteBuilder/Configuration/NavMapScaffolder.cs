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

            var assembly = SiteAssembler.AssembleConfigured(scanRoots, null, inference);
            if (!assembly.Succeeded) return assembly.ToResult();

            var scaffold = BuildScaffold(assembly.Value.Site.Pages);
            return NavMapFile.Write(navMapPath, scaffold);
        }
        catch (Exception ex)
        {
            return Result.Fail(ex);
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
        var landing = FindLanding(root);
        if (landing is not null)
            entries.Add(new NavMapEntry(landing.Title, landing.RelativeSource, [], false));

        entries.AddRange(root.Files
            .Where(file => !IsLandingFile(file.RelativeSource))
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
        var landing = FindLanding(directory);
        if (landing is not null)
        {
            var landingTitle = IsOriginBackedReadme(landing.SourcePath!)
                ? "README"
                : "Overview";
            children.Add(new NavMapEntry(landingTitle, landing.RelativeSource, [], false));
        }

        children.AddRange(directory.Files
            .Where(file => !IsLandingFile(file.RelativeSource))
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

    static SiteNode? FindLanding(DirectoryNode directory) =>
        directory.Files.FirstOrDefault(file => IsLandingFile(file.RelativeSource));

    static bool IsLandingFile(string relativeSource)
    {
        var fileName = Path.GetFileName(relativeSource);
        return fileName.Equals("README.md", StringComparison.OrdinalIgnoreCase)
            || fileName.Equals("_index.md", StringComparison.OrdinalIgnoreCase);
    }

    static bool IsOriginBackedReadme(string sourcePath) =>
        File.ReadLines(sourcePath).FirstOrDefault()?.StartsWith("[//]: # (origin:", StringComparison.Ordinal) == true;

    sealed class DirectoryNode(string name)
    {
        public string Name { get; } = name;
        public Dictionary<string, DirectoryNode> Directories { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<SiteNode> Files { get; } = [];
    }
}
