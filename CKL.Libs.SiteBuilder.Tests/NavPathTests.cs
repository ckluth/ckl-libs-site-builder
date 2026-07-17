using CKL.Libs.ResultPattern;
using CKL.Libs.SiteBuilder.Assembly;
using CKL.Libs.SiteBuilder.Configuration;
using CKL.Libs.SiteBuilder.Model;
using NUnit.Framework;

namespace CKL.Libs.SiteBuilder.Tests;

/// <summary>Proves every ADR-0021 decision: the single generated-nav path, the guaranteed synthesised
/// landing with no filename magic, home designation, configurable section-click behaviour, the
/// unconditional search page, the asset-copy ignore rule, and the guarded output-directory clean.</summary>
public class NavPathTests
{
    string _workspace = null!;
    string _outputDir = null!;

    [SetUp]
    public void SetUp()
    {
        _workspace = Directory.CreateTempSubdirectory("sitebuilder-navpath-").FullName;
        _outputDir = Path.Combine(_workspace, "_site-out");
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_workspace)) Directory.Delete(_workspace, recursive: true);
    }

    // --- Single-path + generated-map -----------------------------------------------------------

    [Test]
    public void Build_ConfigWithoutNav_GeneratesNavYamlInCwd_AndPlacesEveryDocument()
    {
        var docsDir = Directory.CreateDirectory(Path.Combine(_workspace, "docs")).FullName;
        WriteFile(Path.Combine(docsDir, "guide.md"), "# Guide\n");
        WriteFile(Path.Combine(docsDir, "overview.md"), "# Overview\n");
        WriteFile(Path.Combine(docsDir, "decisions", "0001-adopt.md"), "# Adopt\n");
        WriteFile(Path.Combine(docsDir, "ideas", "0001-idea.md"), "# Idea\n");

        var configPath = Path.Combine(_workspace, "site.yml");
        File.WriteAllText(configPath, """
            output: .\_site-out
            scanRoots:
              - .\docs
            """);

        var cwd = Directory.CreateTempSubdirectory("sitebuilder-cwd-").FullName;
        var originalCwd = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(cwd);

            var result = SiteBuilder.Build(configPath);
            Assert.That(result.Succeeded, Is.True);

            var navPath = Path.Combine(cwd, "nav.yml");
            Assert.That(File.Exists(navPath), Is.True);

            var navText = File.ReadAllText(navPath);
            Assert.Multiple(() =>
            {
                Assert.That(navText, Does.Contain("guide.md"));
                Assert.That(navText, Does.Contain("overview.md"));
                Assert.That(navText, Does.Contain("decisions"));
                Assert.That(navText, Does.Contain("0001-adopt.md"));
                Assert.That(navText, Does.Contain("ideas"));
                Assert.That(navText, Does.Contain("0001-idea.md"));
            });

            // A second run must reuse (not overwrite) a hand-owned map.
            File.AppendAllText(navPath, "\n# hand edited\n");
            var handEdited = File.ReadAllText(navPath);
            var secondResult = SiteBuilder.Build(configPath);

            Assert.That(secondResult.Succeeded, Is.True);
            Assert.That(File.ReadAllText(navPath), Is.EqualTo(handEdited));
        }
        finally
        {
            Directory.SetCurrentDirectory(originalCwd);
            Directory.Delete(cwd, recursive: true);
        }
    }

    // --- Landing + home designation -------------------------------------------------------------

    [Test]
    public void AssembleConfigured_NoHomeDesignated_SynthesizesLandingListingTopLevel_AndReadmeIsOrdinary()
    {
        var sourceDir = Directory.CreateDirectory(Path.Combine(_workspace, "src")).FullName;
        WriteFile(Path.Combine(sourceDir, "README.md"), "# Home\n");
        WriteFile(Path.Combine(sourceDir, "guide.md"), "# Guide\n");

        var navMap = new NavMap([
            new NavMapEntry("Readme", "README.md", []),
            new NavMapEntry("Guide", "guide.md", [])
        ]);

        var result = SiteAssembler.AssembleConfigured([sourceDir], navMap);
        Assert.That(result.Succeeded, Is.True);

        var model = result.Value!.Site;
        var readme = model.Pages.Single(p => p.RelativeSource == "README.md");
        Assert.That(readme.RelativeOutput, Is.Not.EqualTo("index.html"));
        Assert.That(readme.Kind, Is.EqualTo(SiteNodeKind.Document));

        var landing = model.Pages.Single(p => p.RelativeOutput == "index.html");
        Assert.That(landing.Kind, Is.EqualTo(SiteNodeKind.Landing));
        Assert.That(landing.SourcePath, Is.Null);
        Assert.That(landing.GeneratedHtml, Does.Contain("Readme"));
        Assert.That(landing.GeneratedHtml, Does.Contain("Guide"));

        var homeNavNode = model.Nav.Single(n => n.Title == "Home");
        Assert.That(homeNavNode.Page!.RelativeOutput, Is.EqualTo("index.html"));
    }

    [Test]
    public void AssembleConfigured_HomeDesignatedEntry_BecomesIndexHtml()
    {
        var sourceDir = Directory.CreateDirectory(Path.Combine(_workspace, "src")).FullName;
        WriteFile(Path.Combine(sourceDir, "README.md"), "# Home\n");
        WriteFile(Path.Combine(sourceDir, "guide.md"), "# Guide\n");

        var navMap = new NavMap([
            new NavMapEntry("Home", "README.md", [], Home: true),
            new NavMapEntry("Guide", "guide.md", [])
        ]);

        var result = SiteAssembler.AssembleConfigured([sourceDir], navMap);
        Assert.That(result.Succeeded, Is.True);

        var model = result.Value!.Site;
        var readme = model.Pages.Single(p => p.RelativeSource == "README.md");
        Assert.That(readme.RelativeOutput, Is.EqualTo("index.html"));
        Assert.That(readme.Kind, Is.EqualTo(SiteNodeKind.Landing));

        // Only one index.html total: no separately synthesised landing.
        Assert.That(model.Pages.Count(p => p.RelativeOutput == "index.html"), Is.EqualTo(1));
    }

    [Test]
    public void AssembleConfigured_TwoHomeEntries_Fails()
    {
        var sourceDir = Directory.CreateDirectory(Path.Combine(_workspace, "src")).FullName;
        WriteFile(Path.Combine(sourceDir, "a.md"), "# A\n");
        WriteFile(Path.Combine(sourceDir, "b.md"), "# B\n");

        var navMap = new NavMap([
            new NavMapEntry("A", "a.md", [], Home: true),
            new NavMapEntry("B", "b.md", [], Home: true)
        ]);

        var result = SiteAssembler.AssembleConfigured([sourceDir], navMap);

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.ErrorMessage, Does.Contain("home"));
    }

    // --- Section-click behaviour -----------------------------------------------------------------

    [Test]
    public void AssembleConfigured_DefaultExpand_GeneratesNoSectionOverviewPage()
    {
        var sourceDir = Directory.CreateDirectory(Path.Combine(_workspace, "src")).FullName;
        WriteFile(Path.Combine(sourceDir, "guide", "intro.md"), "# Intro\n");

        var navMap = new NavMap([
            new NavMapEntry("Guide", null, [
                new NavMapEntry("Intro", Path.Combine("guide", "intro.md"), [])
            ])
        ]);

        var result = SiteAssembler.AssembleConfigured([sourceDir], navMap);
        Assert.That(result.Succeeded, Is.True);

        var model = result.Value!.Site;
        Assert.That(model.Pages.Any(p => p.Kind == SiteNodeKind.NodeIndex), Is.False);

        var guideNav = model.Nav.Single(n => n.Title == "Guide");
        Assert.That(guideNav.Page, Is.Null);
    }

    [Test]
    public void AssembleConfigured_OverviewBehaviour_GeneratesSectionOverviewPage()
    {
        var sourceDir = Directory.CreateDirectory(Path.Combine(_workspace, "src")).FullName;
        WriteFile(Path.Combine(sourceDir, "guide", "intro.md"), "# Intro\n");

        var navMap = new NavMap([
            new NavMapEntry("Guide", null, [
                new NavMapEntry("Intro", Path.Combine("guide", "intro.md"), [])
            ])
        ]);

        var result = SiteAssembler.AssembleConfigured([sourceDir], navMap, sectionBehaviour: SectionBehaviour.Overview);
        Assert.That(result.Succeeded, Is.True);

        var model = result.Value!.Site;
        var guideIndex = model.Pages.Single(p => p.Kind == SiteNodeKind.NodeIndex);
        Assert.That(guideIndex.RelativeOutput.Replace('\\', '/'), Is.EqualTo("guide/index.html"));

        var guideNav = model.Nav.Single(n => n.Title == "Guide");
        Assert.That(guideNav.Page, Is.Not.Null);
    }

    [Test]
    public void AssembleConfigured_PerEntrySectionOverride_FlipsSingleSection()
    {
        var sourceDir = Directory.CreateDirectory(Path.Combine(_workspace, "src")).FullName;
        WriteFile(Path.Combine(sourceDir, "guide", "intro.md"), "# Intro\n");
        WriteFile(Path.Combine(sourceDir, "reference", "api.md"), "# Api\n");

        var navMap = new NavMap([
            new NavMapEntry("Guide", null, [
                new NavMapEntry("Intro", Path.Combine("guide", "intro.md"), [])
            ], Section: SectionBehaviour.Overview),
            new NavMapEntry("Reference", null, [
                new NavMapEntry("Api", Path.Combine("reference", "api.md"), [])
            ])
        ]);

        var result = SiteAssembler.AssembleConfigured([sourceDir], navMap, sectionBehaviour: SectionBehaviour.Expand);
        Assert.That(result.Succeeded, Is.True);

        var model = result.Value!.Site;
        Assert.That(model.Pages.Count(p => p.Kind == SiteNodeKind.NodeIndex), Is.EqualTo(1));
        Assert.That(model.Pages.Single(p => p.Kind == SiteNodeKind.NodeIndex).RelativeOutput.Replace('\\', '/'), Is.EqualTo("guide/index.html"));
    }

    // --- Search always on, asset-ignore rule, guarded clean ---------------------------------------

    [Test]
    public void Build_DirectOptions_AlwaysWritesSearchPageAndNavEntry()
    {
        var sourceDir = Directory.CreateDirectory(Path.Combine(_workspace, "src")).FullName;
        WriteFile(Path.Combine(sourceDir, "README.md"), "# Home\n");

        var result = SiteBuilder.Build(new SiteBuilderOptions(sourceDir, _outputDir));

        Assert.That(result.Succeeded, Is.True);
        Assert.That(File.Exists(Path.Combine(_outputDir, "search", "index.html")), Is.True);
    }

    [Test]
    public void Build_SkipsDefaultIgnoredDirectories_ButCopiesOrdinaryAssets()
    {
        var sourceDir = Directory.CreateDirectory(Path.Combine(_workspace, "src")).FullName;
        WriteFile(Path.Combine(sourceDir, "README.md"), "# Home\n");
        WriteFile(Path.Combine(sourceDir, ".vs", "cruft.txt"), "cruft");
        WriteFile(Path.Combine(sourceDir, "bin", "out.dll"), "cruft");
        WriteFile(Path.Combine(sourceDir, "obj", "temp.txt"), "cruft");
        WriteFile(Path.Combine(sourceDir, "node_modules", "pkg", "index.js"), "cruft");
        WriteFile(Path.Combine(sourceDir, "icon.png"), "not-really-a-png");

        var result = SiteBuilder.Build(new SiteBuilderOptions(sourceDir, _outputDir));

        Assert.That(result.Succeeded, Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(Directory.Exists(Path.Combine(_outputDir, ".vs")), Is.False);
            Assert.That(Directory.Exists(Path.Combine(_outputDir, "bin")), Is.False);
            Assert.That(Directory.Exists(Path.Combine(_outputDir, "obj")), Is.False);
            Assert.That(Directory.Exists(Path.Combine(_outputDir, "node_modules")), Is.False);
            Assert.That(File.Exists(Path.Combine(_outputDir, "icon.png")), Is.True);
        });
    }

    [Test]
    public void Build_ConfiguredAssetExclude_IsAlsoSkipped()
    {
        var sourceDir = Directory.CreateDirectory(Path.Combine(_workspace, "src")).FullName;
        WriteFile(Path.Combine(sourceDir, "README.md"), "# Home\n");
        WriteFile(Path.Combine(sourceDir, "vendor", "third-party.js"), "cruft");
        WriteFile(Path.Combine(sourceDir, "icon.png"), "not-really-a-png");

        var configPath = Path.Combine(_workspace, "site.yml");
        File.WriteAllText(configPath, $"""
            output: .\_site-out
            scanRoots:
              - .\src
            assets:
              exclude:
                - vendor
            """);

        var result = SiteBuilder.Build(configPath);

        Assert.That(result.Succeeded, Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(Directory.Exists(Path.Combine(_outputDir, "vendor")), Is.False);
            Assert.That(File.Exists(Path.Combine(_outputDir, "icon.png")), Is.True);
        });
    }

    [Test]
    public void Build_RebuildRemovesStaleOutputFile_WhenMarkerPresent()
    {
        var sourceDir = Directory.CreateDirectory(Path.Combine(_workspace, "src")).FullName;
        WriteFile(Path.Combine(sourceDir, "README.md"), "# Home\n");

        var options = new SiteBuilderOptions(sourceDir, _outputDir);
        var first = SiteBuilder.Build(options);
        Assert.That(first.Succeeded, Is.True);

        var stalePath = Path.Combine(_outputDir, "stale.txt");
        File.WriteAllText(stalePath, "stale");

        var second = SiteBuilder.Build(options);
        Assert.That(second.Succeeded, Is.True);
        Assert.That(File.Exists(stalePath), Is.False);
    }

    [Test]
    public void Build_NonEmptyUnmarkedOutputDirectory_FailsWithoutDeletingAnything()
    {
        var sourceDir = Directory.CreateDirectory(Path.Combine(_workspace, "src")).FullName;
        WriteFile(Path.Combine(sourceDir, "README.md"), "# Home\n");

        Directory.CreateDirectory(_outputDir);
        var existingPath = Path.Combine(_outputDir, "existing.txt");
        File.WriteAllText(existingPath, "keep-me");

        var result = SiteBuilder.Build(new SiteBuilderOptions(sourceDir, _outputDir));

        Assert.That(result.Succeeded, Is.False);
        Assert.That(File.Exists(existingPath), Is.True);
    }

    // --- Empty-title single-file entry derives headline (ADR-0030) ------------------------------

    [Test]
    public void AssembleConfigured_EmptyTitleSingleFileEntry_DerivesTitleFromH1()
    {
        var sourceDir = Directory.CreateDirectory(Path.Combine(_workspace, "src")).FullName;
        WriteFile(Path.Combine(sourceDir, "guide", "intro.md"), "# Introduction\n");

        var navMap = new NavMap([
            new NavMapEntry("", Path.Combine("guide", "intro.md"), [])
        ]);

        var result = SiteAssembler.AssembleConfigured([sourceDir], navMap);
        Assert.That(result.Succeeded, Is.True);

        var model = result.Value!.Site;
        var page = model.Pages.Single(p => p.RelativeSource == Path.Combine("guide", "intro.md"));
        Assert.That(page.Title, Is.EqualTo("Introduction"));
        Assert.That(page.RelativeOutput.Replace('\\', '/'), Does.EndWith("introduction.html"));

        var navNode = model.Nav.Single(n => n.Title == "Introduction");
        Assert.That(navNode.Page!.RelativeOutput, Is.EqualTo(page.RelativeOutput));
    }

    [Test]
    public void AssembleConfigured_EmptyTitleSingleFileEntry_NoH1_FallsBackToFormattedFilename()
    {
        var sourceDir = Directory.CreateDirectory(Path.Combine(_workspace, "src")).FullName;
        WriteFile(Path.Combine(sourceDir, "0007-my-doc.md"), "Body text only, no headline.\n");

        var navMap = new NavMap([
            new NavMapEntry("", "0007-my-doc.md", [])
        ]);

        var result = SiteAssembler.AssembleConfigured([sourceDir], navMap);
        Assert.That(result.Succeeded, Is.True);

        var expectedTitle = SiteAssembler.FormatName("0007-my-doc");
        var page = result.Value!.Site.Pages.Single(p => p.RelativeSource == "0007-my-doc.md");
        Assert.That(page.Title, Is.EqualTo(expectedTitle));
    }

    [Test]
    public void AssembleConfigured_NonEmptyTitleSingleFileEntry_IsUsedVerbatim()
    {
        var sourceDir = Directory.CreateDirectory(Path.Combine(_workspace, "src")).FullName;
        WriteFile(Path.Combine(sourceDir, "guide", "intro.md"), "# Introduction\n");

        var navMap = new NavMap([
            new NavMapEntry("Custom", Path.Combine("guide", "intro.md"), [])
        ]);

        var result = SiteAssembler.AssembleConfigured([sourceDir], navMap);
        Assert.That(result.Succeeded, Is.True);

        var page = result.Value!.Site.Pages.Single(p => p.RelativeSource == Path.Combine("guide", "intro.md"));
        Assert.That(page.Title, Is.EqualTo("Custom"));
    }

    [Test]
    public void NavMapFile_EmptyTitleOnSectionOrWildcardEntry_FailsToParse()
    {
        var sectionResult = ReadNav("""
            nav:
              - title:
                children:
                  - title: Intro
                    source: guide\intro.md
            """);
        var wildcardResult = ReadNav("""
            nav:
              - title:
                source: ideas\*.md
            """);

        Assert.Multiple(() =>
        {
            Assert.That(sectionResult.Succeeded, Is.False);
            Assert.That(wildcardResult.Succeeded, Is.False);
        });
    }

    Result<NavMap> ReadNav(string yaml)
    {
        var navPath = Path.Combine(_workspace, Guid.NewGuid().ToString("N") + ".yml");
        File.WriteAllText(navPath, yaml);
        return NavMapFile.Read(navPath);
    }

    static void WriteFile(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }
}
