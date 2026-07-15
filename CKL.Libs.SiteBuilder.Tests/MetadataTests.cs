using CKL.Libs.SiteBuilder.Metadata;
using NUnit.Framework;

namespace CKL.Libs.SiteBuilder.Tests;

public class StructuralExtractorTests
{
    [Test]
    public void Extract_TypedDecisionFolder_ResolvesTypeAndStripsFamilyPrefixFromTitle()
    {
        var content =
            "# ADR-0018: A markdown document's metadata is resolved by precedence\n\n" +
            "- Date: 2026-07-14\n" +
            "- Status: accepted\n" +
            "- Tags: docs, metadata, knowledge-network\n";

        var metadata = StructuralExtractor.Extract(Path.Combine("docs", "decisions", "0018-x.md"), content);

        Assert.That(metadata.Type, Is.EqualTo("decision"));
        Assert.That(metadata.Title, Is.EqualTo("A markdown document's metadata is resolved by precedence"));
        Assert.That(metadata.Date, Is.EqualTo("2026-07-14"));
        Assert.That(metadata.State, Is.EqualTo("accepted"));
        Assert.That(metadata.Tags, Is.EqualTo(new[] { "docs", "metadata", "knowledge-network" }));
    }

    [Test]
    public void Extract_TypedIdeaFolder_ResolvesIdeaType()
    {
        var content = "# Idea 0014: A more structured method\n\n- Date: 2026-07-14\n- Status: seed\n";

        var metadata = StructuralExtractor.Extract(Path.Combine("docs", "ideas", "0014-x.md"), content);

        Assert.That(metadata.Type, Is.EqualTo("idea"));
        Assert.That(metadata.Title, Is.EqualTo("A more structured method"));
    }

    [Test]
    public void Extract_TypedPlanFolder_ResolvesPlanType()
    {
        var content = "# Plan 0014: Build the metadata-resolution pass\n\n- Date: 2026-07-14\n- Status: draft\n";

        var metadata = StructuralExtractor.Extract(Path.Combine("docs", "plans", "0014-x.md"), content);

        Assert.That(metadata.Type, Is.EqualTo("plan"));
        Assert.That(metadata.Title, Is.EqualTo("Build the metadata-resolution pass"));
    }

    [Test]
    public void Extract_NonDecisionTrailDocument_YieldsOnlyTitleAndFolderType()
    {
        var content = "# Getting Started\n\nSome ordinary prose, no header fields.\n";

        var metadata = StructuralExtractor.Extract(Path.Combine("guide", "intro.md"), content);

        Assert.That(metadata.Type, Is.EqualTo("guide"));
        Assert.That(metadata.Title, Is.EqualTo("Getting Started"));
        Assert.That(metadata.Date, Is.Null);
        Assert.That(metadata.State, Is.Null);
        Assert.That(metadata.Tags, Is.Null);
    }

    [Test]
    public void Extract_RootLevelDocument_YieldsGenericDocType()
    {
        var metadata = StructuralExtractor.Extract("README.md", "# Home\n");

        Assert.That(metadata.Type, Is.EqualTo("doc"));
        Assert.That(metadata.Title, Is.EqualTo("Home"));
    }
}

public class FrontmatterOverlayAndSeamTests
{
    [Test]
    public void Apply_FrontmatterPresent_FillsOnlyStructureEmptyFields()
    {
        var structural = new DocumentMetadata(Type: "decision", Title: "Some Title");
        var content =
            "---\n" +
            "type: idea\n" +
            "summary: A short synopsis.\n" +
            "perspective: author\n" +
            "---\n" +
            "# Some Title\n";

        var result = FrontmatterOverlay.Apply(structural, content);

        // structure wins on 'type' — frontmatter's 'idea' is a defect, not an override.
        Assert.That(result.Metadata.Type, Is.EqualTo("decision"));
        Assert.That(result.Defects, Has.Count.EqualTo(1));
        // fields structure left empty are filled from frontmatter.
        Assert.That(result.Metadata.Summary, Is.EqualTo("A short synopsis."));
        Assert.That(result.Metadata.Perspective, Is.EqualTo("author"));
    }

    [Test]
    public void Apply_NoFrontmatter_ReturnsStructuralMetadataUnchangedWithNoDefects()
    {
        var structural = new DocumentMetadata(Type: "doc", Title: "Plain");
        var result = FrontmatterOverlay.Apply(structural, "# Plain\n\nNo frontmatter here.\n");

        Assert.That(result.Metadata, Is.EqualTo(structural));
        Assert.That(result.Defects, Is.Empty);
    }

