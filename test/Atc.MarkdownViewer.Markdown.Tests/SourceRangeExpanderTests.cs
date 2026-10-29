namespace Atc.MarkdownViewer.Markdown.Tests;

/// <summary>
/// TEST-004: "Copy as Markdown" ranges never cut markup in half.
/// </summary>
public sealed class SourceRangeExpanderTests
{
    private readonly MarkdownRenderService service = new();

    [Fact]
    public void Expand_InsidePlainText_KeepsRange()
    {
        // Arrange
        const string source = "- [x] Map consumers to owners\n";
        var start = source.IndexOf("Map consumers", StringComparison.Ordinal);

        // Act
        var (s, e) = Expand(source, start, start + "Map consumers".Length);

        // Assert
        source[s..e].Should().Be("Map consumers");
    }

    [Fact]
    public void Expand_StartingInsideBold_IncludesBothMarkers()
    {
        // Arrange
        const string source = "Some **bold** text\n";
        var start = source.IndexOf("old", StringComparison.Ordinal);

        // Act
        var (s, e) = Expand(source, start, source.IndexOf("text", StringComparison.Ordinal) + 4);

        // Assert
        source[s..e].Should().Be("**bold** text");
    }

    [Fact]
    public void Expand_AcrossTwoParagraphs_TakesWholeBlocks()
    {
        // Arrange
        const string source = "First paragraph here.\n\nSecond paragraph here.\n";

        // Act
        var (s, e) = Expand(source, source.IndexOf("paragraph", StringComparison.Ordinal), source.IndexOf("Second", StringComparison.Ordinal) + 3);

        // Assert
        source[s..e].Should().Be("First paragraph here.\n\nSecond paragraph here.");
    }

    [Fact]
    public void Expand_AcrossListItems_IncludesFirstMarker()
    {
        // Arrange
        const string source = "- alpha one\n- beta two\n";

        // Act
        var (s, e) = Expand(source, source.IndexOf("one", StringComparison.Ordinal), source.IndexOf("beta", StringComparison.Ordinal) + 2);

        // Assert
        source[s..e].Should().Be("- alpha one\n- beta two");
    }

    [Fact]
    public void Expand_InsideLinkText_TakesWholeLink()
    {
        // Arrange
        const string source = "See [the docs](https://example.com) now\n";

        // Act
        var (s, e) = Expand(source, source.IndexOf("docs", StringComparison.Ordinal), source.IndexOf("docs", StringComparison.Ordinal) + 4);

        // Assert
        source[s..e].Should().Be("[the docs](https://example.com)");
    }

    [Fact]
    public void Expand_PartOfEscape_TakesWholeInexactRun()
    {
        // Arrange: Markdig merges "\*" and " b" into one run whose text differs from its source.
        const string source = "a \\* b\n";
        var star = source.IndexOf('*', StringComparison.Ordinal);

        // Act
        var (s, e) = Expand(source, star, star + 1);

        // Assert
        source[s..e].Should().Be("\\* b");
    }

    [Fact]
    public void Expand_EmptyRange_ReturnsItUnchanged()
        => Expand("text\n", 2, 2).Should().Be((2, 2));

    private (int Start, int End) Expand(
        string source,
        int start,
        int end)
    {
        var result = service.Render(source, RenderContext.ForText());
        return SourceRangeExpander.Expand(result.Document, source, start, end);
    }
}