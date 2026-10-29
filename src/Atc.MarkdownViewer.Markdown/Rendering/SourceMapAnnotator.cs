namespace Atc.MarkdownViewer.Markdown.Rendering;

/// <summary>
/// Adds source-map attributes to the parsed document before it is rendered (plan MD-002, MD-003).
/// </summary>
internal static class SourceMapAnnotator
{
    public static void Annotate(
        MarkdownDocument document,
        string source)
    {
        foreach (var node in document.Descendants())
        {
            switch (node)
            {
                case LiteralInline literal:
                    AnnotateLiteral(literal, source);
                    break;
                case CodeInline code:
                    AddRange(code);
                    AnnotateCodeInline(code, source);
                    break;
                case MathBlock math:
                    // Rendered with \[ \] delimiters added, so its text never maps 1:1.
                    AddRange(math);
                    break;
                case FencedCodeBlock fenced:
                    AddRange(fenced);
                    AnnotateCodeLines(fenced, source);
                    break;
                case MarkdownDocument:
                case HtmlBlock:
                case HtmlInline:
                case HtmlEntityInline:
                case LineBreakInline:
                case LinkReferenceDefinitionGroup:
                case LinkReferenceDefinition:
                case ContainerInline { Parent: null }:
                case ContainerInline and not (EmphasisInline or LinkInline):
                    // Written verbatim, rendered by MappedHtmlEntityInlineRenderer, or not rendered as an element.
                    break;
                default:
                    AddRange(node);
                    break;
            }
        }
    }

    /// <summary>
    /// Markdig spans are inclusive at the end; the HTML carries an exclusive end.
    /// </summary>
    private static void AddRange(MarkdownObject node)
    {
        if (node.Span.IsEmpty || node.Span.Start < 0)
        {
            return;
        }

        var attributes = node.GetAttributes();
        attributes.AddProperty(SourceMapAttributes.Start, SourceMapAttributes.Format(node.Span.Start));
        attributes.AddProperty(SourceMapAttributes.End, SourceMapAttributes.Format(node.Span.End + 1));
    }

    private static void AnnotateLiteral(
        LiteralInline literal,
        string source)
    {
        if (literal.Span.IsEmpty || literal.Span.Start < 0)
        {
            return;
        }

        var textStart = ExactTextStart(source, literal);
        literal.SetData(SourceMapAttributes.ExactKey, textStart >= 0);
        if (textStart < 0)
        {
            return;
        }

        literal.SetData(SourceMapAttributes.TextStartKey, textStart);
        if (!IsSoleChild(literal) || TextHost(literal) is not { } host)
        {
            return;
        }

        host.GetAttributes().AddProperty(SourceMapAttributes.TextStart, SourceMapAttributes.Format(textStart));
        literal.SetData(SourceMapAttributes.MappedByParentKey, true);
    }

    /// <summary>
    /// Finds where the literal's text sits in the source. Markdig's span can include surrounding whitespace
    /// (pipe table cells), which is tolerated; any other difference (escapes, emoji) makes the run inexact.
    /// </summary>
    /// <returns>The source offset of the text, or -1 when the text does not appear verbatim.</returns>
    private static int ExactTextStart(
        string source,
        LiteralInline literal)
    {
        var content = literal.Content.AsSpan();
        var start = literal.Span.Start;
        var length = Math.Min(literal.Span.Length, source.Length - start);
        if (content.IsEmpty || length < content.Length)
        {
            return -1;
        }

        var slice = source.AsSpan(start, length);
        if (slice.SequenceEqual(content))
        {
            return start;
        }

        var index = slice.IndexOf(content, StringComparison.Ordinal);
        return index >= 0 &&
               slice[..index].IsWhiteSpace() &&
               slice[(index + content.Length)..].IsWhiteSpace()
            ? start + index
            : -1;
    }

    private static void AnnotateCodeInline(
        CodeInline code,
        string source)
    {
        if (code.Span.IsEmpty || code.Span.Start < 0)
        {
            return;
        }

        var slice = source.AsSpan(code.Span.Start, Math.Min(code.Span.Length, source.Length - code.Span.Start));
        var index = slice.IndexOf(code.Content.AsSpan(), StringComparison.Ordinal);
        var attributes = code.GetAttributes();
        if (index >= 0 && code.Content.Length > 0)
        {
            attributes.AddProperty(SourceMapAttributes.TextStart, SourceMapAttributes.Format(code.Span.Start + index));
        }
        else
        {
            attributes.AddProperty(SourceMapAttributes.Inexact, "0");
        }
    }

    private static void AnnotateCodeLines(
        FencedCodeBlock fenced,
        string source)
    {
        var lines = fenced.Lines;
        if (lines.Count == 0)
        {
            return;
        }

        var first = lines.Lines[0].Slice;
        var last = lines.Lines[lines.Count - 1].Slice;
        var attributes = fenced.GetAttributes();
        attributes.AddProperty(SourceMapAttributes.TextStart, SourceMapAttributes.Format(first.Start));

        // The renderer writes the lines joined with '\n' (StringLineGroup.ToString() keeps the original line
        // endings, so it is not used); with CRLF or stripped indentation the text no longer matches the source.
        // The renderer also writes a '\n' after the last line, so that newline must match too.
        var text = string.Join('\n', Enumerable.Range(0, lines.Count).Select(i => lines.Lines[i].Slice.ToString())) + "\n";
        var length = last.End - first.Start + 2;
        if (length < 0 || !IsExact(source, first.Start, length, text.AsSpan()))
        {
            attributes.AddProperty(SourceMapAttributes.Inexact, "0");
        }
    }

    private static bool IsExact(
        string source,
        int start,
        int length,
        ReadOnlySpan<char> text)
        => start >= 0 &&
           length == text.Length &&
           start + length <= source.Length &&
           source.AsSpan(start, length).SequenceEqual(text);

    private static bool IsSoleChild(LiteralInline literal)
        => literal.Parent is { } parent &&
           ReferenceEquals(parent.FirstChild, literal) &&
           ReferenceEquals(parent.LastChild, literal);

    /// <summary>
    /// The element that renders the literal as its direct and only text, or <see langword="null"/> when the
    /// literal must carry its own wrapper.
    /// </summary>
    private static MarkdownObject? TextHost(LiteralInline literal)
        => literal.Parent switch
        {
            EmphasisInline emphasis => emphasis,
            LinkInline { IsImage: false } link => link,
            { Parent: null } root => root.ParentBlock switch
            {
                HeadingBlock heading => heading,
                ParagraphBlock paragraph when !IsImplicitParagraph(paragraph) => paragraph,
                _ => null,
            },
            _ => null,
        };

    /// <summary>
    /// Paragraphs in tight lists and table cells are rendered without a <c>&lt;p&gt;</c> element.
    /// </summary>
    private static bool IsImplicitParagraph(ParagraphBlock paragraph)
        => paragraph.Parent is TableCell ||
           paragraph.Parent is ListItemBlock { Parent: ListBlock { IsLoose: false } };
}