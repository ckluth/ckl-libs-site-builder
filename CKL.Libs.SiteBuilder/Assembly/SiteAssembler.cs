using CKL.Libs.ResultPattern;
using CKL.Libs.SiteBuilder.Model;

namespace CKL.Libs.SiteBuilder.Assembly;

/// <summary>
/// Produces a <see cref="SiteModel"/> in memory from a scan root — no staging folder
/// is ever written to disk (ADR 0019 §2). Ported behaviour-for-behaviour from the
/// source renderer's discovery/nav logic (<c>SiteBuilder.cs</c> in
/// site-builder-renderer): <c>DiscoverPages</c>, <c>BuildNav</c>, <c>BuildNavNode</c>,
/// <c>ToOutputPath</c>, <c>FormatName</c>, <c>FormatFolderName</c>, <c>ExtractTitle</c>.
/// </summary>
internal static class SiteAssembler
{
    public static Result<SiteModel> Assemble(string sourceDir)
    {
        try
        {
            var pages = DiscoverPages(sourceDir);
            var nav = BuildNav(pages, sourceDir);
            return new SiteModel(pages, nav);
        }
        catch (Exception ex)
        {
            return Result<SiteModel>.Fail(ex);
        }
    }

    static List<SiteNode> DiscoverPages(string sourceDir) =>
        Directory
            .GetFiles(sourceDir, "*.md", SearchOption.AllDirectories)
            .Select(sourcePath =>
            {
                var rel = Path.GetRelativePath(sourceDir, sourcePath);
                var relativeOutput = ToOutputPath(rel);
                var title = ExtractTitle(sourcePath)
                    ?? FormatName(Path.GetFileNameWithoutExtension(rel));
                var kind = Path.GetFileName(relativeOutput).Equals("index.html", StringComparison.OrdinalIgnoreCase)
                    ? SiteNodeKind.Landing
                    : SiteNodeKind.Document;
                return new SiteNode(sourcePath, rel, relativeOutput, title, kind, SiteNode.NoOverrides);
            })
            .OrderBy(p => p.RelativeSource)
            .ToList();

    internal static string ToOutputPath(string relativeSourcePath)
    {
        var fileName = Path.GetFileName(relativeSourcePath);
        var dir = Path.GetDirectoryName(relativeSourcePath) ?? "";

        var outputFileName = fileName.Equals("README.md", StringComparison.OrdinalIgnoreCase)
                             || fileName.Equals("_index.md", StringComparison.OrdinalIgnoreCase)
            ? "index.html"
            : Path.ChangeExtension(fileName, ".html");

        return dir.Length > 0 ? Path.Combine(dir, outputFileName) : outputFileName;
    }

    internal static List<SiteNavNode> BuildNav(List<SiteNode> pages, string sourceDir)
    {
        var nodes = new List<SiteNavNode>();

        var homePage = pages.FirstOrDefault(p =>
            p.RelativeOutput.Equals("index.html", StringComparison.OrdinalIgnoreCase));
        if (homePage is not null)
        {
            var homeTitle = ExtractTitle(homePage.SourcePath!) ?? "Home";
            nodes.Add(new SiteNavNode("", homeTitle, new SiteNavPage(homeTitle, "index.html"), []));
        }

        foreach (var dir in Directory.GetDirectories(sourceDir).OrderBy(d => d))
        {
            var folderName = Path.GetFileName(dir)!;
            var node = BuildNavNode(folderName, pages, sourceDir, depth: 1, maxDepth: 2);
            if (node is not null) nodes.Add(node);
        }

        return nodes;
    }

    static SiteNavNode? BuildNavNode(string relativePath, List<SiteNode> pages, string sourceDir, int depth, int maxDepth)
    {
        var relNorm = relativePath.Replace('\\', '/');
        var indexOutput = relNorm + "/index.html";

        var landing = pages.FirstOrDefault(p =>
            p.RelativeOutput.Replace('\\', '/').Equals(indexOutput, StringComparison.OrdinalIgnoreCase));

        if (landing is null) return null;

        var title = FormatFolderName(Path.GetFileName(relativePath));
        var landingLabel = File.ReadLines(landing.SourcePath!).FirstOrDefault()
            ?.StartsWith("[//]: # (origin:") == true ? "README" : "Overview";
        var landingNav = new SiteNavPage(landingLabel, landing.RelativeOutput);

        var children = new List<SiteNavNode>();
        if (depth < maxDepth)
        {
            var fullPath = Path.Combine(sourceDir, relativePath);
            foreach (var subDir in Directory.GetDirectories(fullPath).OrderBy(d => d))
            {
                var subRel = Path.Combine(relativePath, Path.GetFileName(subDir)!);
                var child = BuildNavNode(subRel, pages, sourceDir, depth + 1, maxDepth);
                if (child is not null) children.Add(child);
            }
        }

        return new SiteNavNode(relativePath, title, landingNav, children);
    }

    internal static string? ExtractTitle(string sourcePath)
    {
        foreach (var line in File.ReadLines(sourcePath))
        {
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("# "))
                return trimmed[2..].Trim();
            if (trimmed.Length > 0 && !trimmed.StartsWith('#'))
                break;
        }
        return null;
    }

    internal static string FormatFolderName(string folderName)
    {
        var withoutNumber = System.Text.RegularExpressions.Regex.Replace(folderName, @"^\d+[-_]?", "");
        return FormatName(withoutNumber);
    }

    internal static string FormatName(string name)
    {
        var words = name.Replace('-', ' ').Replace('_', ' ')
                        .Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(" ", words.Select(w => char.ToUpper(w[0]) + w[1..]));
    }
}
