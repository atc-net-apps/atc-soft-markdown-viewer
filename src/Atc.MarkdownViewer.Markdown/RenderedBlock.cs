namespace Atc.MarkdownViewer.Markdown;

/// <summary>
/// One render unit: a top-level block, or a raw HTML element with the blocks it wraps (plan MD-008).
/// </summary>
/// <param name="Hash">A 64-bit XxHash3 of the unit's source text, in hex. Used to patch only changed units.</param>
/// <param name="Html">The rendered HTML.</param>
/// <param name="SourceStart">The start offset of the unit in the source (inclusive).</param>
/// <param name="SourceEnd">The end offset of the unit in the source (exclusive).</param>
public sealed record RenderedBlock(string Hash, string Html, int SourceStart, int SourceEnd);