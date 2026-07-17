using CKL.Libs.ResultPattern;
using CKL.Libs.SiteBuilder.Assembly;
using CKL.Libs.SiteBuilder.Configuration;
using NUnit.Framework;

namespace CKL.Libs.SiteBuilder.Tests;

public class WildcardNavTests
{
    string _workspace = null!;
    string _outputDir = null!;

    [SetUp]
    public void SetUp()
    {
        _workspace = Directory.CreateTempSubdirectory("sitebuilder-wildcard-").FullName;
        _outputDir = Path.Combine(_workspace, "_site-out");
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_workspace)) Directory.Delete(_workspace, recursive: true);
    }

    [Test]
    public void WildcardSection_ExpandsInRelativePathOrder_WithHeadlineTitles()
    {
        var sourceDir = Directory.CreateDirectory(Path.Combine(_workspace, "src")).FullName;
        WriteFile(Path.Combine(sourceDir, "ideas", "0002-b.md"), "# Beta\n");
        WriteFile(Path.Combine(sourceDir, "ideas", "0001-a.md"), "# Alpha\n");
        WriteFile(Path.Combine(sourceDir, "ideas", "0003-c.md"), "# Gamma\n");

        var navMap = new NavMap([
            new NavMapEntry("Ideas", @"ideas\*.md", [])
        ]);

        var result = SiteAssembler.AssembleConfigured([sourceDir], navMap);

        Assert.That(result.Succeeded, Is.True);
        var ideas = result.Value!.Site.Nav.Single(node => node.Title == "Ideas");
        Assert.That(ideas.Children.Select(child => child.Title), Is.EqualTo(new[] { "Alpha", "Beta", "Gamma" }));
    }

    [Test]
    public void WildcardExclude_IsPlacedButNotRendered_AndDoesNotReportDrift()
    {
        var sourceDir = Directory.CreateDirectory(Path.Combine(_workspace, "src")).FullName;
        WriteFile(Path.Combine(sourceDir, "ideas", "0001-a.md"), "# Alpha\n");
        WriteFile(Path.Combine(sourceDir, "ideas", "0002-b.md"), "# Beta\n");
        WriteFile(Path.Combine(sourceDir, "ideas", "0003-c.md"), "# Gamma\n");

        var navMap = new NavMap([
            new NavMapEntry("Ideas", @"ideas\*.md", [], Exclude: [@"ideas\0002-b.md"])
        ]);

        var result = SiteAssembler.AssembleConfigured([sourceDir], navMap);

        Assert.That(result.Succeeded, Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(result.Value!.Site.Pages.Any(page => SamePath(page.RelativeSource, @"ideas\0002-b.md")), Is.False);
            Assert.That(result.Value.UnplacedDocuments, Is.Empty);
            Assert.That(result.Value.Site.Pages.Any(page => SamePath(page.RelativeSource, @"ideas\0001-a.md")), Is.True);
            Assert.That(result.Value.Site.Pages.Any(page => SamePath(page.RelativeSource, @"ideas\0003-c.md")), Is.True);
        });
    }

    [Test]
    public void WildcardExclude_WithoutMatch_SurfacesWarning()
    {
        var sourceDir = Directory.CreateDirectory(Path.Combine(_workspace, "src")).FullName;
        WriteFile(Path.Combine(sourceDir, "ideas", "0001-a.md"), "# Alpha\n");

        var navMap = new NavMap([
            new NavMapEntry("Ideas", @"ideas\*.md", [], Exclude: [@"ideas\9999-x.md"])
        ]);

        var result = SiteAssembler.AssembleConfigured([sourceDir], navMap);

        Assert.That(result.Succeeded, Is.True);
        Assert.That(
            result.Value!.Warnings,
            Has.Exactly(1).Matches<string>(warning => warning.Contains("ideas\\9999-x.md", StringComparison.OrdinalIgnoreCase)));
    }

    [Test]
    public void WildcardMatcher_HonoursNestedGlobSemantics()
    {
        var sourceDir = Directory.CreateDirectory(Path.Combine(_workspace, "src")).FullName;
        WriteFile(Path.Combine(sourceDir, "docs", "guide.md"), "# Guide\n");
        WriteFile(Path.Combine(sourceDir, "docs", "nested", "deep.md"), "# Deep\n");

        var nestedGlobResult = SiteAssembler.AssembleConfigured(
            [sourceDir],
            new NavMap([new NavMapEntry("Docs", @"docs\**\*.md", [])]));
        var singleSegmentGlobResult = SiteAssembler.AssembleConfigured(
            [sourceDir],
            new NavMap([new NavMapEntry("Docs", @"docs\*.md", [])]));

        Assert.Multiple(() =>
        {
            Assert.That(nestedGlobResult.Succeeded, Is.True);
            Assert.That(singleSegmentGlobResult.Succeeded, Is.True);
            Assert.That(nestedGlobResult.Value!.Site.Nav.Single(node => node.Title == "Docs").Children.Select(child => child.Title), Is.EquivalentTo(new[] { "Guide", "Deep" }));
            Assert.That(singleSegmentGlobResult.Value!.Site.Nav.Single(node => node.Title == "Docs").Children.Select(child => child.Title), Is.EqualTo(new[] { "Guide" }));
        });
    }

    [Test]
    public void WildcardSchemaViolations_FailToParse()
    {
        var wildcardWithChildren = ReadNav("""
            nav:
              - title: Ideas
                source: ideas\*.md
                children:
                  - title: Pinned
                    source: ideas\0001-a.md
            """);
        var wildcardWithHome = ReadNav("""
            nav:
              - title: Ideas
                source: ideas\*.md
                home: true
            """);
        var wildcardWithSkip = ReadNav("""
            nav:
              - title: Ideas
                source: ideas\*.md
                skip: true
            """);
        var wildcardWithInvalidTitleFrom = ReadNav("""
            nav:
              - title: Ideas
                source: ideas\*.md
                titleFrom: filename
            """);

        Assert.Multiple(() =>
        {
            Assert.That(wildcardWithChildren.Succeeded, Is.False);
            Assert.That(wildcardWithHome.Succeeded, Is.False);
            Assert.That(wildcardWithSkip.Succeeded, Is.False);
            Assert.That(wildcardWithInvalidTitleFrom.Succeeded, Is.False);
        });
    }

    [Test]
    public void WildcardOverview_GeneratesSectionIndexPage()
    {
        var sourceDir = Directory.CreateDirectory(Path.Combine(_workspace, "src")).FullName;
        WriteFile(Path.Combine(sourceDir, "ideas", "0001-a.md"), "# Alpha\n");
        WriteFile(Path.Combine(sourceDir, "ideas", "0002-b.md"), "# Beta\n");

        var navMap = new NavMap([
            new NavMapEntry("Ideas", @"ideas\*.md", [], Section: SectionBehaviour.Overview)
        ]);

        var result = SiteAssembler.AssembleConfigured([sourceDir], navMap);

        Assert.That(result.Succeeded, Is.True);
        var overview = result.Value!.Site.Pages.Single(page => page.Kind == Model.SiteNodeKind.NodeIndex);
        Assert.Multiple(() =>
        {
            Assert.That(overview.RelativeOutput.Replace('\\', '/'), Is.EqualTo("ideas/index.html"));
            Assert.That(overview.GeneratedHtml, Does.Contain("Alpha"));
            Assert.That(overview.GeneratedHtml, Does.Contain("Beta"));
        });
    }

    // --- Section-scoped exclude reuse (ADR-0031) -------------------------------------------------

    [Test]
    public void WildcardExclude_ExcludedFileReusedInAnotherSection_RendersThereWithNoDriftOrDoubleClaim()
    {
        var sourceDir = Directory.CreateDirectory(Path.Combine(_workspace, "src")).FullName;
        WriteFile(Path.Combine(sourceDir, "decisions", "0001-a.md"), "# Alpha\n");
        WriteFile(Path.Combine(sourceDir, "decisions", "0002-b.md"), "# Beta\n");
        WriteFile(Path.Combine(sourceDir, "decisions", "0003-c.md"), "# Gamma\n");

        var navMap = new NavMap([
            new NavMapEntry("Decisions", @"decisions\*.md", [], Exclude: [@"decisions\0001-a.md"]),
            new NavMapEntry("Foundations", @"decisions\0001-a.md", [])
        ]);

        var result = SiteAssembler.AssembleConfigured([sourceDir], navMap);

        Assert.That(result.Succeeded, Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(result.Value!.UnplacedDocuments, Is.Empty);

            var decisions = result.Value.Site.Nav.Single(node => node.Title == "Decisions");
            Assert.That(decisions.Children.Any(child => SamePath(child.Page!.RelativeOutput, @"decisions\0001-a.md")), Is.False);
            Assert.That(decisions.Children.Select(child => child.Title), Does.Not.Contain("Alpha"));

            var foundations = result.Value.Site.Nav.Single(node => node.Title == "Foundations");
            Assert.That(foundations.Page, Is.Not.Null);

            Assert.That(result.Value.Site.Pages.Count(page => SamePath(page.RelativeSource, @"decisions\0001-a.md")), Is.EqualTo(1));
        });
    }

    [Test]
    public void TwoLiteralSourceEntries_ClaimingSameFile_StillFails()
    {
        var sourceDir = Directory.CreateDirectory(Path.Combine(_workspace, "src")).FullName;
        WriteFile(Path.Combine(sourceDir, "decisions", "0001-a.md"), "# Alpha\n");

        var navMap = new NavMap([
            new NavMapEntry("Alpha", @"decisions\0001-a.md", []),
            new NavMapEntry("Alpha Again", @"decisions\0001-a.md", [])
        ]);

        var result = SiteAssembler.AssembleConfigured([sourceDir], navMap);

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.ErrorMessage, Does.Contain("more than once"));
    }

    Result<NavMap> ReadNav(string yaml)
    {
        var navPath = Path.Combine(_workspace, Guid.NewGuid().ToString("N") + ".yml");
        File.WriteAllText(navPath, yaml);
        return NavMapFile.Read(navPath);
    }

    static bool SamePath(string actual, string expected) =>
        actual.Replace('\\', '/').Equals(expected.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase);

    static void WriteFile(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }
}
