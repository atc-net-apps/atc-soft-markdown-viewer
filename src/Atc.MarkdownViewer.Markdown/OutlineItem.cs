namespace Atc.MarkdownViewer.Markdown;

/// <summary>
/// A heading for the table of contents (plan REQ-005).
/// </summary>
/// <param name="Level">The heading level, 1 to 6.</param>
/// <param name="Text">The heading as plain text.</param>
/// <param name="Id">The element id, in GitHub's format.</param>
/// <param name="SourceStart">The start offset of the heading in the source.</param>
public sealed record OutlineItem(int Level, string Text, string Id, int SourceStart);