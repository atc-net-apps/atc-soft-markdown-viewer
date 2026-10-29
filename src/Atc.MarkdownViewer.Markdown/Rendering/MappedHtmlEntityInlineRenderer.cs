namespace Atc.MarkdownViewer.Markdown.Rendering;

/// <summary>
/// Writes an HTML entity such as <c>&amp;copy;</c> inside an inexact source-mapped <c>span</c>.
/// </summary>
internal sealed class MappedHtmlEntityInlineRenderer : HtmlObjectRenderer<HtmlEntityInline>
{
    protected override void Write(
        HtmlRenderer renderer,
        HtmlEntityInline obj)
    {
        var wrap = renderer.EnableHtmlForInline && !obj.Span.IsEmpty;
        if (wrap)
        {
            renderer.Write("<span ").Write(SourceMapAttributes.Start).Write("=\"").Write(SourceMapAttributes.Format(obj.Span.Start));
            renderer.Write("\" ").Write(SourceMapAttributes.End).Write("=\"").Write(SourceMapAttributes.Format(obj.Span.End + 1));
            renderer.Write("\" ").Write(SourceMapAttributes.Inexact).Write("=\"0\">");
        }

        if (renderer.EnableHtmlEscape)
        {
            var transcoded = obj.Transcoded;
            renderer.WriteEscape(ref transcoded);
        }
        else
        {
            renderer.Write(obj.Transcoded);
        }

        if (wrap)
        {
            renderer.Write("</span>");
        }
    }
}