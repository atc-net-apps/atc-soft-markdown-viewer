namespace Atc.MarkdownViewer.Markdown.Rendering;

/// <summary>
/// Facts gathered in one pass over the parsed document: outline, word count and which lazy features it needs.
/// </summary>
internal sealed record DocumentFacts(
    IReadOnlyList<OutlineItem> Outline,
    int WordCount,
    bool HasRawHtml,
    bool HasMermaid,
    bool HasMath)
{
    public static DocumentFacts Collect(MarkdownDocument document)
    {
        var outline = new List<OutlineItem>();
        var words = 0;
        bool hasRawHtml = false, hasMermaid = false, hasMath = false;
        foreach (var node in document.Descendants())
        {
            switch (node)
            {
                case HeadingBlock heading:
                    outline.Add(new OutlineItem(heading.Level, PlainText(heading.Inline), heading.GetAttributes().Id ?? string.Empty, heading.Span.Start));
                    break;
                case LiteralInline literal:
                    words += CountWords(literal.Content.AsSpan());
                    break;
                case HtmlBlock or HtmlInline:
                    hasRawHtml = true;
                    break;
                case FencedCodeBlock { Info: { } info } when info.Equals("mermaid", StringComparison.OrdinalIgnoreCase):
                    hasMermaid = true;
                    break;
                case MathBlock or MathInline:
                    hasMath = true;
                    break;
            }
        }

        return new DocumentFacts(outline, words, hasRawHtml, hasMermaid, hasMath);
    }

    private static string PlainText(ContainerInline? container)
    {
        if (container is null)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        foreach (var inline in container.Descendants<Inline>())
        {
            switch (inline)
            {
                case LiteralInline literal:
                    sb.Append(literal.Content.AsSpan());
                    break;
                case CodeInline code:
                    sb.Append(code.Content);
                    break;
                case HtmlEntityInline entity:
                    sb.Append(entity.Transcoded.AsSpan());
                    break;
            }
        }

        return sb.ToString().Trim();
    }

    private static int CountWords(ReadOnlySpan<char> text)
    {
        var count = 0;
        var inWord = false;
        foreach (var c in text)
        {
            if (char.IsWhiteSpace(c))
            {
                inWord = false;
            }
            else if (!inWord)
            {
                inWord = true;
                count++;
            }
        }

        return count;
    }
}