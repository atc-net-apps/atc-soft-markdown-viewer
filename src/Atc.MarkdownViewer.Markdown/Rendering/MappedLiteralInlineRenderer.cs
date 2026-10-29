namespace Atc.MarkdownViewer.Markdown.Rendering;

/// <summary>
/// Writes a text run. Runs not already mapped by their parent element (<c>data-ts</c>) are wrapped in a
/// <c>span</c> carrying their source range: the exact text range for exact runs, or the whole source span
/// with <c>data-x="0"</c> for inexact ones.
/// </summary>
internal sealed class MappedLiteralInlineRenderer : HtmlObjectRenderer<LiteralInline>
{
    protected override void Write(
        HtmlRenderer renderer,
        LiteralInline obj)
    {
        if (obj.Content.IsEmpty)
        {
            return;
        }

        var wrap = renderer.EnableHtmlForInline &&
                   !obj.Span.IsEmpty &&
                   obj.GetData(SourceMapAttributes.MappedByParentKey) is not true;
        if (wrap)
        {
            WriteOpeningSpan(renderer, obj);
        }

        if (renderer.EnableHtmlEscape)
        {
            renderer.WriteEscape(ref obj.Content);
        }
        else
        {
            renderer.Write(ref obj.Content);
        }

        if (wrap)
        {
            renderer.Write("</span>");
        }
    }

    private static void WriteOpeningSpan(
        HtmlRenderer renderer,
        LiteralInline obj)
    {
        var exactStart = obj.GetData(SourceMapAttributes.TextStartKey) as int?;
        var start = exactStart ?? obj.Span.Start;
        var end = exactStart is { } s ? s + obj.Content.Length : obj.Span.End + 1;

        renderer.Write("<span ").Write(SourceMapAttributes.Start).Write("=\"").Write(SourceMapAttributes.Format(start));
        renderer.Write("\" ").Write(SourceMapAttributes.End).Write("=\"").Write(SourceMapAttributes.Format(end)).Write('"');
        if (exactStart is null)
        {
            renderer.Write(' ').Write(SourceMapAttributes.Inexact).Write("=\"0\"");
        }

        renderer.Write('>');
    }
}