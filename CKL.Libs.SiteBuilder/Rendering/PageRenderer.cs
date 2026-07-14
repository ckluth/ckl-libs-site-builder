using Markdig;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using CKL.Libs.SiteBuilder.Assembly;
using CKL.Libs.SiteBuilder.Model;

namespace CKL.Libs.SiteBuilder.Rendering;

internal static class PageRenderer
{
    internal static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    public static string Render(
        SiteNode page,
        IReadOnlyList<SiteNavNode> navNodes,
        string sourceDir,
        string siteTitle,
        bool showOrigin = true)
    {
        var markdown = File.ReadAllText(page.SourcePath!);
        var originHtml = "";

        if (showOrigin)
        {
            var (origin, stripped) = ExtractOrigin(markdown);
            if (origin is not null)
            {
                markdown = stripped;
                var slash = origin.IndexOf('/');
                var repo  = slash > 0 ? origin[..slash] : origin;
                var rest  = slash > 0 ? origin[(slash + 1)..] : "";
                var label = rest.Length > 0 ? $"[{repo}]/{rest}" : $"[{repo}]";
                originHtml = $"<div class=\"origin\">{Encode(label)}</div>";
            }
        }

        var document = Markdown.Parse(markdown, Pipeline);

        RewriteLinks(document, page, sourceDir);

        var title = ExtractTitle(document)
            ?? SiteAssembler.FormatName(Path.GetFileNameWithoutExtension(page.RelativeSource));

        var writer = new StringWriter();
        var renderer = new HtmlRenderer(writer);
        Pipeline.Setup(renderer);

        for (var i = 0; i < renderer.ObjectRenderers.Count; i++)
        {
            if (renderer.ObjectRenderers[i] is CodeBlockRenderer existing)
            {
                renderer.ObjectRenderers[i] = new MermaidCodeBlockRenderer(existing);
                break;
            }
        }

        renderer.Render(document);
        var contentHtml = writer.ToString();

        var navHtml = BuildNavHtml(page, navNodes);

        var depth = page.RelativeOutput
            .Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]).Length - 1;
        var prefix = depth > 0 ? string.Concat(Enumerable.Repeat("../", depth)) : "";
        var cssPath      = prefix + "site.css";
        var mermaidJsPath = prefix + "mermaid.min.js";

        return HtmlTemplate.Render(title, siteTitle, cssPath, mermaidJsPath, navHtml, originHtml + contentHtml);
    }

    // Reads the origin comment written by the assembler: [//]: # (origin: repo/path)
    // Returns (origin, markdownWithoutComment) or (null, original) if not present.
    static (string? origin, string markdown) ExtractOrigin(string markdown)
    {
        const string prefix = "[//]: # (origin: ";
        const string suffix = ")";
        var firstLine = markdown.Split('\n', 2)[0].TrimEnd('\r');
        if (firstLine.StartsWith(prefix) && firstLine.EndsWith(suffix))
        {
            var origin = firstLine[prefix.Length..^suffix.Length];
            var rest   = markdown.Length > firstLine.Length + 1
                ? markdown[(firstLine.Length + 1)..]
                : "";
            return (origin, rest);
        }
        return (null, markdown);
    }

    static void RewriteLinks(MarkdownDocument document, SiteNode currentPage, string sourceDir)
    {
        var currentSourceDir = Path.GetDirectoryName(currentPage.RelativeSource) ?? "";
        var currentOutputDir = Path.GetDirectoryName(currentPage.RelativeOutput) ?? "";

        foreach (var link in document.Descendants<LinkInline>())
        {
            if (link.Url is null || IsAbsolute(link.Url)) continue;

            var fragmentIdx = link.Url.IndexOf('#');
            var pathPart = fragmentIdx >= 0 ? link.Url[..fragmentIdx] : link.Url;
            var fragment = fragmentIdx >= 0 ? link.Url[fragmentIdx..] : "";

            // Directory-style links → rewrite to index.html in that directory
            if (pathPart.EndsWith('/'))
            {
                var resolvedDir = Path.GetFullPath(Path.Combine(sourceDir, currentSourceDir, pathPart));
                var indexMdPath  = Path.Combine(resolvedDir, "_index.md");
                var readmePath   = Path.Combine(resolvedDir, "README.md");
                var candidatePath = File.Exists(indexMdPath) ? indexMdPath
                                  : File.Exists(readmePath)  ? readmePath
                                  : null;
                if (candidatePath is not null)
                {
                    var relSource = Path.GetRelativePath(sourceDir, candidatePath);
                    if (!relSource.StartsWith(".."))
                    {
                        var targetOutput = SiteAssembler.ToOutputPath(relSource);
                        link.Url = ComputeRelativeHref(currentOutputDir, targetOutput) + fragment;
                    }
                }
                continue;
            }

            if (!pathPart.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                continue;

            var resolvedSourcePath = Path.GetFullPath(Path.Combine(sourceDir, currentSourceDir, pathPart));
            var resolvedRelativeSource = Path.GetRelativePath(sourceDir, resolvedSourcePath);

            if (resolvedRelativeSource.StartsWith(".."))
                continue; // outside source tree, leave as-is

            var targetRelativeOutput = SiteAssembler.ToOutputPath(resolvedRelativeSource);
            link.Url = ComputeRelativeHref(currentOutputDir, targetRelativeOutput) + fragment;
        }
    }

    static string? ExtractTitle(MarkdownDocument document)
    {
        var h1 = document.Descendants<HeadingBlock>().FirstOrDefault(h => h.Level == 1);
        if (h1?.Inline is null) return null;

        var sb = new System.Text.StringBuilder();
        foreach (var literal in h1.Inline.Descendants<LiteralInline>())
            sb.Append(literal.Content.ToString());

        return sb.Length > 0 ? sb.ToString() : null;
    }

    static string BuildNavHtml(SiteNode currentPage, IReadOnlyList<SiteNavNode> navNodes)
    {
        var sb = new System.Text.StringBuilder();
        var currentOut = currentPage.RelativeOutput.Replace('\\', '/');
        var currentOutputDir = (Path.GetDirectoryName(currentPage.RelativeOutput) ?? "").Replace('\\', '/');

        foreach (var node in navNodes)
        {
            if (node.FolderPath == "")
            {
                // Home — plain link, no details
                var href = ComputeRelativeHref(currentOutputDir, node.Landing!.RelativeOutput);
                var active = currentOut.Equals("index.html", StringComparison.OrdinalIgnoreCase) ? " active" : "";
                sb.AppendLine($"<a href=\"{href}\" class=\"nav-home{active}\">{Encode(node.Title)}</a>");
            }
            else
            {
                AppendNavNode(sb, node, currentOut, currentOutputDir, depth: 0);
            }
        }

        return sb.ToString();
    }

    static void AppendNavNode(
        System.Text.StringBuilder sb,
        SiteNavNode node,
        string currentOut,
        string currentOutputDir,
        int depth)
    {
        var folderNorm = node.FolderPath.Replace('\\', '/');
        var isActive = currentOut.StartsWith(folderNorm + "/", StringComparison.OrdinalIgnoreCase);
        var openAttr = isActive ? " open" : "";
        var summaryClass = depth == 0 ? "nav-section" : "nav-subsection";
        var indent = new string(' ', (depth + 1) * 2);

        sb.AppendLine($"<details{openAttr}>");
        sb.AppendLine($"{indent}<summary class=\"{summaryClass}\">{Encode(node.Title)}</summary>");

        if (node.Landing is not null)
        {
            var href = ComputeRelativeHref(currentOutputDir, node.Landing.RelativeOutput);
            var landingOut = node.Landing.RelativeOutput.Replace('\\', '/');
            var active = currentOut.Equals(landingOut, StringComparison.OrdinalIgnoreCase) ? " active" : "";
            sb.AppendLine($"{indent}<a href=\"{href}\" class=\"nav-link{active}\">{Encode(node.Landing.Title)}</a>");
        }

        foreach (var child in node.Children)
            AppendNavNode(sb, child, currentOut, currentOutputDir, depth + 1);

        sb.AppendLine("</details>");
    }

    // Computes relative href from a directory to a file, both relative to the site root.
    // Uses a dummy absolute root so Path.GetRelativePath works correctly with relative inputs.
    internal static string ComputeRelativeHref(string fromDir, string toPath)
    {
        const string root = "C:\\__site__";
        var absFrom = fromDir.Length > 0 ? Path.Combine(root, fromDir) : root;
        var absTo = Path.Combine(root, toPath);
        return Path.GetRelativePath(absFrom, absTo).Replace('\\', '/');
    }

    static bool IsAbsolute(string url) =>
        url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
        url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
        url.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) ||
        url.StartsWith('#');

    static string Encode(string text) => System.Net.WebUtility.HtmlEncode(text);
}
