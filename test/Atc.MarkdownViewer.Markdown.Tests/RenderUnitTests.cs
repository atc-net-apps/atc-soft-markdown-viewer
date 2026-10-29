namespace Atc.MarkdownViewer.Markdown.Tests;

/// <summary>
/// Render units (MD-008): raw HTML that wraps markdown blocks stays together.
/// </summary>
public sealed class RenderUnitTests
{
    private readonly MarkdownRenderService service = new();

    [Fact]
    public void Details_WithMarkdownInside_IsOneUnit()
    {
        // Arrange
        const string source = "Before.\n\n<details>\n<summary>More</summary>\n\nHidden **text**.\n\n</details>\n\nAfter.\n";

        // Act
        var blocks = service.Render(source, RenderContext.ForText()).Blocks;

        // Assert
        blocks.Should().HaveCount(3);
        blocks[1].Html.Should().Contain("<details>").And.Contain("<strong").And.Contain("</details>");
        source[blocks[1].SourceStart..blocks[1].SourceEnd].Should().StartWith("<details>").And.EndWith("</details>");
    }

    [Fact]
    public void UnclosedHtml_ExtendsToEndOfDocument()
    {
        // Arrange
        const string source = "<div>\n\nText.\n\nMore.\n";

        // Act
        var blocks = service.Render(source, RenderContext.ForText()).Blocks;

        // Assert
        blocks.Should().ContainSingle();
    }

    [Fact]
    public void EachMarkdownBlock_IsItsOwnUnit_WithStableHash()
    {
        // Arrange
        const string source = "# Title\n\nOne.\n\nTwo.\n";
        const string edited = "# Title\n\nOne changed.\n\nTwo.\n";

        // Act
        var before = service.Render(source, RenderContext.ForText()).Blocks;
        var after = service.Render(edited, RenderContext.ForText()).Blocks;

        // Assert
        before.Should().HaveCount(3);
        after[0].Hash.Should().Be(before[0].Hash);
        after[1].Hash.Should().NotBe(before[1].Hash);
        after[2].Hash.Should().Be(before[2].Hash);
    }

    [Theory]
    [InlineData("<details>", 1)]
    [InlineData("<details><summary>x</summary>", 1)]
    [InlineData("<div>\n<img src=\"a.png\">\n<br/>", 1)]
    [InlineData("<p>closed</p>", 0)]
    [InlineData("</details>", -1)]
    public void Depth_CountsOpenElements(
        string html,
        int expected)
    {
        // Arrange
        var document = Markdig.Markdown.Parse(html, MarkdownPipelineFactory.Default);
        var block = document.OfType<HtmlBlock>().Single();

        // Act & Assert
        RenderUnitBuilder.Depth(block).Should().Be(expected);
    }
}