using CKL.Libs.SiteBuilder.Assembly;
using CKL.Libs.SiteBuilder.Configuration;
using CKL.Libs.SiteBuilder.Model;
using NUnit.Framework;

namespace CKL.Libs.SiteBuilder.Tests;

public class IntroParagraphTests
{
    string _workspace = null!;
    string _outputDir = null!;

    [SetUp]
    public void SetUp()
    {
        _workspace = Directory.CreateTempSubdirectory("sitebuilder-intro-").FullName;
        _outputDir = Path.Combine(_workspace, "_site-out");
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_workspace)) Directory.Delete(_workspace, recursive: true);
    }

    [Test]
    public void SiteIntro_RendersOnSynthesisedLanding()
    {
        var docsDir = Directory.CreateDirectory(Path.Combine(_workspace, "docs")).FullName;
        WriteFile(Path.Combine(docsDir, "guide.md"), "# Guide\n");
        WriteFile(Path.Combine(_workspace, "nav.yml"), """
            nav:
              - title: Guide
                source: guide.md
            """);
        WriteFile(Path.Combine(_workspace, "site.yml"), """
            output: .\_site-out
            scanRoots:
              - .\docs
            nav: .\nav.yml
            intro: Welcome **home**.
            """);

        var result = SiteBuilder.Build(Path.Combine(_workspace, "site.yml"));

        Assert.That(result.Succeeded, Is.True);
        var indexHtml = File.ReadAllText(Path.Combine(_outputDir, "index.html"));
        var contentHtml = indexHtml[indexHtml.IndexOf("<h1>Home</h1>", StringComparison.Ordinal)..];
        Assert.Multiple(() =>
        {
            Assert.That(indexHtml, Does.Contain("<strong>home</strong>"));
            Assert.That(contentHtml.IndexOf("<strong>home</strong>", StringComparison.Ordinal), Is.LessThan(contentHtml.IndexOf("<ul>", StringComparison.Ordinal)));
        });
    }

    [Test]
    public void SectionIntro_RendersOnOverviewPage()
    {
        var sourceDir = Directory.CreateDirectory(Path.Combine(_workspace, "src")).FullName;
        WriteFile(Path.Combine(sourceDir, "guide", "intro.md"), "# Intro\n");

        var navMap = new NavMap([
            new NavMapEntry(
                "Guide",
                null,
                [new NavMapEntry("Intro", Path.Combine("guide", "intro.md"), [])],
                Section: SectionBehaviour.Overview,
                Intro: "A **guided** section.")
        ]);

        var result = SiteAssembler.AssembleConfigured([sourceDir], navMap);

        Assert.That(result.Succeeded, Is.True);
        var overview = result.Value!.Site.Pages.Single(page => page.Kind == SiteNodeKind.NodeIndex);
        var generatedHtml = overview.GeneratedHtml!;
        Assert.Multiple(() =>
        {
            Assert.That(generatedHtml, Does.Contain("<strong>guided</strong>"));
            Assert.That(generatedHtml.IndexOf("<strong>guided</strong>", StringComparison.Ordinal), Is.LessThan(generatedHtml.IndexOf("<ul>", StringComparison.Ordinal)));
        });
    }

    [Test]
    public void SectionIntro_OnExpandSection_WarnsAndIsIgnored()
    {
        var sourceDir = Directory.CreateDirectory(Path.Combine(_workspace, "src")).FullName;
        WriteFile(Path.Combine(sourceDir, "guide", "intro.md"), "# Intro\n");

        var navMap = new NavMap([
            new NavMapEntry(
                "Guide",
                null,
                [new NavMapEntry("Intro", Path.Combine("guide", "intro.md"), [])],
                Intro: "Hidden intro")
        ]);

        var result = SiteAssembler.AssembleConfigured([sourceDir], navMap, sectionBehaviour: SectionBehaviour.Expand);

        Assert.That(result.Succeeded, Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(result.Value!.Warnings, Has.Exactly(1).EqualTo("The 'intro' on section 'Guide' is ignored because the section renders no page (section: expand)."));
            Assert.That(result.Value.Site.Pages.Any(page => page.GeneratedHtml?.Contains("Hidden intro", StringComparison.Ordinal) == true), Is.False);
        });
    }

    [Test]
    public void SiteIntro_WithDesignatedHome_WarnsAndIsIgnored()
    {
        var docsDir = Directory.CreateDirectory(Path.Combine(_workspace, "docs")).FullName;
        WriteFile(Path.Combine(docsDir, "home.md"), "# Home\nBody text.\n");
        WriteFile(Path.Combine(_workspace, "nav.yml"), """
            nav:
              - title: Home
                source: home.md
                home: true
            """);
        WriteFile(Path.Combine(_workspace, "site.yml"), """
            output: .\_site-out
            scanRoots:
              - .\docs
            nav: .\nav.yml
            intro: This should be ignored.
            """);

        var result = SiteBuilder.Build(Path.Combine(_workspace, "site.yml"));

        Assert.That(result.Succeeded, Is.True);
        var homeHtml = File.ReadAllText(Path.Combine(_outputDir, "index.html"));
        Assert.Multiple(() =>
        {
            Assert.That(result.Value!.Warnings, Has.Exactly(1).EqualTo("The site 'intro' is ignored because a 'home' page is designated."));
            Assert.That(homeHtml, Does.Not.Contain("This should be ignored."));
        });
    }

    [Test]
    public void Intro_OnLeafEntry_FailsBuild()
    {
        var docsDir = Directory.CreateDirectory(Path.Combine(_workspace, "docs")).FullName;
        WriteFile(Path.Combine(docsDir, "guide.md"), "# Guide\n");
        WriteFile(Path.Combine(_workspace, "nav.yml"), """
            nav:
              - title: Guide
                source: guide.md
                intro: invalid
            """);
        WriteFile(Path.Combine(_workspace, "site.yml"), """
            output: .\_site-out
            scanRoots:
              - .\docs
            nav: .\nav.yml
            """);

        var result = SiteBuilder.Build(Path.Combine(_workspace, "site.yml"));

        Assert.That(result.Succeeded, Is.False);
    }

    static void WriteFile(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }
}
