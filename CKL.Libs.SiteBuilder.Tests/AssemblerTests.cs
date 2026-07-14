using CKL.Libs.SiteBuilder.Assembly;
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

        // No staging folder: the assembler must not have written anything under the source tree.
        var entriesAfter = Directory.GetFileSystemEntries(_sourceDir, "*", SearchOption.AllDirectories).Length;
        Assert.That(entriesAfter, Is.EqualTo(entriesBefore));
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
