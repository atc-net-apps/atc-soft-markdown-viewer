namespace Atc.MarkdownViewer.Markdown.Tests;

/// <summary>
/// TEST-002: with the source-map additions stripped, the output equals Markdig's own HTML for the same
/// pipeline, for every fixture and every GFM spec example.
/// </summary>
public sealed class ConformanceTests
{
    private static readonly RenderOptions Raw = new(RewriteLinks: false, Sanitize: false);
    private readonly MarkdownRenderService service = new();

    public static TheoryData<string> Fixtures => FixtureFiles.GfmNamesData;

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void Fixture_MatchesMarkdigDefault(string fixture)
        => AssertMatchesDefault(FixtureFiles.ReadGfm(fixture));

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void Fixture_WithCrlfAndTabs_MatchesMarkdigDefault(string fixture)
        => AssertMatchesDefault(FixtureFiles.ReadGfm(fixture).Replace("\n", "\r\n", StringComparison.Ordinal).Replace("    ", "\t", StringComparison.Ordinal));

    [Fact]
    public void GfmSpecExamples_MatchMarkdigDefault()
    {
        // Arrange
        var examples = GfmSpecExample.Load();
        var failures = new List<string>();

        // Act
        foreach (var example in examples)
        {
            var expected = Markdig.Markdown.ToHtml(example.Markdown, MarkdownPipelineFactory.Default);
            var actual = Render(example.Markdown);
            if (!string.Equals(actual, expected, StringComparison.Ordinal))
            {
                failures.Add($"{example}\n--- markdown\n{example.Markdown}--- expected\n{expected}--- actual\n{actual}");
            }
        }

        // Assert
        examples.Should().HaveCountGreaterThan(600);
        failures.Should().BeEmpty();
    }

    private void AssertMatchesDefault(string source)
        => Render(source).Should().Be(Markdig.Markdown.ToHtml(source, MarkdownPipelineFactory.Default));

    private string Render(string source)
    {
        var result = service.Render(source, RenderContext.ForText(), Raw);
        return HtmlNormalizer.StripSourceMap(string.Concat(result.Blocks.Select(b => b.Html)));
    }
}