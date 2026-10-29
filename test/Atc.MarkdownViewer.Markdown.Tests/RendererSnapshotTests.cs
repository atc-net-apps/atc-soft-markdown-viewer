namespace Atc.MarkdownViewer.Markdown.Tests;

/// <summary>
/// TEST-001: each GFM fixture renders to its committed snapshot. Run with <c>UPDATE_SNAPSHOTS=1</c> to
/// rewrite the snapshots after an intended change, then review the diff.
/// </summary>
public sealed class RendererSnapshotTests
{
    private readonly MarkdownRenderService service = new();

    public static TheoryData<string> Fixtures => FixtureFiles.GfmNamesData;

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void Render_MatchesSnapshot(string fixture)
    {
        // Arrange
        var source = FixtureFiles.ReadGfm(fixture);

        // Act
        var html = ToSnapshot(service.Render(source, FixtureFiles.GfmContext));

        // Assert
        var snapshotPath = Path.Combine(FixtureFiles.SourceSnapshotDirectory, fixture + ".html");
        if (Environment.GetEnvironmentVariable("UPDATE_SNAPSHOTS") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(snapshotPath)!);
            File.WriteAllText(snapshotPath, html);
            return;
        }

        File.Exists(snapshotPath).Should().BeTrue($"snapshot {fixture}.html should exist; run the tests once with UPDATE_SNAPSHOTS=1");
        html.Should().Be(File.ReadAllText(snapshotPath).ReplaceLineEndings("\n"));
    }

    /// <summary>
    /// One line per unit header (source range and hash), followed by the unit's HTML.
    /// </summary>
    private static string ToSnapshot(RenderResult result)
    {
        var sb = new StringBuilder();
        foreach (var block in result.Blocks)
        {
            sb.Append("<!-- unit ")
                .Append(block.SourceStart.ToString(CultureInfo.InvariantCulture))
                .Append("..")
                .Append(block.SourceEnd.ToString(CultureInfo.InvariantCulture))
                .Append(" -->\n")
                .Append(block.Html.ReplaceLineEndings("\n"));
        }

        return sb.ToString();
    }
}