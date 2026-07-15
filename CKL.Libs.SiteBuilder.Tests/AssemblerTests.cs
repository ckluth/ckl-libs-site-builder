using CKL.Libs.SiteBuilder.Assembly;
using CKL.Libs.SiteBuilder.Configuration;
using CKL.Libs.SiteBuilder.Model;
using NUnit.Framework;

namespace CKL.Libs.SiteBuilder.Tests;

public class AssemblerTests
{
    string _sourceDir = null!;
    string _outputDir = null!;

    [SetUp]
    public void SetUp()
    {
        _sourceDir = Directory.CreateTempSubdirectory("sitebuilder-src-").FullName;
        _outputDir = Path.Combine(Path.GetTempPath(), "sitebuilder-out-" + Guid.NewGuid());
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_sourceDir)) Directory.Delete(_sourceDir, recursive: true);
        if (Directory.Exists(_outputDir)) Directory.Delete(_outputDir, recursive: true);
    }

    [Test]
    public void Assemble_DiscoversDocumentAndLandingKinds_AndWritesNoStagingFolder()
    {
        WriteSourceFile("README.md", "# Home\n");
        WriteSourceFile(Path.Combine("guide", "README.md"), "# Guide\n");
        WriteSourceFile(Path.Combine("guide", "intro.md"), "# Intro\n");

        var entriesBefore = Directory.GetFileSystemEntries(_sourceDir, "*", SearchOption.AllDirectories).Length;

        var result = SiteAssembler.Assemble(_sourceDir);

        Assert.That(result.Succeeded, Is.True);
        var model = result.Value!;
        Assert.That(model.Pages.Single(p => p.RelativeSource == "README.md").Kind, Is.EqualTo(SiteNodeKind.Landing));
        Assert.That(model.Pages.Single(p => p.RelativeSource.EndsWith("intro.md")).Kind, Is.EqualTo(SiteNodeKind.Document));
        Assert.That(model.Pages.Single(p => p.RelativeOutput.Replace('\\', '/') == "guide/index.html").Kind, Is.EqualTo(SiteNodeKind.Landing));

        var entriesAfter = Directory.GetFileSystemEntries(_sourceDir, "*", SearchOption.AllDirectories).Length;
        Assert.That(entriesAfter, Is.EqualTo(entriesBefore));
    }

    [Test]
    public void AssembleConfigured_NavMap_ReordersRetitlesSkips_AndReportsDrift()
    {
        WriteSourceFile("README.md", "# Home\n");
        WriteSourceFile(Path.Combine("guide", "intro.md"), "# Intro\n");
        WriteSourceFile(Path.Combine("guide", "advanced.md"), "# Advanced\n");
        WriteSourceFile("orphan.md", "# Orphan\n");

        var navMap = new NavMap([
            new NavMapEntry("Home", "README.md", [], false),
            new NavMapEntry("Guide", null, [
                new NavMapEntry("Advanced First", Path.Combine("guide", "advanced.md"), [], false),
                new NavMapEntry("Introduction", Path.Combine("guide", "intro.md"), [], false),
                new NavMapEntry("Hidden", "orphan.md", [], true)
            ], false)
        ]);

        var result = SiteAssembler.AssembleConfigured([_sourceDir], navMap);

        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.Value!.UnplacedDocuments, Is.Empty);

        var model = result.Value.Site;
        Assert.That(model.Pages.Any(p => p.RelativeSource == "orphan.md"), Is.False);

        var advanced = model.Pages.Single(p => p.RelativeSource.Replace('\\', '/') == "guide/advanced.md");
        var intro = model.Pages.Single(p => p.RelativeSource.Replace('\\', '/') == "guide/intro.md");
        var guideIndex = model.Pages.Single(p => p.Kind == SiteNodeKind.NodeIndex);
        var search = model.Pages.Single(p => p.Kind == SiteNodeKind.Search);

        Assert.Multiple(() =>
        {
            Assert.That(advanced.Title, Is.EqualTo("Advanced First"));
            Assert.That(advanced.RelativeOutput.Replace('\\', '/'), Is.EqualTo("guide/advanced-first.html"));
            Assert.That(intro.RelativeOutput.Replace('\\', '/'), Is.EqualTo("guide/introduction.html"));
            Assert.That(guideIndex.RelativeOutput.Replace('\\', '/'), Is.EqualTo("guide/index.html"));
            Assert.That(search.RelativeOutput.Replace('\\', '/'), Is.EqualTo("search/index.html"));
        });
    }

    [Test]
    public void Build_CopiesNonMarkdownAssetToMirroredOutputPath()
    {
        WriteSourceFile("README.md", "# Home\n");
        var assetRelative = Path.Combine("images", "logo.png");
        WriteSourceFile(assetRelative, "not-really-a-png");

        var result = SiteBuilder.Build(new SiteBuilderOptions(_sourceDir, _outputDir));

        Assert.That(result.Succeeded, Is.True);
        Assert.That(File.Exists(Path.Combine(_outputDir, assetRelative)), Is.True);
    }

    string WriteSourceFile(string relativePath, string content)
    {
        var path = Path.Combine(_sourceDir, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }
}
