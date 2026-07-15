namespace CKL.Libs.SiteBuilder.Metadata;

/// <summary>
/// The default, deterministic no-op AI-inference implementation (ADR 0020 §3):
/// returns no values, so the metadata-resolution pass runs fully offline and
/// deterministically. Used unless the caller injects a real implementation via
/// <c>SiteBuilderOptions</c>.
/// </summary>
public sealed class NoOpMetadataInference : IMetadataInference
{
    /// <summary>The shared, stateless no-op instance.</summary>
    public static readonly NoOpMetadataInference Instance = new();

    /// <inheritdoc />
    public IReadOnlyDictionary<string, string> Infer(DocumentMetadata resolvedSoFar, string content, IReadOnlyList<string> emptyFields) =>
        new Dictionary<string, string>();
}
