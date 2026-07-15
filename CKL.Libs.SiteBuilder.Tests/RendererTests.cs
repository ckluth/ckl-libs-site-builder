using CKL.Libs.SiteBuilder.Assembly;
using CKL.Libs.SiteBuilder.Model;
using CKL.Libs.SiteBuilder.Rendering;
using NUnit.Framework;

namespace CKL.Libs.SiteBuilder.Tests;

public class RendererTests
{
    string _tempDir = null!;

    [SetUp]
    public void SetUp() => _tempDir = Directory.CreateTempSubdirectory("sitebuilder-render-").FullName;

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true);
    }

    [Test]
    public void Render_MarkdownHeadingAndParagraph_ProducesExpectedHtml()
    {
        var sourcePath = WriteFile("page.md", "# Hello\n\nSome text.\n");
        var page = new SiteNode(sourcePath, "page.md", "page.html", "Hello", SiteNodeKind.Document, SiteNode.NoOverrides);

        var html = PageRenderer.Render(page, [], _tempDir, "Test Site");

        Assert.That(html, Does.Contain("Hello</h1>"));
        Assert.That(html, Does.Contain("<p>Some text.</p>"));
    }

    [Test]
    public void Render_MermaidFencedBlock_BecomesMermaidDiv()
    {
        var markdown = "# Diagram\n\n```mermaid\ngraph TD; A-->B;\n```\n";
        var sourcePath = WriteFile("diagram.md", markdown);
        var page = new SiteNode(sourcePath, "diagram.md", "diagram.html", "Diagram", SiteNodeKind.Document, SiteNode.NoOverrides);

        var html = PageRenderer.Render(page, [], _tempDir, "Test Site");

        Assert.That(html, Does.Contain("<div class=\"mermaid\">"));
        Assert.That(html, Does.Contain("graph TD"));
    }

    [Test]
    public void Render_MarkdownLinkAndDirectoryLink_AreRewritten()
    {
        WriteFile(Path.Combine("sub", "README.md"), "# Sub\n");
        WriteFile("other.md", "# Other\n");
        var sourcePath = WriteFile("page.md", "# Page\n\n[Other](other.md) and [Sub](sub/)\n");
        var page = new SiteNode(sourcePath, "page.md", "page.html", "Page", SiteNodeKind.Document, SiteNode.NoOverrides);

        var html = PageRenderer.Render(page, [], _tempDir, "Test Site");

        Assert.That(html, Does.Contain("href=\"other.html\""));
        Assert.That(html, Does.Contain("href=\"sub/README.html\""));
    }

    [Test]
    public void Render_FolderStructure_YieldsDetailsNavEntries()
    {
        WriteFile("README.md", "# Home\n");
        WriteFile(Path.Combine("guide", "README.md"), "# Guide\n");
        var introPath = WriteFile(Path.Combine("guide", "intro.md"), "# Intro\n");

        var model = SiteAssembler.Assemble(_tempDir);
        Assert.That(model.Succeeded, Is.True);

        var introPage = model.Value!.Pages.Single(p => p.SourcePath == introPath);
        var html = PageRenderer.Render(introPage, model.Value.Nav, _tempDir, "Test Site");

        Assert.That(html, Does.Contain("<details"));
        Assert.That(html, Does.Contain("nav-section"));
        Assert.That(html, Does.Contain("Guide"));
    }

    [Test]
    public void Render_SourceLessNode_UsesGeneratedHtmlWithoutReadingSource()
    {
        var page = new SiteNode(
            SourcePath: null,
            RelativeSource: "",
            RelativeOutput: Path.Combine("guide", "index.html"),
            Title: "Guide",
            Kind: SiteNodeKind.NodeIndex,
            Overrides: SiteNode.NoOverrides,
            GeneratedHtml: "<h1>Guide</h1><p>Generated.</p>");

        var html = PageRenderer.Render(page, [], _tempDir, "Test Site");

        Assert.That(html, Does.Contain("<p>Generated.</p>"));
    }

    string WriteFile(string relativePath, string content)
    {
        var path = Path.Combine(_tempDir, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }
}
