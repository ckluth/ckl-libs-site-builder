using CKL.Libs.ResultPattern;

namespace CKL.Libs.SiteBuilder.Metadata;

/// <summary>
/// The resolution outcome for a single document: its fully resolved
/// <see cref="DocumentMetadata"/>, plus any defects found along the way (e.g. a
/// frontmatter value contradicting structure).
/// </summary>
internal sealed record MetadataResolution(DocumentMetadata Metadata, IReadOnlyList<string> Defects);

/// <summary>
/// Composes the metadata-resolution pass (ADR 0020 + ADR 0018): structure →
/// frontmatter overlay → an AI-inference seam call for the residue — enforcing
/// precedence itself. The seam is invoked only with the fields still empty after
/// structure + frontmatter, and any seam value for an already-set field is
/// discarded (a seam may never override — ADR 0020 §3). Resolution is
/// recomputed on every call, never cached (ADR 0018 Knob B).
/// </summary>
internal static class MetadataResolver
{
    public static Result<MetadataResolution> Resolve(
        string relativeSourcePath, string content, IMetadataInference inference)
    {
        try
        {
            var structural = StructuralExtractor.Extract(relativeSourcePath, content);
            var overlay = FrontmatterOverlay.Apply(structural, content);
            var resolvedSoFar = overlay.Metadata;

            var emptyFields = resolvedSoFar.EmptyFields;
            var inferred = emptyFields.Count > 0
                ? inference.Infer(resolvedSoFar, content, emptyFields)
                : new Dictionary<string, string>();

            var final = resolvedSoFar with
            {
                Type = FillOnlyIfEmpty(resolvedSoFar.Type, inferred, nameof(DocumentMetadata.Type)),
                Title = FillOnlyIfEmpty(resolvedSoFar.Title, inferred, nameof(DocumentMetadata.Title)),
                Date = FillOnlyIfEmpty(resolvedSoFar.Date, inferred, nameof(DocumentMetadata.Date)),
                State = FillOnlyIfEmpty(resolvedSoFar.State, inferred, nameof(DocumentMetadata.State)),
                Tags = resolvedSoFar.Tags is { Count: > 0 }
                    ? resolvedSoFar.Tags
                    : inferred.TryGetValue(nameof(DocumentMetadata.Tags), out var tagsValue) && !string.IsNullOrWhiteSpace(tagsValue)
                        ? tagsValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        : resolvedSoFar.Tags,
                Summary = FillOnlyIfEmpty(resolvedSoFar.Summary, inferred, nameof(DocumentMetadata.Summary)),
                Perspective = FillOnlyIfEmpty(resolvedSoFar.Perspective, inferred, nameof(DocumentMetadata.Perspective)),
            };

            return new MetadataResolution(final, overlay.Defects);
        }
        catch (Exception ex)
        {
            return Result<MetadataResolution>.Fail(ex);
        }
    }

    // A field already set by structure or frontmatter is never overwritten by the seam.
    static string? FillOnlyIfEmpty(string? current, IReadOnlyDictionary<string, string> inferred, string fieldName) =>
        !string.IsNullOrWhiteSpace(current)
            ? current
            : inferred.TryGetValue(fieldName, out var value) && !string.IsNullOrWhiteSpace(value) ? value : current;
}
