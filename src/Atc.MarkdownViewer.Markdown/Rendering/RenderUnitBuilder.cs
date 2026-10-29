namespace Atc.MarkdownViewer.Markdown.Rendering;

/// <summary>
/// Groups top-level blocks into render units (plan MD-008). A top-level HTML block that leaves an element
/// open (such as <c>&lt;details&gt;</c>) absorbs the following blocks until the element is closed, so the
/// unit can be sanitized and patched as a whole.
/// </summary>
internal static partial class RenderUnitBuilder
{
    private static readonly HashSet<string> VoidElements = new(StringComparer.OrdinalIgnoreCase)
    {
        "area", "base", "br", "col", "embed", "hr", "img", "input", "link", "meta", "source", "track", "wbr",
    };

    public static List<List<Block>> Build(MarkdownDocument document)
    {
        var units = new List<List<Block>>(document.Count);
        List<Block>? open = null;
        var depth = 0;
        foreach (var block in document)
        {
            if (block is LinkReferenceDefinitionGroup)
            {
                continue;
            }

            if (open is not null)
            {
                open.Add(block);
                if (block is HtmlBlock html)
                {
                    depth += Depth(html);
                }

                if (depth <= 0)
                {
                    units.Add(open);
                    open = null;
                }

                continue;
            }

            if (block is HtmlBlock opening && Depth(opening) is var opened and > 0)
            {
                open = [block];
                depth = opened;
                continue;
            }

            units.Add([block]);
        }

        if (open is not null)
        {
            units.Add(open);
        }

        return units;
    }

    /// <summary>
    /// Counts the elements the HTML block opens minus the number it closes.
    /// </summary>
    /// <param name="block">The raw HTML block.</param>
    /// <returns>The net number of elements left open.</returns>
    internal static int Depth(HtmlBlock block)
    {
        var depth = 0;
        foreach (var groups in Tag().Matches(block.Lines.ToString()).Select(m => m.Groups))
        {
            if (groups["close"].Success)
            {
                depth--;
            }
            else if (!groups["self"].Success && !VoidElements.Contains(groups["name"].Value))
            {
                depth++;
            }
        }

        return depth;
    }

    [GeneratedRegex(@"<(?<close>/)?(?<name>[A-Za-z][A-Za-z0-9-]*)(?:\s[^<>]*?)?(?<self>/)?>", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Tag();
}