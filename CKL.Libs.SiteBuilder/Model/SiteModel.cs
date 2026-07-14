namespace CKL.Libs.SiteBuilder.Model;

/// <summary>
/// The kind of a <see cref="SiteNode"/>. Only <see cref="Document"/> and
/// <see cref="Landing"/> are produced by the current assembler; <see cref="NodeIndex"/>
/// and <see cref="Search"/> are declared for the model shape but not yet synthesised
/// (they depend on the authoritative navigation map and search index, deferred to
/// plan 0014).
/// </summary>
internal enum SiteNodeKind
{
    /// <summary>An ordinary rendered markdown document.</summary>
    Document,

    /// <summary>A folder's landing page (its <c>README.md</c> / <c>_index.md</c>, or the site's home page).</summary>
    Landing,

    /// <summary>A synthetic per-folder index node. Not yet synthesised.</summary>
    NodeIndex,

    /// <summary>A synthetic search node. Not yet synthesised.</summary>
    Search
}

/// <summary>
/// A single node in the in-memory site model. <see cref="SourcePath"/> is the node's
/// source location (<c>null</c> for a future synthetic node with no backing source
/// file). <see cref="RelativeOutput"/> doubles as the node's navigation position,
/// combined with its place in the <see cref="SiteNavNode"/> tree. <see cref="Overrides"/>
/// is empty for every node produced by this plan — it is populated later by the
/// metadata-resolution pass / navigation map.
/// </summary>
internal sealed record SiteNode(
    string? SourcePath,
    string RelativeSource,
    string RelativeOutput,
    string Title,
    SiteNodeKind Kind,
    IReadOnlyDictionary<string, string> Overrides)
{
    internal static readonly IReadOnlyDictionary<string, string> NoOverrides =
        new Dictionary<string, string>();
}

/// <summary>The navigable entry point of a <see cref="SiteNavNode"/> — a folder's landing page or the home page.</summary>
internal sealed record SiteNavPage(string Title, string RelativeOutput);

/// <summary>A node in the folder-derived navigation tree, optionally pointing at a landing <see cref="SiteNode"/>.</summary>
internal sealed record SiteNavNode(
    string FolderPath,
    string Title,
    SiteNavPage? Landing,
    List<SiteNavNode> Children);

/// <summary>
/// The in-memory site model the assembler produces and the renderer consumes lazily.
/// <see cref="Pages"/> is the flat renderable set; <see cref="Nav"/> is the folder-derived
/// navigation tree. No staging folder is ever materialised on disk (ADR 0019 §2).
/// </summary>
internal sealed record SiteModel(IReadOnlyList<SiteNode> Pages, IReadOnlyList<SiteNavNode> Nav);
