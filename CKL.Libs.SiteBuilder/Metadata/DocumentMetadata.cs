namespace CKL.Libs.SiteBuilder.Metadata;

/// <summary>
/// A document's fully resolved metadata (ADR 0020 §1–§3): the fixed vocabulary
/// <c>type</c>, <c>title</c>, <c>date</c>, <c>state</c>, <c>tags</c>,
/// <c>summary</c>, <c>perspective</c>. Every field is nullable/empty when no
/// stage (structure, frontmatter, AI) resolved it. Public because it appears in
/// the public <see cref="IMetadataInference"/> seam the caller implements.
/// </summary>
public sealed record DocumentMetadata(
    string? Type = null,
    string? Title = null,
    string? Date = null,
    string? State = null,
    IReadOnlyList<string>? Tags = null,
    string? Summary = null,
    string? Perspective = null)
{
    /// <summary>An empty metadata value — every field unresolved.</summary>
    internal static readonly DocumentMetadata Empty = new();

    /// <summary>The field names this instance still leaves empty — the residue a later stage may fill.</summary>
    internal IReadOnlyList<string> EmptyFields =>
        [.. EmptyFieldChecks.Where(f => f.IsEmpty).Select(f => f.Name)];

    IEnumerable<(string Name, bool IsEmpty)> EmptyFieldChecks =>
    [
        (nameof(Type), string.IsNullOrWhiteSpace(Type)),
        (nameof(Title), string.IsNullOrWhiteSpace(Title)),
        (nameof(Date), string.IsNullOrWhiteSpace(Date)),
        (nameof(State), string.IsNullOrWhiteSpace(State)),
        (nameof(Tags), Tags is null || Tags.Count == 0),
        (nameof(Summary), string.IsNullOrWhiteSpace(Summary)),
        (nameof(Perspective), string.IsNullOrWhiteSpace(Perspective)),
    ];
}
