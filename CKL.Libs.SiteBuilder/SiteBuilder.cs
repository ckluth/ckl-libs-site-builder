using System.Text;
using CKL.Libs.ResultPattern;
using CKL.Libs.SiteBuilder.Assembly;
using CKL.Libs.SiteBuilder.Metadata;
using CKL.Libs.SiteBuilder.Rendering;

namespace CKL.Libs.SiteBuilder;

/// <summary>
/// Options controlling a <see cref="SiteBuilder"/> build. Navigation is currently
/// derived from source folder structure (the authoritative navigation map lands in
/// the config/CLI plan).
/// </summary>
/// <param name="SourceDirectory">The root directory to scan for markdown.</param>
/// <param name="OutputDirectory">The directory the rendered site is written to.</param>
/// <param name="SiteTitle">
/// The site title shown in the navigation header and page titles. Defaults to a
/// formatted version of <paramref name="SourceDirectory"/>'s folder name when null.
/// </param>
/// <param name="ShowOrigin">Whether an origin comment at the top of a document is rendered as a byline.</param>
/// <param name="MetadataInference">
/// The AI-inference seam (ADR 0020 §3) consulted only for metadata fields
/// structure and frontmatter leave empty. Defaults to
/// <see cref="NoOpMetadataInference"/>, so a build runs fully offline and
/// deterministically unless the caller injects a real implementation.
/// </param>
public sealed record SiteBuilderOptions(
    string SourceDirectory,
    string OutputDirectory,
    string? SiteTitle = null,
    bool ShowOrigin = true,
    IMetadataInference? MetadataInference = null);

/// <summary>
/// The public entry point of the SiteBuilder pipeline: assembles an in-memory site
/// model from <see cref="SiteBuilderOptions.SourceDirectory"/> and renders it to
/// <see cref="SiteBuilderOptions.OutputDirectory"/>. The assemble/render stages are
/// internal implementation detail — this is the only public surface.
/// </summary>
public static class SiteBuilder
{
    /// <summary>Builds the site described by <paramref name="options"/>.</summary>
    public static Result Build(SiteBuilderOptions options)
    {
        try
        {
            Directory.CreateDirectory(options.OutputDirectory);

            var model = SiteAssembler.Assemble(options.SourceDirectory, options.MetadataInference);
            if (!model.Succeeded) return model.ToResult();

            var siteTitle = options.SiteTitle
                ?? SiteAssembler.FormatName(Path.GetFileName(
                    options.SourceDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)));

            foreach (var page in model.Value.Pages)
            {
                var html = PageRenderer.Render(page, model.Value.Nav, options.SourceDirectory, siteTitle, options.ShowOrigin);
                var outputPath = Path.Combine(options.OutputDirectory, page.RelativeOutput);
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
                File.WriteAllText(outputPath, html, Encoding.UTF8);
            }

            File.WriteAllText(Path.Combine(options.OutputDirectory, "site.css"), HtmlTemplate.Css);

            var mermaidSrc = Path.Combine(AppContext.BaseDirectory, "vendor", "mermaid.min.js");
            if (File.Exists(mermaidSrc))
                File.Copy(mermaidSrc, Path.Combine(options.OutputDirectory, "mermaid.min.js"), overwrite: true);

            CopyNonMarkdownAssets(options.SourceDirectory, options.OutputDirectory);

            return Result.Success;
        }
        catch (Exception ex)
        {
            return ex;
        }
    }

    // R-03: non-markdown source assets are copied straight to the output at their mirrored path.
    static void CopyNonMarkdownAssets(string sourceDir, string outputDir)
    {
        foreach (var sourcePath in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            if (sourcePath.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                continue;

            var relativePath = Path.GetRelativePath(sourceDir, sourcePath);
            var destinationPath = Path.Combine(outputDir, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            File.Copy(sourcePath, destinationPath, overwrite: true);
        }
    }
}
