namespace Atc.MarkdownViewer.Markdown.Tests;

public sealed class TableFormatterTests
{
    [Fact]
    public void Format_AlignsColumnsAndKeepsAlignmentMarkers()
    {
        // Arrange
        const string source = "Intro\n\n|Name|Status|Score|\n|:-|:-:|-:|\n|queue|done|7|\n|api|in progress|12|\n\nAfter\n";
        var offset = source.IndexOf("queue", StringComparison.Ordinal);

        // Act
        var edit = TableFormatter.Format(source, offset);

        // Assert
        edit.Should().NotBeNull();
        var formatted = source[..edit!.Start] + edit.NewText + source[edit.End..];
        formatted.Should().Be(
            "Intro\n\n" +
            "| Name  |   Status    | Score |\n" +
            "| :---- | :---------: | ----: |\n" +
            "| queue |    done     |     7 |\n" +
            "| api   | in progress |    12 |\n" +
            "\nAfter\n");
    }

    [Fact]
    public void Format_PadsMissingCells()
    {
        // Arrange
        const string source = "| a | b | c |\n| --- | --- | --- |\n| 1 |\n";

        // Act
        var edit = TableFormatter.Format(source, 0);

        // Assert
        edit!.NewText.Should().Be("| a   | b   | c   |\n| --- | --- | --- |\n| 1   |     |     |");
    }

    [Fact]
    public void Format_KeepsEscapedPipesAndCodeSpansInCells()
    {
        // Arrange
        const string source = "| expr | note |\n|---|---|\n| `a|b` | x \\| y |\n";

        // Act
        var edit = TableFormatter.Format(source, 0);

        // Assert
        edit!.NewText.Should().Contain("| `a|b` | x \\| y |");
    }

    [Fact]
    public void Format_KeepsCrlfAndIndentation()
    {
        // Arrange
        const string source = "  | a | b |\r\n  |---|---|\r\n  | 1 | 2 |\r\n";

        // Act
        var edit = TableFormatter.Format(source, 3);

        // Assert
        edit!.NewText.Should().Be("  | a   | b   |\r\n  | --- | --- |\r\n  | 1   | 2   |");
    }

    [Theory]
    [InlineData("Just a paragraph.\n", 3)]
    [InlineData("a | b\nnot a delimiter\n", 1)]
    [InlineData("| a |\n|---|\n", 99)]
    public void Format_OutsideATable_ReturnsNull(
        string source,
        int offset)
        => TableFormatter.Format(source, offset).Should().BeNull();
}