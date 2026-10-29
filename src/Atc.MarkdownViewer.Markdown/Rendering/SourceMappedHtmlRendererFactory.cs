namespace Atc.MarkdownViewer.Markdown.Rendering;

/// <summary>
/// Creates Markdig's <see cref="HtmlRenderer"/> with the source-mapping renderers swapped in (plan MD-003).
/// </summary>
internal static class SourceMappedHtmlRendererFactory
{
    public static HtmlRenderer Create(
        TextWriter writer,
        MarkdownPipeline pipeline)
    {
        var renderer = new HtmlRenderer(writer);
        pipeline.Setup(renderer);

        Replace<Markdig.Renderers.Html.Inlines.LiteralInlineRenderer>(renderer, new MappedLiteralInlineRenderer());
        Replace<Markdig.Renderers.Html.Inlines.HtmlEntityInlineRenderer>(renderer, new MappedHtmlEntityInlineRenderer());
        renderer.ObjectRenderers.RemoveAll(r => r is YamlFrontMatterHtmlRenderer);
        renderer.ObjectRenderers.Insert(0, new FrontMatterHtmlRenderer());
        return renderer;
    }

    private static void Replace<TOriginal>(
        HtmlRenderer renderer,
        IMarkdownObjectRenderer replacement)
        where TOriginal : IMarkdownObjectRenderer
    {
        if (!renderer.ObjectRenderers.Replace<TOriginal>(replacement))
        {
            throw new InvalidOperationException($"Markdig renderer {typeof(TOriginal).Name} was not found.");
        }
    }
}