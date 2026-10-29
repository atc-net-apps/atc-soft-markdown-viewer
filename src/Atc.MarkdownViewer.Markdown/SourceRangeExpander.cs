namespace Atc.MarkdownViewer.Markdown;

/// <summary>
/// Widens a selected source range so "Copy as Markdown" never cuts markup in half (plan MD-005, SYNC-003).
/// </summary>
public static class SourceRangeExpander
{
    /// <summary>
    /// Expands <c>[start, end)</c> to the smallest range that contains only whole inline elements. Exact text
    /// runs may still be cut. A range that spans more than one leaf block is expanded from the start of the
    /// first block's source line (so list and quote markers are included) to the end of the last block.
    /// </summary>
    /// <param name="document">The parsed document of <paramref name="source"/>.</param>
    /// <param name="source">The source text.</param>
    /// <param name="start">The start offset (inclusive).</param>
    /// <param name="end">The end offset (exclusive).</param>
    /// <returns>The expanded range, end exclusive.</returns>
    public static (int Start, int End) Expand(
        MarkdownDocument document,
        string source,
        int start,
        int end)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(source);

        start = Math.Clamp(start, 0, source.Length);
        end = Math.Clamp(end, start, source.Length);
        if (start == end)
        {
            return (start, end);
        }

        var leaves = document.Descendants<LeafBlock>().Where(b => !b.Span.IsEmpty && Overlaps(b.Span, start, end)).ToList();
        if (leaves.Count > 1)
        {
            start = LineStart(source, Math.Min(start, leaves[0].Span.Start));
            end = Math.Max(end, leaves[^1].Span.End + 1);
        }

        // Expanding to one element can make the range cut into another, so repeat until nothing changes.
        bool changed;
        do
        {
            changed = false;
            foreach (var inline in document.Descendants<Inline>())
            {
                if (!MustBeWhole(inline) || inline.Span.IsEmpty || !Overlaps(inline.Span, start, end) || IsInside(inline.Span, start, end))
                {
                    continue;
                }

                start = Math.Min(start, inline.Span.Start);
                end = Math.Max(end, inline.Span.End + 1);
                changed = true;
            }
        }
        while (changed);

        return (start, Math.Min(end, source.Length));
    }

    private static bool MustBeWhole(Inline inline)
        => inline switch
        {
            LiteralInline literal => literal.GetData(SourceMapAttributes.ExactKey) is false || literal is Markdig.Extensions.Emoji.EmojiInline,
            ContainerInline { Parent: null } => false,
            LineBreakInline => false,
            _ => true,
        };

    private static bool Overlaps(
        SourceSpan span,
        int start,
        int end)
        => span.Start < end && span.End + 1 > start;

    private static bool IsInside(
        SourceSpan span,
        int start,
        int end)
        => span.Start >= start && span.End + 1 <= end;

    private static int LineStart(
        string source,
        int offset)
    {
        var index = offset > 0 ? source.LastIndexOf('\n', offset - 1) : -1;
        return index + 1;
    }
}