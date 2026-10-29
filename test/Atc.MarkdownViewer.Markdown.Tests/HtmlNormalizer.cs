namespace Atc.MarkdownViewer.Markdown.Tests;

/// <summary>
/// Removes everything the source-mapped renderer adds, so its output can be compared with Markdig's default
/// HTML (plan TEST-002).
/// </summary>
public static partial class HtmlNormalizer
{
    public static string StripSourceMap(string html)
    {
        var result = WrapperSpan().Replace(html, "${text}");
        result = SourceMapAttribute().Replace(result, string.Empty);
        return FrontMatter().Replace(result, string.Empty);
    }

    [GeneratedRegex("""<span data-sp="\d+" data-ep="\d+"(?: data-x="0")?>(?<text>[^<]*)</span>""", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 5000)]
    private static partial Regex WrapperSpan();

    [GeneratedRegex(@" data-(?:sp|ep|ts|x)=""[^""]*""", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 5000)]
    private static partial Regex SourceMapAttribute();

    [GeneratedRegex("""<details class="mdv-frontmatter".*?</details>\n?""", RegexOptions.Singleline | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 5000)]
    private static partial Regex FrontMatter();
}