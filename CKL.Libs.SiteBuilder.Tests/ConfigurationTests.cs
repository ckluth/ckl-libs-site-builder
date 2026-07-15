using CKL.Libs.SiteBuilder.Configuration;
using CKL.Libs.SiteBuilder.Metadata;
using CKL.Libs.SiteBuilder.Rendering;
using NUnit.Framework;

namespace CKL.Libs.SiteBuilder.Tests;

public class ConfigurationTests
{
    string _workspace = null!;
    string _outputDir = null!;

    [SetUp]
    public void SetUp()
    {
        _workspace = Directory.CreateTempSubdirectory("sitebuilder-config-").FullName;
        _outputDir = Path.Combine(_workspace, "_site-out");
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_workspace)) Directory.Delete(_workspace, recursive: true);
    }

    [Test]
    public void Read_ParsesSiteConfigAndResolvesPaths()
    {
        var docsDir = Directory.CreateDirectory(Path.Combine(_workspace, "repo-a", "docs")).FullName;
        var configDir = Directory.CreateDirectory(Path.Combine(_workspace, "config")).FullName;
        var configPath = Path.Combine(configDir, "site.yml");
        File.WriteAllText(configPath, """
            title: My Docs Site
            output: ./_site
            scanRoots:
              - ../repo-a/docs
            theme:
              stylesheet: ./custom.css
              mermaid: forest
            nav: ./nav.yml
            """);

        var result = SiteConfigReader.Read(configPath);

        Assert.That(result.Succeeded, Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(result.Value!.Title, Is.EqualTo("My Docs Site"));
            Assert.That(result.Value.OutputDirectory, Is.EqualTo(Path.Combine(configDir, "_site")));
            Assert.That(result.Value.ScanRoots, Is.EqualTo(new[] { docsDir }));
            Assert.That(result.Value.Theme.StylesheetPath, Is.EqualTo(Path.Combine(configDir, "custom.css")));
            Assert.That(result.Value.Theme.MermaidTheme, Is.EqualTo("forest"));
            Assert.That(result.Value.NavMapPath, Is.EqualTo(Path.Combine(configDir, "nav.yml")));
        });
    }

    [Test]
    public void Read_MinimalConfig_UsesDefaults()
    {
        var docsDir = Directory.CreateDirectory(Path.Combine(_workspace, "docs")).FullName;
        var configPath = Path.Combine(_workspace, "site.yml");
        File.WriteAllText(configPath, """
            scanRoots:
              - ./docs
            """);

        var result = SiteConfigReader.Read(configPath);

        Assert.That(result.Succeeded, Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(result.Value!.Title, Is.EqualTo("docs"));
            Assert.That(result.Value.OutputDirectory, Is.EqualTo(Path.Combine(_workspace, "_site")));
            Assert.That(result.Value.Theme.StylesheetPath, Is.Null);
            Assert.That(result.Value.Theme.MermaidTheme, Is.EqualTo("dark"));
            Assert.That(result.Value.NavMapPath, Is.Null);
            Assert.That(result.Value.ScanRoots, Is.EqualTo(new[] { docsDir }));
        });
    }

    [Test]
    public void Read_MalformedYaml_Fails()
    {
        var configPath = Path.Combine(_workspace, "site.yml");
        File.WriteAllText(configPath, """
            scanRoots:
              - ./docs
            theme: [
            """);

        var result = SiteConfigReader.Read(configPath);

        Assert.That(result.Succeeded, Is.False);
    }

    [Test]
    public void EnsureExists_WritesScaffoldOnFirstRun_AndLeavesExistingMapUntouched()
    {
        WriteSource("README.md", "# Home\n");
        WriteSource(Path.Combine("guide", "README.md"), "# Guide\n");
        WriteSource(Path.Combine("guide", "intro.md"), "# Intro\n");

        var navPath = Path.Combine(_workspace, "nav.yml");
        var firstRun = NavMapScaffolder.EnsureExists(navPath, [_workspace], NoOpMetadataInference.Instance);

        Assert.That(firstRun.Succeeded, Is.True);
        var scaffoldText = File.ReadAllText(navPath);
        Assert.That(scaffoldText, Does.Contain("title: Guide"));
        Assert.That(scaffoldText, Does.Contain("source: guide\\intro.md").Or.Contain("source: guide/intro.md"));

        File.WriteAllText(navPath, "nav:\n  - title: Hand Owned\n    source: README.md\n");
        var secondRun = NavMapScaffolder.EnsureExists(navPath, [_workspace], NoOpMetadataInference.Instance);

        Assert.That(secondRun.Succeeded, Is.True);
        Assert.That(File.ReadAllText(navPath), Is.EqualTo("nav:\n  - title: Hand Owned\n    source: README.md\n"));
    }

    [Test]
    public void Build_ConfigReportsUnplacedDocuments_AndAppliesThemeSeams()
    {
        WriteSource("README.md", "# Home\n");
        WriteSource("kept.md", "# Kept\n");
        WriteSource("orphan.md", "# Orphan\n");
        var configPath = Path.Combine(_workspace, "site.yml");
        var navPath = Path.Combine(_workspace, "nav.yml");
        var stylesheetPath = Path.Combine(_workspace, "custom.css");
        File.WriteAllText(stylesheetPath, "body{color:hotpink;}");
        File.WriteAllText(navPath, """
            nav:
              - title: Home
                source: README.md
              - title: Docs
                children:
                  - title: Kept Page
                    source: kept.md
            """);
        File.WriteAllText(configPath, $"""
            output: .\_site-out
            scanRoots:
              - .
            theme:
              stylesheet: .\custom.css
              mermaid: forest
            nav: .\nav.yml
            """);

        var result = SiteBuilder.Build(configPath);

        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.Value!.UnplacedDocuments, Is.EqualTo(new[] { "orphan.md" }));
        Assert.That(File.ReadAllText(Path.Combine(_outputDir, "site.css")), Is.EqualTo("body{color:hotpink;}"));

        var keptHtml = File.ReadAllText(Path.Combine(_outputDir, "docs", "kept-page.html"));
        Assert.That(keptHtml, Does.Contain("theme:'forest'"));
    }

    [Test]
    public void Build_DefaultThemeAndSearchNode_WriteNoJsonFile()
    {
        WriteSource("README.md", "# Home\n");
        WriteSource("guide.md", """
            ---
            summary: Quick summary
            tags:
              - docs
            ---
            # Guide
            """);

        var configPath = Path.Combine(_workspace, "site.yml");
        var navPath = Path.Combine(_workspace, "nav.yml");
        File.WriteAllText(navPath, """
            nav:
              - title: Home
                source: README.md
              - title: Guide
                source: guide.md
            """);
        File.WriteAllText(configPath, """
            output: .\_site-out
            scanRoots:
              - .
            nav: .\nav.yml
            """);

        var result = SiteBuilder.Build(configPath);

        Assert.That(result.Succeeded, Is.True);
        Assert.That(File.ReadAllText(Path.Combine(_outputDir, "site.css")), Is.EqualTo(HtmlTemplate.Css));

        var guideHtml = File.ReadAllText(Path.Combine(_outputDir, "guide.html"));
        Assert.That(guideHtml, Does.Contain("theme:'dark'"));

        var searchHtml = File.ReadAllText(Path.Combine(_outputDir, "search", "index.html"));
        Assert.Multiple(() =>
        {
            Assert.That(searchHtml, Does.Contain("\"title\":\"Guide\""));
            Assert.That(searchHtml, Does.Contain("\"summary\":\"Quick summary\""));
            Assert.That(searchHtml, Does.Contain("\"url\":\"guide.html\""));
        });

        var jsonFiles = Directory.GetFiles(_outputDir, "*.json", SearchOption.AllDirectories);
        Assert.That(jsonFiles, Is.Empty);
    }

    string WriteSource(string relativePath, string content)
    {
        var path = Path.Combine(_workspace, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }
}
