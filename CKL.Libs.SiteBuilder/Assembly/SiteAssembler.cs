using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CKL.Libs.ResultPattern;
using CKL.Libs.SiteBuilder.Configuration;
using CKL.Libs.SiteBuilder.Metadata;
using CKL.Libs.SiteBuilder.Model;

namespace CKL.Libs.SiteBuilder.Assembly;

internal sealed record SiteAssemblyResult(SiteModel Site, IReadOnlyList<string> UnplacedDocuments);

internal static class SiteAssembler
{
    public static Result<SiteModel> Assemble(string sourceDir, IMetadataInference? inference = null)
    {
        var effectiveInference = inference ?? NoOpMetadataInference.Instance;
        var scaffold = NavMapScaffolder.BuildInMemory([sourceDir], effectiveInference);
        if (!scaffold.Succeeded) return scaffold.ToResult<SiteModel>();

        var result = AssembleConfigured([sourceDir], scaffold.Value, inference);
        if (!result.Succeeded) return result.ToResult<SiteModel>();
        return result.Value.Site;
    }

    public static Result<SiteAssemblyResult> AssembleConfigured(
        IReadOnlyList<string> scanRoots,
        NavMap navMap,
        IMetadataInference? inference = null,
        SectionBehaviour sectionBehaviour = SectionBehaviour.Expand)
    {
        try
        {
            var metadataIndex = MetadataIndex.Build(scanRoots, inference ?? NoOpMetadataInference.Instance);
            if (!metadataIndex.Succeeded) return metadataIndex.ToResult<SiteAssemblyResult>();

            var discovered = DiscoverPages(scanRoots, metadataIndex.Value);

            var drift = DriftDetector.Detect(discovered.Select(page => page.RelativeSource), navMap);
            var mapped = AssembleFromNavMap(discovered, metadataIndex.Value, navMap, sectionBehaviour);
            if (!mapped.Succeeded) return mapped.ToResult<SiteAssemblyResult>();

            return new SiteAssemblyResult(mapped.Value, drift);
        }
        catch (Exception ex)
        {
            return Result<SiteAssemblyResult>.Fail(ex);
        }
    }

