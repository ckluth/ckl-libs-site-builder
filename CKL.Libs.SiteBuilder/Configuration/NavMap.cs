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

internal sealed record NavMapEntry(
    string Title,
    string? Source,
    IReadOnlyList<NavMapEntry> Children,
    bool Skip = false,
    bool Home = false,
    SectionBehaviour? Section = null);

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
        if (string.IsNullOrWhiteSpace(entry.Title))
            throw new InvalidOperationException("Every nav map entry must define a title.");

        var children = (entry.Children ?? []).Select(MapEntry).ToArray();
        var source = string.IsNullOrWhiteSpace(entry.Source) ? null : entry.Source!.Trim();
        var home = entry.Home ?? false;
        var section = SectionBehaviourParser.TryParse(entry.Section);

        if (source is not null && children.Length > 0)
            throw new InvalidOperationException(
                $"The nav map entry '{entry.Title}' cannot define both 'source' and 'children'.");

        if (home && source is null)
            throw new InvalidOperationException(
                $"The nav map entry '{entry.Title}' cannot be 'home: true' without a 'source'.");

        return new NavMapEntry(entry.Title.Trim(), source, children, entry.Skip ?? false, home, section);
    }

    static NavMapEntryYaml MapEntry(NavMapEntry entry) =>
        new()
        {
            Title = entry.Title,
            Source = entry.Source,
            Children = entry.Children.Count == 0 ? null : entry.Children.Select(MapEntry).ToList(),
            Skip = entry.Skip ? true : null,
            Home = entry.Home ? true : null,
            Section = SectionBehaviourParser.ToYaml(entry.Section)
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
    }
}
