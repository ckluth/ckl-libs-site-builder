using CKL.Libs.ResultPattern;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace CKL.Libs.SiteBuilder.Configuration;

internal sealed record NavMap(IReadOnlyList<NavMapEntry> Entries);

internal enum SectionBehaviour { Expand, Overview }

internal static class SectionBehaviourParser
{
    public static SectionBehaviour? TryParse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        return value.Trim().ToLowerInvariant() switch
        {
            "expand" => SectionBehaviour.Expand,
            "overview" => SectionBehaviour.Overview,
            _ => throw new InvalidOperationException(
                $"The section behaviour value '{value}' is invalid; expected 'expand' or 'overview'.")
        };
    }

    public static string? ToYaml(SectionBehaviour? value) => value switch
    {
        null => null,
        SectionBehaviour.Expand => "expand",
        SectionBehaviour.Overview => "overview",
        _ => throw new InvalidOperationException($"Unhandled section behaviour '{value}'.")
    };
}

internal sealed record NavMapEntry
{
    public NavMapEntry(
        string Title,
        string? Source,
        IReadOnlyList<NavMapEntry> Children,
        bool Skip = false,
        bool Home = false,
        SectionBehaviour? Section = null,
        string? TitleFrom = null,
        IReadOnlyList<string>? Exclude = null,
        string? Intro = null)
    {
        this.Title = Title;
        this.Source = Source;
        this.Children = Children;
        this.Skip = Skip;
        this.Home = Home;
        this.Section = Section;
        this.TitleFrom = TitleFrom;
        this.Exclude = Exclude ?? [];
        this.Intro = Intro;
    }

    public string Title { get; init; }
    public string? Source { get; init; }
    public IReadOnlyList<NavMapEntry> Children { get; init; }
    public bool Skip { get; init; }
    public bool Home { get; init; }
    public SectionBehaviour? Section { get; init; }
    public string? TitleFrom { get; init; }
    public IReadOnlyList<string> Exclude { get; init; }
    public string? Intro { get; init; }
}

internal static class NavMapFile
{
    public static Result<NavMap> Read(string path)
    {
        try
        {
            if (!File.Exists(path))
                return Result<NavMap>.Fail(new FileNotFoundException("The nav map file was not found.", path));

            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();

            var dto = deserializer.Deserialize<NavMapYaml>(File.ReadAllText(path))
                ?? throw new InvalidOperationException("The nav map file is empty.");

            return new NavMap((dto.Nav ?? []).Select(MapEntry).ToArray());
        }
        catch (Exception ex)
        {
            return Result<NavMap>.Fail(ex);
        }
    }

    public static Result Write(string path, NavMap map)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            var serializer = new SerializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
                .Build();

            var dto = new NavMapYaml
            {
                Nav = map.Entries.Select(MapEntry).ToList()
            };

            File.WriteAllText(path, serializer.Serialize(dto));
            return Result.Success;
        }
        catch (Exception ex)
        {
            return Result.Fail(ex);
        }
    }

    static NavMapEntry MapEntry(NavMapEntryYaml entry)
    {
        var children = (entry.Children ?? []).Select(MapEntry).ToArray();
        var source = string.IsNullOrWhiteSpace(entry.Source) ? null : entry.Source!.Trim();
        var isWildcard = source is not null && (source.Contains('*') || source.Contains('?'));
        var home = entry.Home ?? false;
        var section = SectionBehaviourParser.TryParse(entry.Section);

        var isSingleFileEntry = source is not null && !isWildcard && children.Length == 0;
        if (string.IsNullOrWhiteSpace(entry.Title) && !isSingleFileEntry)
            throw new InvalidOperationException("Every nav map entry must define a title.");

        var title = string.IsNullOrWhiteSpace(entry.Title) ? "" : entry.Title.Trim();
        var titleFrom = string.IsNullOrWhiteSpace(entry.TitleFrom) ? null : entry.TitleFrom!.Trim();
        var intro = string.IsNullOrWhiteSpace(entry.Intro) ? null : entry.Intro.Trim();
        var exclude = (entry.Exclude ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .ToArray();

        if (source is not null && children.Length > 0)
            throw new InvalidOperationException(
                $"The nav map entry '{entry.Title}' cannot define both 'source' and 'children'.");

        if (home && source is null)
            throw new InvalidOperationException(
                $"The nav map entry '{entry.Title}' cannot be 'home: true' without a 'source'.");

        if (isWildcard && children.Length > 0)
            throw new InvalidOperationException(
                $"The nav map entry '{entry.Title}' cannot define both a wildcard 'source' and 'children'.");

        if (isWildcard && home)
            throw new InvalidOperationException(
                $"The nav map entry '{entry.Title}' cannot combine a wildcard 'source' with 'home: true'.");

        if (isWildcard && (entry.Skip ?? false))
            throw new InvalidOperationException(
                $"The nav map entry '{entry.Title}' cannot combine a wildcard 'source' with 'skip: true'.");

        if (titleFrom is not null && !titleFrom.Equals("headline", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"The nav map entry '{entry.Title}' has invalid 'titleFrom: {entry.TitleFrom}'; expected 'headline'.");

        if (!isWildcard && titleFrom is not null)
            throw new InvalidOperationException(
                $"The nav map entry '{entry.Title}' cannot define 'titleFrom' unless 'source' is a wildcard.");

        if (!isWildcard && exclude.Length > 0)
            throw new InvalidOperationException(
                $"The nav map entry '{entry.Title}' cannot define 'exclude' unless 'source' is a wildcard.");

        if (source is not null && !isWildcard && intro is not null)
            throw new InvalidOperationException(
                $"The nav map entry '{entry.Title}' cannot define 'intro' because intro is only valid on a section or wildcard entry.");

        return new NavMapEntry(title, source, children, entry.Skip ?? false, home, section, titleFrom, exclude, intro);
    }

    static NavMapEntryYaml MapEntry(NavMapEntry entry) =>
        new()
        {
            Title = entry.Title,
            Source = entry.Source,
            Children = entry.Children.Count == 0 ? null : entry.Children.Select(MapEntry).ToList(),
            Skip = entry.Skip ? true : null,
            Home = entry.Home ? true : null,
            Section = SectionBehaviourParser.ToYaml(entry.Section),
            TitleFrom = entry.TitleFrom,
            Exclude = entry.Exclude.Count == 0 ? null : entry.Exclude.ToList(),
            Intro = entry.Intro
        };

    sealed class NavMapYaml
    {
        public List<NavMapEntryYaml>? Nav { get; set; }
    }

    sealed class NavMapEntryYaml
    {
        public string? Title { get; set; }
        public string? Source { get; set; }
        public List<NavMapEntryYaml>? Children { get; set; }
        public bool? Skip { get; set; }
        public bool? Home { get; set; }
        public string? Section { get; set; }
        public string? TitleFrom { get; set; }
        public List<string>? Exclude { get; set; }
        public string? Intro { get; set; }
    }
}
