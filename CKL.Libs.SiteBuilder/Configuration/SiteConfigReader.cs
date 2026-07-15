using CKL.Libs.ResultPattern;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace CKL.Libs.SiteBuilder.Configuration;

internal static class SiteConfigReader
{
    public static Result<SiteConfig> Read(string configPath)
    {
        try
        {
            if (!File.Exists(configPath))
                return Result<SiteConfig>.Fail(new FileNotFoundException("The site config file was not found.", configPath));

            var yaml = File.ReadAllText(configPath);
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();

            var dto = deserializer.Deserialize<SiteConfigYaml>(yaml)
                ?? throw new InvalidOperationException("The site config file is empty.");

            if (dto.ScanRoots is null || dto.ScanRoots.Count == 0)
                throw new InvalidOperationException("The site config must define at least one scan root.");

            var configDirectory = Path.GetDirectoryName(Path.GetFullPath(configPath))
                ?? Directory.GetCurrentDirectory();

            var scanRoots = dto.ScanRoots
                .Select(path => ResolvePath(configDirectory, path, "scan root"))
                .ToArray();

            var title = string.IsNullOrWhiteSpace(dto.Title)
                ? Path.GetFileName(scanRoots[0].TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
                : dto.Title.Trim();

            var outputDirectory = ResolvePath(configDirectory, dto.Output ?? @".\_site");

            var stylesheetPath = string.IsNullOrWhiteSpace(dto.Theme?.Stylesheet)
                ? null
                : ResolvePath(configDirectory, dto.Theme.Stylesheet!, "stylesheet");

            var mermaidTheme = string.IsNullOrWhiteSpace(dto.Theme?.Mermaid)
                ? SiteThemeConfig.DefaultMermaidTheme
                : dto.Theme.Mermaid!.Trim();

            var navMapPath = string.IsNullOrWhiteSpace(dto.Nav)
                ? null
                : ResolvePath(configDirectory, dto.Nav!, "nav map");

            return new SiteConfig(title, outputDirectory, scanRoots, new SiteThemeConfig(stylesheetPath, mermaidTheme), navMapPath);
        }
        catch (Exception ex)
        {
            return Result<SiteConfig>.Fail(ex);
        }
    }

    static string ResolvePath(string configDirectory, string path, string label = "path")
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new InvalidOperationException($"The site config {label} cannot be empty.");

        return Path.GetFullPath(Path.IsPathRooted(path)
            ? path
            : Path.Combine(configDirectory, path));
    }

    sealed class SiteConfigYaml
    {
        public string? Title { get; set; }
        public string? Output { get; set; }
        public List<string>? ScanRoots { get; set; }
        public SiteThemeYaml? Theme { get; set; }
        public string? Nav { get; set; }
    }

    sealed class SiteThemeYaml
    {
        public string? Stylesheet { get; set; }
        public string? Mermaid { get; set; }
    }
}
