using CKL.Libs.ResultPattern;

namespace CKL.Libs.SiteBuilder.Metadata;

/// <summary>
/// The in-memory metadata index (R-08's in-memory half; R-13): resolves every
/// discovered markdown document under a scan root into its
/// <see cref="DocumentMetadata"/>, built fresh on every call and never written to
/// disk — no <c>docs-index.json</c>, no persisted store of any kind.
/// </summary>
internal sealed class MetadataIndex
{
    readonly IReadOnlyDictionary<string, DocumentMetadata> _byRelativeSource;
    readonly IReadOnlyDictionary<string, IReadOnlyList<string>> _defectsByRelativeSource;

    MetadataIndex(
        IReadOnlyDictionary<string, DocumentMetadata> byRelativeSource,
        IReadOnlyDictionary<string, IReadOnlyList<string>> defectsByRelativeSource)
    {
        _byRelativeSource = byRelativeSource;
        _defectsByRelativeSource = defectsByRelativeSource;
    }

    /// <summary>The resolved metadata for a document, keyed by its path relative to the scan root.</summary>
    public DocumentMetadata? Get(string relativeSourcePath) =>
        _byRelativeSource.TryGetValue(relativeSourcePath, out var metadata) ? metadata : null;

    /// <summary>Any defects surfaced while resolving a document (e.g. frontmatter contradicting structure).</summary>
    public IReadOnlyList<string> DefectsFor(string relativeSourcePath) =>
        _defectsByRelativeSource.TryGetValue(relativeSourcePath, out var defects) ? defects : [];

    /// <summary>
    /// Builds the index by resolving every <c>*.md</c> file under <paramref name="sourceDir"/>.
    /// Recomputed fresh on every call (ADR 0018 Knob B) — never cached or persisted.
    /// </summary>
    public static Result<MetadataIndex> Build(string sourceDir, IMetadataInference inference)
    {
        try
        {
            var byRelativeSource = new Dictionary<string, DocumentMetadata>();
            var defectsByRelativeSource = new Dictionary<string, IReadOnlyList<string>>();

            foreach (var sourcePath in Directory.GetFiles(sourceDir, "*.md", SearchOption.AllDirectories))
            {
                var relativeSourcePath = Path.GetRelativePath(sourceDir, sourcePath);
                var content = File.ReadAllText(sourcePath);

                var resolved = MetadataResolver.Resolve(relativeSourcePath, content, inference);
                if (!resolved.Succeeded) return resolved.ToResult<MetadataIndex>();

                byRelativeSource[relativeSourcePath] = resolved.Value.Metadata;
                if (resolved.Value.Defects.Count > 0)
                    defectsByRelativeSource[relativeSourcePath] = resolved.Value.Defects;
            }

            return new MetadataIndex(byRelativeSource, defectsByRelativeSource);
        }
        catch (Exception ex)
        {
            return Result<MetadataIndex>.Fail(ex);
        }
    }
}
