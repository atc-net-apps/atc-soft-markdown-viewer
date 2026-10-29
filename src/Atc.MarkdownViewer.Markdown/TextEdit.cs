namespace Atc.MarkdownViewer.Markdown;

/// <summary>
/// A replacement of a source range.
/// </summary>
/// <param name="Start">The start offset (inclusive).</param>
/// <param name="End">The end offset (exclusive).</param>
/// <param name="NewText">The text that replaces the range.</param>
public sealed record TextEdit(int Start, int End, string NewText);