namespace Atc.MarkdownViewer.Markdown.Rendering;

/// <summary>
/// Names of the source-map attributes written into the HTML (plan SYNC-001, MD-003).
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><c>data-sp</c>/<c>data-ep</c>: the element's source range as UTF-16 offsets, end exclusive.</item>
/// <item><c>data-ts</c>: on an element whose only content is one exact text run, the source offset of that text.</item>
/// <item><c>data-x="0"</c>: the element's text differs from its source (escapes, entities, emoji), so offsets inside it are not exact.</item>
/// </list>
/// </remarks>
internal static class SourceMapAttributes
{
    public const string Start = "data-sp";
    public const string End = "data-ep";
    public const string TextStart = "data-ts";
    public const string Inexact = "data-x";

    /// <summary>Data key on a <see cref="LiteralInline"/>: its text equals its source slice.</summary>
    public const string ExactKey = "mdv.exact";

    /// <summary>Data key on an exact <see cref="LiteralInline"/>: the source offset where its text starts.</summary>
    public const string TextStartKey = "mdv.text-start";

    /// <summary>Data key on a <see cref="LiteralInline"/>: its parent element carries <c>data-ts</c>, so no wrapper is needed.</summary>
    public const string MappedByParentKey = "mdv.mapped-by-parent";

    public static string Format(int value)
        => value.ToString(CultureInfo.InvariantCulture);
}