    /// <summary>Discovers pages under the scan roots and projects them as ordinary (unmapped) site nodes, for nav-map scaffolding.</summary>
    internal static Result<IReadOnlyList<SiteNode>> DiscoverPagesForScaffold(
        IReadOnlyList<string> scanRoots,
        IMetadataInference inference)
    {
        try
        {
            var metadataIndex = MetadataIndex.Build(scanRoots, inference);
            if (!metadataIndex.Succeeded) return metadataIndex.ToResult<IReadOnlyList<SiteNode>>();

            var discovered = DiscoverPages(scanRoots, metadataIndex.Value);
            IReadOnlyList<SiteNode> nodes = discovered
                .Select(page => page.ToMappedSiteNode(page.Title, ToOutputPath(page.RelativeSource), page.Kind))
                .OrderBy(page => page.RelativeSource, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return Result<IReadOnlyList<SiteNode>>.Success(nodes);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<SiteNode>>.Fail(ex);
        }
    }

    static Result<SiteModel> AssembleFromNavMap(
        IReadOnlyList<DiscoveredPage> discovered,
        MetadataIndex metadataIndex,
        NavMap navMap,
        SectionBehaviour sectionBehaviour)
    {
        try
        {
            var bySource = discovered.ToDictionary(page => page.RelativeSource, StringComparer.OrdinalIgnoreCase);
            var pages = new List<SiteNode>();
            var navNodes = new List<SiteNavNode>();
            var placedSources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var usedOutputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var homeSources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            AppendMappedEntries(
                navMap.Entries,
                currentDirectory: "",
                pages,
                navNodes,
                bySource,
                placedSources,
                usedOutputs,
                homeSources,
                sectionBehaviour);

            if (!usedOutputs.Contains("index.html"))
            {
                var landingHtml = BuildNodeIndexHtml("Home", "index.html", navNodes);
                EnsureOutputAvailable("index.html", usedOutputs);
                pages.Add(new SiteNode(
                    SourcePath: null,
                    RelativeSource: "",
                    RelativeOutput: "index.html",
                    Title: "Home",
                    Kind: SiteNodeKind.Landing,
                    Overrides: SiteNode.NoOverrides,
                    GeneratedHtml: landingHtml));
                navNodes.Insert(0, new SiteNavNode("Home", new SiteNavPage("Home", "index.html"), []));
            }

            var searchPage = BuildSearchNode(metadataIndex, pages, usedOutputs);
            pages.Add(searchPage);
            navNodes.Add(new SiteNavNode("Search", new SiteNavPage("Search", searchPage.RelativeOutput), []));

            return new SiteModel(
                pages.OrderBy(page => page.RelativeOutput, StringComparer.OrdinalIgnoreCase).ToArray(),
                navNodes);
        }
        catch (Exception ex)
        {
            return Result<SiteModel>.Fail(ex);
        }
    }

    static void AppendMappedEntries(
        IReadOnlyList<NavMapEntry> entries,
        string currentDirectory,
        List<SiteNode> pages,
        List<SiteNavNode> navNodes,
        IReadOnlyDictionary<string, DiscoveredPage> bySource,
        ISet<string> placedSources,
        ISet<string> usedOutputs,
        ISet<string> homeSources,
        SectionBehaviour sectionBehaviour)
    {
        var reservedSlugs = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in entries)
        {
            var slug = ReserveSlug(reservedSlugs, Slugify(entry.Title));

            if (entry.Skip)
            {
                MarkPlaced(entry, placedSources, bySource);
                continue;
            }

            if (!string.IsNullOrWhiteSpace(entry.Source))
            {
                if (!bySource.TryGetValue(entry.Source!, out var discovered))
                    throw new InvalidOperationException($"The nav map references '{entry.Source}', but no such source document was discovered.");

                if (!placedSources.Add(entry.Source!))
                    throw new InvalidOperationException($"The nav map places '{entry.Source}' more than once.");

                string relativeOutput;
                SiteNodeKind kind;

                if (entry.Home)
                {
                    if (homeSources.Count > 0)
                        throw new InvalidOperationException(
                            $"The nav map designates more than one entry as home ('{homeSources.First()}' and '{entry.Source}'); exactly one entry may have 'home: true'.");

                    homeSources.Add(entry.Source!);
                    relativeOutput = "index.html";
                    kind = SiteNodeKind.Landing;
                }
                else
                {
                    relativeOutput = CombineOutput(currentDirectory, slug + ".html");
                    kind = SiteNodeKind.Document;
                }

                EnsureOutputAvailable(relativeOutput, usedOutputs);

                var node = discovered.ToMappedSiteNode(entry.Title, relativeOutput, kind);
                pages.Add(node);
                navNodes.Add(new SiteNavNode(entry.Title, new SiteNavPage(entry.Title, relativeOutput), []));
                continue;
            }

            var sectionDirectory = CombineOutput(currentDirectory, slug);
            var childNavNodes = new List<SiteNavNode>();
            var effectiveBehaviour = entry.Section ?? sectionBehaviour;

            AppendMappedEntries(
                entry.Children,
                sectionDirectory,
                pages,
                childNavNodes,
                bySource,
                placedSources,
                usedOutputs,
                homeSources,
                effectiveBehaviour);

            if (effectiveBehaviour == SectionBehaviour.Overview)
            {
                var sectionOutput = Path.Combine(sectionDirectory, "index.html");
                EnsureOutputAvailable(sectionOutput, usedOutputs);

                var generatedHtml = BuildNodeIndexHtml(entry.Title, sectionOutput, childNavNodes);
                pages.Add(new SiteNode(
                    SourcePath: null,
                    RelativeSource: "",
                    RelativeOutput: sectionOutput,
                    Title: entry.Title,
                    Kind: SiteNodeKind.NodeIndex,
                    Overrides: SiteNode.NoOverrides,
                    GeneratedHtml: generatedHtml));

                navNodes.Add(new SiteNavNode(
                    entry.Title,
                    new SiteNavPage("Overview", sectionOutput),
                    childNavNodes));
            }
            else
            {
                navNodes.Add(new SiteNavNode(entry.Title, null, childNavNodes));
            }
        }
    }