    [Test]
    public void NoOpMetadataInference_ReturnsEmptyResidue()
    {
        var metadata = new DocumentMetadata(Type: "doc");
        var result = NoOpMetadataInference.Instance.Infer(metadata, "content", metadata.EmptyFields);

        Assert.That(result, Is.Empty);
    }

    [Test]
    public void MetadataResolver_Resolve_IsDeterministicAndOfflineWithNoOpSeam()
    {
        var content = "# Plain\n\nJust prose.\n";

        var result = MetadataResolver.Resolve("doc.md", content, NoOpMetadataInference.Instance);

        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.Value!.Metadata.Summary, Is.Null);
        Assert.That(result.Value.Metadata.Perspective, Is.Null);
    }

    [Test]
    public void MetadataResolver_Resolve_InjectedSeamFillsOnlyEmptyResidue_NeverOverridesStructureOrFrontmatter()
    {
        var content =
            "---\n" +
            "summary: from frontmatter\n" +
            "---\n" +
            "# ADR-0099: A decided thing\n\n" +
            "- Date: 2026-07-14\n" +
            "- Status: accepted\n";

        var fakeSeam = new FakeInference(new Dictionary<string, string>
        {
            ["Type"] = "should-never-apply",
            ["Summary"] = "should-never-apply",
            ["Perspective"] = "inferred perspective",
        });

        var result = MetadataResolver.Resolve(Path.Combine("docs", "decisions", "0099-x.md"), content, fakeSeam);

        Assert.That(result.Succeeded, Is.True);
        var metadata = result.Value!.Metadata;
        Assert.That(metadata.Type, Is.EqualTo("decision")); // structural — seam's value discarded
        Assert.That(metadata.Summary, Is.EqualTo("from frontmatter")); // frontmatter — seam's value discarded
        Assert.That(metadata.Perspective, Is.EqualTo("inferred perspective")); // genuinely empty residue — seam fills it
        Assert.That(fakeSeam.WasCalledWithEmptyFields, Does.Not.Contain("Type"));
        Assert.That(fakeSeam.WasCalledWithEmptyFields, Does.Not.Contain("Summary"));
        Assert.That(fakeSeam.WasCalledWithEmptyFields, Does.Contain("Perspective"));
    }

    sealed class FakeInference(IReadOnlyDictionary<string, string> valuesToReturn) : IMetadataInference
    {
        public IReadOnlyList<string> WasCalledWithEmptyFields { get; private set; } = [];

        public IReadOnlyDictionary<string, string> Infer(DocumentMetadata resolvedSoFar, string content, IReadOnlyList<string> emptyFields)
        {
            WasCalledWithEmptyFields = emptyFields;
            return valuesToReturn;
        }
    }
}

public class MetadataIndexTests
{
    string _sourceDir = null!;

    [SetUp]
    public void SetUp() => _sourceDir = Directory.CreateTempSubdirectory("sitebuilder-metadata-").FullName;

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_sourceDir)) Directory.Delete(_sourceDir, recursive: true);
    }

    [Test]
    public void Build_ResolvesMetadataPerDocument_AndWritesNoIndexFileToDisk()
    {
        WriteFile("README.md", "# Home\n");
        WriteFile(Path.Combine("docs", "decisions", "0001-x.md"), "# ADR-0001: First decision\n\n- Date: 2026-01-01\n- Status: accepted\n");

        var entriesBefore = Directory.GetFileSystemEntries(_sourceDir, "*", SearchOption.AllDirectories).Length;

        var result = MetadataIndex.Build(_sourceDir, NoOpMetadataInference.Instance);

        Assert.That(result.Succeeded, Is.True);
        var homeMetadata = result.Value!.Get("README.md");
        Assert.That(homeMetadata, Is.Not.Null);
        Assert.That(homeMetadata!.Title, Is.EqualTo("Home"));

        var decisionMetadata = result.Value.Get(Path.Combine("docs", "decisions", "0001-x.md"));
        Assert.That(decisionMetadata, Is.Not.Null);
        Assert.That(decisionMetadata!.Type, Is.EqualTo("decision"));
        Assert.That(decisionMetadata.State, Is.EqualTo("accepted"));

        var entriesAfter = Directory.GetFileSystemEntries(_sourceDir, "*", SearchOption.AllDirectories).Length;
        Assert.That(entriesAfter, Is.EqualTo(entriesBefore));

        var indexFiles = Directory.GetFiles(_sourceDir, "*index*.json", SearchOption.AllDirectories);
        Assert.That(indexFiles, Is.Empty);
    }

    string WriteFile(string relativePath, string content)
    {
        var path = Path.Combine(_sourceDir, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }
}
