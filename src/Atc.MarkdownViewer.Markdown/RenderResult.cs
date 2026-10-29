namespace Atc.MarkdownViewer.Markdown;

/// <summary>
/// The result of rendering a document (plan MD-001).
/// </summary>
/// <param name="Source">The source text that was rendered.</param>
/// <param name="Document">The parsed document, kept for <see cref="SourceRangeExpander"/>.</param>
/// <param name="Blocks">The render units in document order.</param>
/// <param name="Outline">The headings in document order.</param>
/// <param name="WordCount">The number of words in the document's text.</param>
/// <param name="HasRawHtml">Whether the document contains raw HTML.</param>
/// <param name="HasMermaid">Whether the document contains a mermaid fence.</param>
/// <param name="HasMath">Whether the document contains math.</param>
public sealed record RenderResult(
    string Source,
    MarkdownDocument Document,
    IReadOnlyList<RenderedBlock> Blocks,
    IReadOnlyList<OutlineItem> Outline,
    int WordCount,
    bool HasRawHtml,
    bool HasMermaid,
    bool HasMath);