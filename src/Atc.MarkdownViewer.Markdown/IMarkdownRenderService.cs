namespace Atc.MarkdownViewer.Markdown;

/// <summary>
/// Renders markdown to source-mapped HTML (plan MD-001).
/// </summary>
public interface IMarkdownRenderService
{
    /// <summary>
    /// Parses and renders a document.
    /// </summary>
    /// <param name="source">The markdown source.</param>
    /// <param name="context">Where the document lives, for resolving links and images.</param>
    /// <returns>The render units, outline and document facts.</returns>
    RenderResult Render(
        string source,
        RenderContext context);
}