using System.Text;
using System.Text.RegularExpressions;
using CKL.Libs.ResultPattern;
using CKL.Libs.SiteBuilder.Assembly;
using CKL.Libs.SiteBuilder.Configuration;
using CKL.Libs.SiteBuilder.Metadata;
using CKL.Libs.SiteBuilder.Rendering;

namespace CKL.Libs.SiteBuilder;

/// <summary>The observable result of a config-driven site build.</summary>
/// <param name="OutputDirectory">The directory the build wrote to.</param>
/// <param name="UnplacedDocuments">Any discovered source documents absent from the authoritative nav map.</param>
/// <param name="Warnings">Non-fatal build warnings surfaced during assembly.</param>
public sealed record SiteBuildReport(
    string OutputDirectory,
    IReadOnlyList<string> UnplacedDocuments,
    IReadOnlyList<string> Warnings);

/// <summary>Options controlling a SiteBuilder run.</summary>
/// <param name="SourceDirectory">The legacy single scan root for direct-options builds.</param>
/// <param name="OutputDirectory">The directory the rendered site is written to.</param>
/// <param name="SiteTitle">The site title shown in page titles and the navigation header.</param>
/// <param name="ShowOrigin">Whether an origin comment at the top of a document is rendered as a byline.</param>
/// <param name="MetadataInference">The optional metadata-inference seam; defaults to the deterministic no-op implementation when omitted.</param>
/// <param name="ScanRoots">The configured scan roots; when omitted, <paramref name="SourceDirectory"/> is used.</param>
/// <param name="StylesheetPath">An optional stylesheet whose contents replace the built-in CSS.</param>
/// <param name="MermaidTheme">The Mermaid theme name emitted into the rendered template.</param>
/// <param name="Intro">Optional markdown rendered above the synthesised landing page listing.</param>
public sealed record SiteBuilderOptions(
    string SourceDirectory,
    string OutputDirectory,
    string? SiteTitle = null,
    bool ShowOrigin = true,
    IMetadataInference? MetadataInference = null,
    IReadOnlyList<string>? ScanRoots = null,
    string? StylesheetPath = null,
    string MermaidTheme = "dark",
    string? Intro = null)
{
    internal IReadOnlyList<string> EffectiveScanRoots =>
        ScanRoots is { Count: > 0 } ? ScanRoots : [SourceDirectory];
}

/// <summary>The public entry point of the SiteBuilder pipeline.</summary>
public static class SiteBuilder
{
    /// <summary>Builds a site from direct options, preserving the legacy single-root calling shape.
    /// Warnings are not surfaced through this legacy overload; use the config-driven overload to observe them.</summary>
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

            var navMapPath = config.Value.NavMapPath
                ?? Path.Combine(Directory.GetCurrentDirectory(), "nav.yml");

            var scaffold = NavMapScaffolder.EnsureExists(
                navMapPath,
                config.Value.ScanRoots,
                metadataInference ?? NoOpMetadataInference.Instance);
            if (!scaffold.Succeeded) return scaffold.ToResult<SiteBuildReport>();

            var nav = NavMapFile.Read(navMapPath);
            if (!nav.Succeeded) return nav.ToResult<SiteBuildReport>();
            NavMap navMap = nav.Value;

            var options = new SiteBuilderOptions(
                SourceDirectory: config.Value.ScanRoots[0],
                OutputDirectory: config.Value.OutputDirectory,
                SiteTitle: config.Value.Title,
                ShowOrigin: showOrigin,
                MetadataInference: metadataInference,
                ScanRoots: config.Value.ScanRoots,
                StylesheetPath: config.Value.Theme.StylesheetPath,
                MermaidTheme: config.Value.Theme.MermaidTheme,
                Intro: config.Value.Intro);

