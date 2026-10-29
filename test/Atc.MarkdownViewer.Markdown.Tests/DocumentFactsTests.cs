namespace Atc.MarkdownViewer.Markdown.Tests;

/// <summary>
/// Outline, word count and lazy-feature flags (MD-001, REQ-005, REQ-014).
/// </summary>
public sealed class DocumentFactsTests
{
    private readonly MarkdownRenderService service = new();

    [Fact]
    public void Outline_ListsHeadingsWithGitHubIds()
    {
        // Act
        var outline = service.Render(FixtureFiles.ReadGfm("headings"), RenderContext.ForText()).Outline;

        // Assert
        outline.Select(o => (o.Level, o.Text, o.Id)).Should().Equal(
            (1, "Migration plan", "migration-plan"),
            (2, "Phase 1 · Inventory", "phase-1--inventory"),
            (3, "Map consumers to owners", "map-consumers-to-owners"),
            (4, "Deep heading", "deep-heading"),
            (1, "Setext heading", "setext-heading"),
            (2, "Another setext", "another-setext"));
    }

    [Fact]
    public void Outline_SourceStart_PointsAtHeading()
    {
        // Arrange
        const string source = "Intro.\n\n## Second\n";

        // Act
        var item = service.Render(source, RenderContext.ForText()).Outline.Single();

        // Assert
        source[item.SourceStart..].Should().StartWith("## Second");
    }

    [Fact]
    public void WordCount_CountsTextWords()
        => service.Render("One two *three* `code`.\n\n- four five\n", RenderContext.ForText()).WordCount.Should().Be(6);

    [Theory]
    [InlineData("mermaid", false, true, false)]
    [InlineData("math", false, false, true)]
    [InlineData("raw-html", true, false, false)]
    [InlineData("emphasis", false, false, false)]
    public void Flags_DescribeLazyFeatures(
        string fixture,
        bool rawHtml,
        bool mermaid,
        bool math)
    {
        // Act
        var result = service.Render(FixtureFiles.ReadGfm(fixture), RenderContext.ForText());

        // Assert
        (result.HasRawHtml, result.HasMermaid, result.HasMath).Should().Be((rawHtml, mermaid, math));
    }

    [Fact]
    public void FrontMatter_RendersAsCollapsedTable()
    {
        // Act
        var html = service.Render(FixtureFiles.ReadGfm("front-matter"), RenderContext.ForText()).Blocks[0].Html;

        // Assert
        html.Should().StartWith("<details class=\"mdv-frontmatter\"")
            .And.Contain("<tr><th>title</th><td>Benchmark fixture</td></tr>")
            .And.Contain("<tr><th>owner</th><td>atc-net-apps</td></tr>");
    }

    [Fact]
    public void FrontMatter_WithNestedYaml_RendersAsPreformatted()
    {
        // Act
        var html = service.Render("---\ntags:\n  - a\n  - b\n---\n", RenderContext.ForText()).Blocks[0].Html;

        // Assert
        html.Should().Contain("<pre><code>tags:\n  - a\n  - b</code></pre>");
    }
}