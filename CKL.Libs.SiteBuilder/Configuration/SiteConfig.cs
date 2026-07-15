namespace CKL.Libs.SiteBuilder.Configuration;

internal sealed record SiteConfig(
    string Title,
    string OutputDirectory,
    IReadOnlyList<string> ScanRoots,
    SiteThemeConfig Theme,
    string? NavMapPath);

internal sealed record SiteThemeConfig(
    string? StylesheetPath,
    string MermaidTheme)
{
    internal const string DefaultMermaidTheme = "dark";
}
