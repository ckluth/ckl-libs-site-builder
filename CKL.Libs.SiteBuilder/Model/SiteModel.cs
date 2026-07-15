namespace CKL.Libs.SiteBuilder.Model;

internal enum SiteNodeKind
{
    Document,
    Landing,
    NodeIndex,
    Search
}

internal sealed record SiteNode(
    string? SourcePath,
    string RelativeSource,
    string RelativeOutput,
    string Title,
    SiteNodeKind Kind,
    IReadOnlyDictionary<string, string> Overrides,
    string SourceRoot = "",
    string? GeneratedHtml = null)
{
    internal static readonly IReadOnlyDictionary<string, string> NoOverrides =
        new Dictionary<string, string>();
}

internal sealed record SiteNavPage(string Title, string RelativeOutput);

internal sealed record SiteNavNode(
    string Title,
    SiteNavPage? Page,
    List<SiteNavNode> Children);

internal sealed record SiteModel(IReadOnlyList<SiteNode> Pages, IReadOnlyList<SiteNavNode> Nav);