            return BuildCore(options, navMap, config.Value.SectionBehaviour, config.Value.AssetExcludes, config.Value.Intro);
        }
        catch (Exception ex)
        {
            return Result<SiteBuildReport>.Fail(ex);
        }
    }

    static Result<SiteBuildReport> BuildCore(
        SiteBuilderOptions options,
        NavMap? navMap,
        SectionBehaviour sectionBehaviour = SectionBehaviour.Expand,
        IReadOnlyList<string>? assetExcludes = null,
        string? siteIntro = null)
    {
        try
        {
            var prepare = PrepareOutputDirectory(options.OutputDirectory);
            if (!prepare.Succeeded) return prepare.ToResult<SiteBuildReport>();

            NavMap effectiveNavMap;
            if (navMap is not null)
            {
                effectiveNavMap = navMap;
            }
            else
            {
                var scaffold = NavMapScaffolder.BuildInMemory(
                    options.EffectiveScanRoots,
                    options.MetadataInference ?? NoOpMetadataInference.Instance);
                if (!scaffold.Succeeded) return scaffold.ToResult<SiteBuildReport>();
                effectiveNavMap = scaffold.Value;
            }

            var assembly = SiteAssembler.AssembleConfigured(
                options.EffectiveScanRoots,
                effectiveNavMap,
                options.MetadataInference,
                sectionBehaviour,
                siteIntro ?? options.Intro);
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

            CopyNonMarkdownAssets(options.EffectiveScanRoots, options.OutputDirectory, assetExcludes ?? []);

            return new SiteBuildReport(options.OutputDirectory, assembly.Value.UnplacedDocuments, assembly.Value.Warnings);
        }
        catch (Exception ex)
        {
            return Result<SiteBuildReport>.Fail(ex);
        }
    }

    const string OutputMarkerFileName = ".sitebuilder";

    static readonly string[] DefaultIgnoredDirectoryNames =
        [".git", ".vs", ".vscode", ".idea", "bin", "obj", "node_modules"];

    /// <summary>Ensures the output directory is safe to write into: creates it, accepts it if empty,
    /// cleans it if it carries this tool's marker from a prior run, and refuses to touch unrecognised data.</summary>
    static Result PrepareOutputDirectory(string outputDirectory)
    {
        try
        {
            if (!Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
                WriteOutputMarker(outputDirectory);
                return Result.Success;
            }

            var entries = Directory.GetFileSystemEntries(outputDirectory);
            if (entries.Length == 0)
            {
                WriteOutputMarker(outputDirectory);
                return Result.Success;
            }

            var markerPath = Path.Combine(outputDirectory, OutputMarkerFileName);
            if (!File.Exists(markerPath))
            {
                return Result.Fail(new InvalidOperationException(
                    $"The output directory '{outputDirectory}' is not empty and was not produced by a prior SiteBuilder run " +
                    $"(no '{OutputMarkerFileName}' marker found); refusing to delete unrecognised data."));
            }

            foreach (var entry in entries)
            {
                if (Directory.Exists(entry)) Directory.Delete(entry, recursive: true);
                else File.Delete(entry);
            }

            WriteOutputMarker(outputDirectory);
            return Result.Success;
        }
        catch (Exception ex)
        {
            return Result.Fail(ex);
        }
    }

    static void WriteOutputMarker(string outputDirectory) =>
        File.WriteAllText(Path.Combine(outputDirectory, OutputMarkerFileName), string.Empty);

    static void CopyNonMarkdownAssets(IReadOnlyList<string> sourceRoots, string outputDir, IReadOnlyList<string> additionalExcludes)
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

                if (IsExcludedAsset(relativePath, additionalExcludes))
                    continue;

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

    static bool IsExcludedAsset(string relativePath, IReadOnlyList<string> additionalExcludes)
    {
        var segments = relativePath.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);

        foreach (var segment in segments)
        {
            if (segment.StartsWith('.'))
                return true;

            if (DefaultIgnoredDirectoryNames.Contains(segment, StringComparer.OrdinalIgnoreCase))
                return true;

            foreach (var pattern in additionalExcludes)
            {
                if (MatchesGlob(segment, pattern))
                    return true;
            }
        }

        return false;
    }

    static bool MatchesGlob(string value, string pattern)
    {
        if (!pattern.Contains('*') && !pattern.Contains('?'))
            return value.Equals(pattern, StringComparison.OrdinalIgnoreCase);

        var regexPattern = "^" + Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$";
        return Regex.IsMatch(value, regexPattern, RegexOptions.IgnoreCase);
    }
}