    static void MarkPlaced(
        NavMapEntry entry,
        ISet<string> placedSources,
        IReadOnlyDictionary<string, DiscoveredPage> bySource)
    {
        if (!string.IsNullOrWhiteSpace(entry.Source))
        {
            if (!bySource.ContainsKey(entry.Source!))
                throw new InvalidOperationException($"The nav map references '{entry.Source}', but no such source document was discovered.");

            if (!placedSources.Add(entry.Source!))
                throw new InvalidOperationException($"The nav map places '{entry.Source}' more than once.");
        }

        foreach (var child in entry.Children)
            MarkPlaced(child, placedSources, bySource);
    }

    static SiteNode BuildSearchNode(
        MetadataIndex metadataIndex,
        IReadOnlyList<SiteNode> placedPages,
        ISet<string> usedOutputs)
    {
        var relativeOutput = Path.Combine("search", "index.html");
        EnsureOutputAvailable(relativeOutput, usedOutputs);

        var searchItems = placedPages
            .Where(page => page.SourcePath is not null)
            .Select(page =>
            {
                var metadata = metadataIndex.Get(page.RelativeSource);
                return new SearchItem(
                    metadata?.Type,
                    metadata?.Title ?? page.Title,
                    metadata?.Tags?.ToArray() ?? [],
                    metadata?.Summary,
                    ComputeRelativeHrefForGeneratedPage(relativeOutput, page.RelativeOutput));
            })
            .OrderBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var json = JsonSerializer.Serialize(
                searchItems,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })
            .Replace("</", "<\\/");
        var html = $$"""
            <h1>Search</h1>
            <p>Filter the placed documents by title, type, tags, or summary.</p>
            <input id="search-query" type="search" placeholder="Search..." style="width:100%;padding:0.7em;margin:1em 0;">
            <ul id="search-results"></ul>
            <script type="application/json" id="search-data">{{json}}</script>
            <script>
            (function () {
              var data = JSON.parse(document.getElementById('search-data').textContent || '[]');
              var query = document.getElementById('search-query');
              var results = document.getElementById('search-results');
              function render(items) {
                results.innerHTML = items.map(function (item) {
                  var tags = item.tags && item.tags.length ? ' <small>(' + item.tags.join(', ') + ')</small>' : '';
                  var type = item.type ? '<div><strong>' + item.type + '</strong></div>' : '';
                  var summary = item.summary ? '<div>' + item.summary + '</div>' : '';
                  return '<li style="margin:0.75em 0;"><a href="' + item.url + '">' + item.title + '</a>' + tags + type + summary + '</li>';
                }).join('');
              }
              function filter() {
                var term = (query.value || '').toLowerCase();
                if (!term) { render(data); return; }
                render(data.filter(function (item) {
                  var haystack = [item.title, item.type, item.summary, (item.tags || []).join(' ')].join(' ').toLowerCase();
                  return haystack.indexOf(term) >= 0;
                }));
              }
              query.addEventListener('input', filter);
              render(data);
            }());
            </script>
            """;

