using System.Text;
using CKL.Libs.ResultPattern;
using CKL.Libs.SiteBuilder.Assembly;
using CKL.Libs.SiteBuilder.Configuration;
using CKL.Libs.SiteBuilder.Metadata;
using CKL.Libs.SiteBuilder.Rendering;

namespace CKL.Libs.SiteBuilder;

/// <summary>The observable result of a config-driven site build.</summary>
/// <param name="OutputDirectory">The directory the build wrote to.</param>
/// <param name="UnplacedDocuments">Any discovered source documents absent from the authoritative nav map.</param>
public sealed record SiteBuildReport(
    string OutputDirectory,
    IReadOnlyList<string> UnplacedDocuments);

/// <summary>Options controlling a SiteBuilder run.</summary>
/// <param name="SourceDirectory">The legacy single scan root for direct-options builds.</param>
/// <param name="OutputDirectory">The directory the rendered site is written to.</param>
/// <param name="SiteTitle">The site title shown in page titles and the navigation header.</param>
/// <param name="ShowOrigin">Whether an origin comment at the top of a document is rendered as a byline.</param>
/// <param name="MetadataInference">The optional metadata-inference seam; defaults to the deterministic no-op implementation when omitted.</param>
/// <param name="ScanRoots">The configured scan roots; when omitted, <paramref name="SourceDirectory"/> is used.</param>
/// <param name="StylesheetPath">An optional stylesheet whose contents replace the built-in CSS.</param>
/// <param name="MermaidTheme">The Mermaid theme name emitted into the rendered template.</param>
public sealed record SiteBuilderOptions(
    string SourceDirectory,
    string OutputDirectory,
    string? SiteTitle = null,
    bool ShowOrigin = true,
    IMetadataInference? MetadataInference = null,
    IReadOnlyList<string>? ScanRoots = null,
    string? StylesheetPath = null,
    string MermaidTheme = "dark")
{
    internal IReadOnlyList<string> EffectiveScanRoots =>
        ScanRoots is { Count: > 0 } ? ScanRoots : [SourceDirectory];
}

/// <summary>The public entry point of the SiteBuilder pipeline.</summary>
public static class SiteBuilder
{
    /// <summary>Builds a site from direct options, preserving the legacy single-root calling shape.</summary>
    public static Result Build(SiteBuilderOptions options)
    {
        var build = BuildCore(options, null);
        if (!build.Succeeded) return build.ToResult();
        return Result.Success;
    }

    /// <summary>Builds a site from a YAML config file and returns any drift report surfaced during the run.</summary>
    public static Result<SiteBuildReport> Build(
        string configPath,
        bool showOrigin = true,
        IMetadataInference? metadataInference = null)
    {
        try
        {
            var config = SiteConfigReader.Read(configPath);
            if (!config.Succeeded) return config.ToResult<SiteBuildReport>();

            if (config.Value.NavMapPath is not null)
            {
                var scaffold = NavMapScaffolder.EnsureExists(
                    config.Value.NavMapPath,
                    config.Value.ScanRoots,
                    metadataInference ?? NoOpMetadataInference.Instance);
                if (!scaffold.Succeeded) return scaffold.ToResult<SiteBuildReport>();
            }

            NavMap? navMap = null;
            if (config.Value.NavMapPath is not null)
            {
                var nav = NavMapFile.Read(config.Value.NavMapPath);
                if (!nav.Succeeded) return nav.ToResult<SiteBuildReport>();
                navMap = nav.Value;
            }

            var options = new SiteBuilderOptions(
                SourceDirectory: config.Value.ScanRoots[0],
                OutputDirectory: config.Value.OutputDirectory,
                SiteTitle: config.Value.Title,
                ShowOrigin: showOrigin,
                MetadataInference: metadataInference,
                ScanRoots: config.Value.ScanRoots,
                StylesheetPath: config.Value.Theme.StylesheetPath,
                MermaidTheme: config.Value.Theme.MermaidTheme);

            return BuildCore(options, navMap);
        }
        catch (Exception ex)
        {
            return Result<SiteBuildReport>.Fail(ex);
        }
    }

    static Result<SiteBuildReport> BuildCore(SiteBuilderOptions options, NavMap? navMap)
    {
        try
        {
            Directory.CreateDirectory(options.OutputDirectory);

            var assembly = SiteAssembler.AssembleConfigured(options.EffectiveScanRoots, navMap, options.MetadataInference);
            if (!assembly.Succeeded) return assembly.ToResult<SiteBuildReport>();

            var model = assembly.Value.Site;
            var siteTitle = options.SiteTitle
                ?? SiteAssembler.FormatName(Path.GetFileName(
                    options.EffectiveScanRoots[0].TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)));
            var outputMap = model.Pages
                .Where(page => page.SourcePath is not null)
                .ToDictionary(page => page.RelativeSource, page => page.RelativeOutput, StringComparer.OrdinalIgnoreCase);

            foreach (var page in model.Pages)
            {
                var html = PageRenderer.Render(
                    page,
                    model.Nav,
                    options.EffectiveScanRoots[0],
                    siteTitle,
                    options.ShowOrigin,
                    options.MermaidTheme,
                    outputMap);
                var outputPath = Path.Combine(options.OutputDirectory, page.RelativeOutput);
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
                File.WriteAllText(outputPath, html, Encoding.UTF8);
            }

            var stylesheet = options.StylesheetPath is null
                ? HtmlTemplate.Css
                : File.ReadAllText(options.StylesheetPath);
            File.WriteAllText(Path.Combine(options.OutputDirectory, "site.css"), stylesheet);

            var mermaidSrc = Path.Combine(AppContext.BaseDirectory, "vendor", "mermaid.min.js");
            if (File.Exists(mermaidSrc))
                File.Copy(mermaidSrc, Path.Combine(options.OutputDirectory, "mermaid.min.js"), overwrite: true);

            CopyNonMarkdownAssets(options.EffectiveScanRoots, options.OutputDirectory);

            return new SiteBuildReport(options.OutputDirectory, assembly.Value.UnplacedDocuments);
        }
        catch (Exception ex)
        {
            return Result<SiteBuildReport>.Fail(ex);
        }
    }

    static void CopyNonMarkdownAssets(IReadOnlyList<string> sourceRoots, string outputDir)
    {
        var copiedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var sourceRoot in sourceRoots)
        {
            foreach (var sourcePath in Directory.GetFiles(sourceRoot, "*", SearchOption.AllDirectories))
            {
                if (sourcePath.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (IsUnderDirectory(sourcePath, outputDir))
                    continue;

                var relativePath = Path.GetRelativePath(sourceRoot, sourcePath);
                if (!copiedPaths.Add(relativePath))
                    throw new InvalidOperationException(
                        $"The non-markdown asset '{relativePath}' appears in more than one scan root.");

                var destinationPath = Path.Combine(outputDir, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
                File.Copy(sourcePath, destinationPath, overwrite: true);
            }
        }

        static bool IsUnderDirectory(string path, string directory)
        {
            var fullPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var fullDirectory = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return fullPath.StartsWith(fullDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || fullPath.Equals(fullDirectory, StringComparison.OrdinalIgnoreCase);
        }
    }
}
