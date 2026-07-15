using CKL.Libs.SiteBuilder.Assembly;
using CKL.Libs.SiteBuilder.Model;
using Markdig;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

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
        bool showOrigin = true,
        string mermaidTheme = "dark",
        IReadOnlyDictionary<string, string>? outputMap = null)
    {
        var navHtml = BuildNavHtml(page, navNodes);
        var depth = page.RelativeOutput
            .Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]).Length - 1;
        var prefix = depth > 0 ? string.Concat(Enumerable.Repeat("../", depth)) : "";
        var cssPath = prefix + "site.css";
        var mermaidJsPath = prefix + "mermaid.min.js";

        if (page.SourcePath is null)
        {
            return HtmlTemplate.Render(
                page.Title,
                siteTitle,
                cssPath,
                mermaidJsPath,
                mermaidTheme,
                navHtml,
                page.GeneratedHtml ?? "");
        }

        var markdown = File.ReadAllText(page.SourcePath);
        var originHtml = "";

        if (showOrigin)
        {
            var (origin, stripped) = ExtractOrigin(markdown);
            if (origin is not null)
            {
                markdown = stripped;
                var slash = origin.IndexOf('/');
                var repo = slash > 0 ? origin[..slash] : origin;
                var rest = slash > 0 ? origin[(slash + 1)..] : "";
                var label = rest.Length > 0 ? $"[{repo}]/{rest}" : $"[{repo}]";
                originHtml = $"<div class=\"origin\">{Encode(label)}</div>";
            }
        }

        var document = Markdown.Parse(markdown, Pipeline);
        RewriteLinks(document, page, sourceDir, outputMap);

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

        return HtmlTemplate.Render(
            title,
            siteTitle,
            cssPath,
            mermaidJsPath,
            mermaidTheme,
            navHtml,
            originHtml + contentHtml);
    }

    static (string? origin, string markdown) ExtractOrigin(string markdown)
    {
        const string prefix = "[//]: # (origin: ";
        const string suffix = ")";
        var firstLine = markdown.Split('\n', 2)[0].TrimEnd('\r');
        if (firstLine.StartsWith(prefix) && firstLine.EndsWith(suffix))
        {
            var origin = firstLine[prefix.Length..^suffix.Length];
            var rest = markdown.Length > firstLine.Length + 1
                ? markdown[(firstLine.Length + 1)..]
                : "";
            return (origin, rest);
        }

        return (null, markdown);
    }

    static void RewriteLinks(
        MarkdownDocument document,
        SiteNode currentPage,
        string sourceDir,
        IReadOnlyDictionary<string, string>? outputMap)
    {
        var currentSourceRoot = string.IsNullOrWhiteSpace(currentPage.SourceRoot)
            ? sourceDir
            : currentPage.SourceRoot;
        var currentSourceDir = Path.GetDirectoryName(currentPage.RelativeSource) ?? "";
        var currentOutputDir = Path.GetDirectoryName(currentPage.RelativeOutput) ?? "";

        foreach (var link in document.Descendants<LinkInline>())
        {
            if (link.Url is null || IsAbsolute(link.Url)) continue;

            var fragmentIdx = link.Url.IndexOf('#');
            var pathPart = fragmentIdx >= 0 ? link.Url[..fragmentIdx] : link.Url;
            var fragment = fragmentIdx >= 0 ? link.Url[fragmentIdx..] : "";

            if (pathPart.EndsWith('/'))
            {
                var resolvedDir = Path.GetFullPath(Path.Combine(currentSourceRoot, currentSourceDir, pathPart));
                var indexMdPath = Path.Combine(resolvedDir, "_index.md");
                var readmePath = Path.Combine(resolvedDir, "README.md");
                var candidatePath = File.Exists(indexMdPath) ? indexMdPath
                    : File.Exists(readmePath) ? readmePath
                    : null;
                if (candidatePath is null) continue;

                var relSource = Path.GetRelativePath(currentSourceRoot, candidatePath);
                if (relSource.StartsWith("..")) continue;

                var targetOutput = ResolveOutputPath(relSource, outputMap);
                if (targetOutput is null) continue;

                link.Url = ComputeRelativeHref(currentOutputDir, targetOutput) + fragment;
                continue;
            }

            if (!pathPart.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                continue;

            var resolvedSourcePath = Path.GetFullPath(Path.Combine(currentSourceRoot, currentSourceDir, pathPart));
            var resolvedRelativeSource = Path.GetRelativePath(currentSourceRoot, resolvedSourcePath);

            if (resolvedRelativeSource.StartsWith(".."))
                continue;

            var targetRelativeOutput = ResolveOutputPath(resolvedRelativeSource, outputMap);
            if (targetRelativeOutput is null) continue;
            link.Url = ComputeRelativeHref(currentOutputDir, targetRelativeOutput) + fragment;
        }
    }

    static string? ResolveOutputPath(string relativeSourcePath, IReadOnlyDictionary<string, string>? outputMap)
    {
        if (outputMap is null)
            return SiteAssembler.ToOutputPath(relativeSourcePath);

        return outputMap.TryGetValue(relativeSourcePath, out var targetOutput)
            ? targetOutput
            : null;
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
            AppendNavNode(sb, node, currentOut, currentOutputDir, depth: 0);

        return sb.ToString();
    }

    static void AppendNavNode(
        System.Text.StringBuilder sb,
        SiteNavNode node,
        string currentOut,
        string currentOutputDir,
        int depth)
    {
        var hasChildren = node.Children.Count > 0;

        if (!hasChildren
            && node.Page is not null
            && node.Title.Equals(node.Page.Title, StringComparison.OrdinalIgnoreCase))
        {
            var href = ComputeRelativeHref(currentOutputDir, node.Page.RelativeOutput);
            var active = currentOut.Equals(node.Page.RelativeOutput.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase)
                ? " active"
                : "";
            var cssClass = depth == 0 && node.Page.RelativeOutput.Replace('\\', '/').Equals("index.html", StringComparison.OrdinalIgnoreCase)
                ? "nav-home"
                : "nav-link";
            sb.AppendLine($"<a href=\"{href}\" class=\"{cssClass}{active}\">{Encode(node.Page.Title)}</a>");
            return;
        }

        var openAttr = IsNodeActive(node, currentOut) ? " open" : "";
        var summaryClass = depth == 0 ? "nav-section" : "nav-subsection";
        var indent = new string(' ', (depth + 1) * 2);

        sb.AppendLine($"<details{openAttr}>");
        sb.AppendLine($"{indent}<summary class=\"{summaryClass}\">{Encode(node.Title)}</summary>");

        if (node.Page is not null)
        {
            var href = ComputeRelativeHref(currentOutputDir, node.Page.RelativeOutput);
            var active = currentOut.Equals(node.Page.RelativeOutput.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase)
                ? " active"
                : "";
            sb.AppendLine($"{indent}<a href=\"{href}\" class=\"nav-link{active}\">{Encode(node.Page.Title)}</a>");
        }

        foreach (var child in node.Children)
            AppendNavNode(sb, child, currentOut, currentOutputDir, depth + 1);

        sb.AppendLine("</details>");
    }

    static bool IsNodeActive(SiteNavNode node, string currentOut)
    {
        if (node.Page is not null
            && currentOut.Equals(node.Page.RelativeOutput.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase))
            return true;

        return node.Children.Any(child => IsNodeActive(child, currentOut));
    }

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
