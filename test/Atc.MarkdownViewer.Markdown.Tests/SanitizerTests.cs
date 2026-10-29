namespace Atc.MarkdownViewer.Markdown.Tests;

/// <summary>
/// TEST-005: raw HTML is sanitized, and the source map survives.
/// </summary>
public sealed class SanitizerTests
{
    private readonly MarkdownRenderService service = new();

    [Theory]
    [InlineData("<img src=\"x\" onerror=\"alert(1)\">", "onerror")]
    [InlineData("<script>alert('x')</script>", "<script")]
    [InlineData("<a href=\"javascript:alert(1)\">bad</a>", "javascript:")]
    [InlineData("<iframe src=\"https://example.com\"></iframe>", "<iframe")]
    public void DangerousHtml_IsRemoved(
        string html,
        string forbidden)
        => Render(html).Should().NotContain(forbidden);

    [Fact]
    public void AllowedHtml_AndSourceMap_Survive()
    {
        // Act
        var html = Render("<details>\n<summary>More</summary>\n\nPress <kbd>Ctrl</kbd>.\n\n</details>");

        // Assert
        html.Should().Contain("<details>").And.Contain("<summary>").And.Contain("<kbd>").And.Contain("data-sp=");
    }

    [Fact]
    public void DocumentWithoutRawHtml_IsNotSanitized()
    {
        // Act
        var result = service.Render("- [x] done\n", RenderContext.ForText());

        // Assert: Markdig's own checkbox markup is kept byte for byte (a sanitizer would rewrite it).
        result.HasRawHtml.Should().BeFalse();
        result.Blocks[0].Html.Should().Contain("disabled=\"disabled\" type=\"checkbox\" checked=\"checked\" />");
    }

    private string Render(string markdown)
        => string.Concat(service.Render(markdown + "\n", RenderContext.ForText()).Blocks.Select(b => b.Html));
}