namespace Atc.MarkdownViewer.Markdown.Tests;

/// <summary>
/// TEST-003: every exact text mapping in the HTML points at exactly that text in the source.
/// </summary>
public sealed partial class SourceMapRoundTripTests
{
    private readonly MarkdownRenderService service = new();

    public static TheoryData<string> Fixtures => FixtureFiles.GfmNamesData;

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void Fixture_Lf_MappingsMatchSource(string fixture)
        => AssertMappings(FixtureFiles.ReadGfm(fixture));

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void Fixture_CrlfAndTabs_MappingsMatchSource(string fixture)
        => AssertMappings(FixtureFiles.ReadGfm(fixture).Replace("\n", "\r\n", StringComparison.Ordinal).Replace("    ", "\t", StringComparison.Ordinal));

    [Fact]
    public void PlainParagraph_CarriesTextStartOnParagraph()
    {
        // Act
        var html = service.Render("Hello world.\n", RenderContext.ForText()).Blocks[0].Html;

        // Assert
        html.Should().Be("<p data-sp=\"0\" data-ep=\"12\" data-ts=\"0\">Hello world.</p>\n");
    }

    [Fact]
    public void Escape_IsMarkedInexact()
    {
        // Act
        var html = service.Render("a \\* b\n", RenderContext.ForText()).Blocks[0].Html;

        // Assert: Markdig merges the escape and the following text into one run, which is then inexact.
        html.Should().Contain("<span data-sp=\"2\" data-ep=\"6\" data-x=\"0\">* b</span>");
    }

    [Fact]
    public void TightListItem_WrapsTextInSpan()
    {
        // Act
        var html = service.Render("- item\n", RenderContext.ForText()).Blocks[0].Html;

        // Assert
        html.Should().Contain("<span data-sp=\"2\" data-ep=\"6\">item</span>");
    }

    private void AssertMappings(string source)
    {
        var html = string.Concat(service.Render(source, RenderContext.ForText()).Blocks.Select(b => b.Html));

        foreach (var match in TextStartElement().Matches(html).Cast<Match>())
        {
            var start = int.Parse(match.Groups["ts"].Value, CultureInfo.InvariantCulture);
            var text = WebUtility.HtmlDecode(match.Groups["text"].Value);
            source.Substring(start, text.Length).Should().Be(text, $"data-ts={start} in {match.Value}");
        }

        foreach (var match in RangeSpan().Matches(html).Cast<Match>())
        {
            var start = int.Parse(match.Groups["sp"].Value, CultureInfo.InvariantCulture);
            var end = int.Parse(match.Groups["ep"].Value, CultureInfo.InvariantCulture);
            var text = WebUtility.HtmlDecode(match.Groups["text"].Value);
            source[start..end].Should().Be(text, $"span {start}..{end} in {match.Value}");
        }
    }

    /// <summary>An element with data-ts, no data-x, and only text inside.</summary>
    [GeneratedRegex("""<(?<tag>[a-z0-9]+)(?:(?! data-x=)[^>])*? data-ts="(?<ts>\d+)"(?:(?! data-x=)[^>])*>(?<text>[^<]*)</\k<tag>>""", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 5000)]
    private static partial Regex TextStartElement();

    /// <summary>A wrapper span without data-x.</summary>
    [GeneratedRegex("""<span data-sp="(?<sp>\d+)" data-ep="(?<ep>\d+)">(?<text>[^<]*)</span>""", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 5000)]
    private static partial Regex RangeSpan();
}