namespace Atc.MarkdownViewer.Markdown.Rendering;

/// <summary>
/// Switches for tests that compare the source-mapped output with Markdig's default output.
/// </summary>
/// <param name="RewriteLinks">Run <see cref="LinkRewriter"/>.</param>
/// <param name="Sanitize">Sanitize units with raw HTML.</param>
internal sealed record RenderOptions(bool RewriteLinks, bool Sanitize)
{
    public static RenderOptions Default { get; } = new(RewriteLinks: true, Sanitize: true);
}