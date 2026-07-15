namespace CKL.Libs.SiteBuilder.Metadata;

/// <summary>
/// The narrow AI-inference seam (ADR 0020 §3): fills only the metadata fields
/// structure and frontmatter left empty ("the residue"). An implementation must
/// never attempt to override a field the caller reports as already resolved —
/// precedence is enforced by <see cref="MetadataResolver"/>, not entrusted to
/// this port. Injected by the caller via <c>SiteBuilderOptions</c>; the library
/// ships only <see cref="NoOpMetadataInference"/> as its default.
/// </summary>
public interface IMetadataInference
{
    /// <summary>
    /// Infers values for <paramref name="emptyFields"/> only, given the
    /// resolved-so-far metadata and the document's content (or a bounded
    /// excerpt). Returns a value only for fields it can actually fill; a field
    /// left out of the result stays empty.
    /// </summary>
    /// <param name="resolvedSoFar">The metadata resolved by structure + frontmatter.</param>
    /// <param name="content">The document's content (or a bounded excerpt).</param>
    /// <param name="emptyFields">The field names still empty (see <see cref="DocumentMetadata.EmptyFields"/>).</param>
    IReadOnlyDictionary<string, string> Infer(DocumentMetadata resolvedSoFar, string content, IReadOnlyList<string> emptyFields);
}
