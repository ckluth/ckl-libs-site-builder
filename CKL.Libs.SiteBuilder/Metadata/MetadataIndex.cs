using CKL.Libs.ResultPattern;

namespace CKL.Libs.SiteBuilder.Metadata;

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

    public IReadOnlyDictionary<string, DocumentMetadata> Entries => _byRelativeSource;

    public DocumentMetadata? Get(string relativeSourcePath) =>
        _byRelativeSource.TryGetValue(relativeSourcePath, out var metadata) ? metadata : null;

    public IReadOnlyList<string> DefectsFor(string relativeSourcePath) =>
        _defectsByRelativeSource.TryGetValue(relativeSourcePath, out var defects) ? defects : [];

    public static Result<MetadataIndex> Build(string sourceDir, IMetadataInference inference) =>
        Build([sourceDir], inference);

    public static Result<MetadataIndex> Build(IReadOnlyList<string> sourceDirs, IMetadataInference inference)
    {
        try
        {
            var byRelativeSource = new Dictionary<string, DocumentMetadata>(StringComparer.OrdinalIgnoreCase);
            var defectsByRelativeSource = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var sourceDir in sourceDirs)
            {
                foreach (var sourcePath in Directory.GetFiles(sourceDir, "*.md", SearchOption.AllDirectories))
                {
                    var relativeSourcePath = Path.GetRelativePath(sourceDir, sourcePath);
                    if (byRelativeSource.ContainsKey(relativeSourcePath))
                    {
                        throw new InvalidOperationException(
                            $"The relative source path '{relativeSourcePath}' appears in more than one scan root.");
                    }

                    var content = File.ReadAllText(sourcePath);

                    var resolved = MetadataResolver.Resolve(relativeSourcePath, content, inference);
                    if (!resolved.Succeeded) return resolved.ToResult<MetadataIndex>();

                    byRelativeSource[relativeSourcePath] = resolved.Value.Metadata;
                    if (resolved.Value.Defects.Count > 0)
                        defectsByRelativeSource[relativeSourcePath] = resolved.Value.Defects;
                }
            }

            return new MetadataIndex(byRelativeSource, defectsByRelativeSource);
        }
        catch (Exception ex)
        {
            return Result<MetadataIndex>.Fail(ex);
        }
    }
}