        return new SiteNode(
            SourcePath: null,
            RelativeSource: "",
            RelativeOutput: relativeOutput,
            Title: "Search",
            Kind: SiteNodeKind.Search,
            Overrides: SiteNode.NoOverrides,
            GeneratedHtml: html);
    }

    static string BuildNodeIndexHtml(string sectionTitle, string currentOutput, IReadOnlyList<SiteNavNode> children)
    {
        var sb = new StringBuilder();
        sb.Append("<h1>").Append(System.Net.WebUtility.HtmlEncode(sectionTitle)).AppendLine("</h1>");
        sb.AppendLine("<ul>");

        foreach (var child in children)
        {
            var target = child.Page?.RelativeOutput;
            if (target is null) continue;

            sb.Append("  <li><a href=\"")
                .Append(System.Net.WebUtility.HtmlEncode(ComputeRelativeHrefForGeneratedPage(currentOutput, target)))
                .Append("\">")
                .Append(System.Net.WebUtility.HtmlEncode(child.Title))
                .AppendLine("</a></li>");
        }

        sb.AppendLine("</ul>");
        return sb.ToString();
    }

    static IReadOnlyList<DiscoveredPage> DiscoverPages(IReadOnlyList<string> scanRoots, MetadataIndex metadataIndex)
    {
        var discovered = new List<DiscoveredPage>();
        var seenSources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var scanRoot in scanRoots)
        {
            foreach (var sourcePath in Directory.GetFiles(scanRoot, "*.md", SearchOption.AllDirectories).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                var relativeSource = Path.GetRelativePath(scanRoot, sourcePath);
                if (!seenSources.Add(relativeSource))
                    throw new InvalidOperationException($"The relative source path '{relativeSource}' appears in more than one scan root.");

                var title = ExtractTitle(sourcePath)
                    ?? FormatName(Path.GetFileNameWithoutExtension(relativeSource));
                var kind = ToOutputPath(relativeSource).EndsWith("index.html", StringComparison.OrdinalIgnoreCase)
                    ? SiteNodeKind.Landing
                    : SiteNodeKind.Document;

                discovered.Add(new DiscoveredPage(
                    scanRoot,
                    sourcePath,
                    relativeSource,
                    title,
                    kind,
                    ToOverrides(metadataIndex.Get(relativeSource))));
            }
        }

        return discovered;
    }

    static IReadOnlyDictionary<string, string> ToOverrides(DocumentMetadata? metadata)
    {
        if (metadata is null) return SiteNode.NoOverrides;

        var overrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        void AddIfPresent(string key, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value)) overrides[key] = value;
        }

        AddIfPresent("type", metadata.Type);
        AddIfPresent("title", metadata.Title);
        AddIfPresent("date", metadata.Date);
        AddIfPresent("state", metadata.State);
        if (metadata.Tags is { Count: > 0 }) overrides["tags"] = string.Join(", ", metadata.Tags);
        AddIfPresent("summary", metadata.Summary);
        AddIfPresent("perspective", metadata.Perspective);

        return overrides;
    }

    internal static string ToOutputPath(string relativeSourcePath)
    {
        var fileName = Path.GetFileName(relativeSourcePath);
        var dir = Path.GetDirectoryName(relativeSourcePath) ?? "";

        var outputFileName = Path.ChangeExtension(fileName, ".html");

        return dir.Length > 0 ? Path.Combine(dir, outputFileName) : outputFileName;
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
        var withoutNumber = Regex.Replace(folderName, @"^\d+[-_]?", "");
        return FormatName(withoutNumber);
    }

    internal static string FormatName(string name)
    {
        var words = name.Replace('-', ' ').Replace('_', ' ')
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(" ", words.Select(word => char.ToUpper(word[0]) + word[1..]));
    }

    static string Slugify(string value)
    {
        var normalized = Regex.Replace(value.Trim().ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
        return normalized.Length == 0 ? "section" : normalized;
    }

    static string ReserveSlug(IDictionary<string, int> reserved, string slug)
    {
        if (!reserved.TryGetValue(slug, out var count))
        {
            reserved[slug] = 1;
            return slug;
        }

        count++;
        reserved[slug] = count;
        return $"{slug}-{count}";
    }

    static string CombineOutput(string currentDirectory, string name) =>
        currentDirectory.Length == 0 ? name : Path.Combine(currentDirectory, name);

    static void EnsureOutputAvailable(string relativeOutput, ISet<string> usedOutputs)
    {
        if (!usedOutputs.Add(relativeOutput))
            throw new InvalidOperationException($"The output path '{relativeOutput}' is assigned more than once.");
    }

    static string ComputeRelativeHrefForGeneratedPage(string fromOutput, string toOutput)
    {
        const string root = "C:\\__site__";
        var fromDir = Path.GetDirectoryName(fromOutput) ?? "";
        var absFrom = fromDir.Length > 0 ? Path.Combine(root, fromDir) : root;
        var absTo = Path.Combine(root, toOutput);
        return Path.GetRelativePath(absFrom, absTo).Replace('\\', '/');
    }

    sealed record DiscoveredPage(
        string SourceRoot,
        string SourcePath,
        string RelativeSource,
        string Title,
        SiteNodeKind Kind,
        IReadOnlyDictionary<string, string> Overrides)
    {
        public SiteNode ToMappedSiteNode(string title, string relativeOutput, SiteNodeKind kind) =>
            new(SourcePath, RelativeSource, relativeOutput, title, kind, Overrides, SourceRoot);
    }

    sealed record SearchItem(
        string? Type,
        string Title,
        IReadOnlyList<string> Tags,
        string? Summary,
        string Url);
}
