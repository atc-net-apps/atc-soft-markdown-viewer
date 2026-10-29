using System.Diagnostics;
using System.Globalization;
using System.IO.Hashing;
using System.Runtime.InteropServices;
using System.Text;
using Ganss.Xss;
using Markdig;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Renderers.Html.Inlines;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace SpikeMarkdown;

public sealed record RenderedBlock(string Hash, string Html);

public sealed record RenderOutput(IReadOnlyList<RenderedBlock> Blocks, bool HasRawHtml, double ParseMs, double RenderMs);

/// <summary>
/// Prototype of the source-mapped renderer (plan MD-002/MD-003): every block and inline element gets
/// data-sp/data-ep (UTF-16 offsets, end exclusive), and literal runs that are not the sole child are
/// wrapped in a span carrying their range.
/// </summary>
public static class SpikeRenderer
{
    public static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UsePreciseSourceLocation()
        .UseAlertBlocks()
        .UsePipeTables()
        .UseTaskLists()
        .UseFootnotes()
        .UseAutoLinks()
        .UseEmphasisExtras(Markdig.Extensions.EmphasisExtras.EmphasisExtraOptions.Strikethrough)
        .UseEmojiAndSmiley(enableSmileys: false)
        .UseYamlFrontMatter()
        .UseMathematics()
        .UseDiagrams()
        .UseAutoIdentifiers(Markdig.Extensions.AutoIdentifiers.AutoIdentifierOptions.GitHub)
        .Build();

    private static readonly HtmlSanitizer Sanitizer = CreateSanitizer();

    public static MarkdownDocument Parse(string source) => Markdown.Parse(source, Pipeline);

    public static string ToHtmlDefault(string source) => Markdown.ToHtml(source, Pipeline);

    public static RenderOutput Render(string source)
    {
        var sw = Stopwatch.StartNew();
        var document = Parse(source);
        var parseMs = sw.Elapsed.TotalMilliseconds;

        sw.Restart();
        var hasRawHtml = Annotate(document);

        var writer = new StringWriter(new StringBuilder(source.Length * 2), CultureInfo.InvariantCulture);
        var renderer = new HtmlRenderer(writer);
        Pipeline.Setup(renderer);
        renderer.ObjectRenderers.Replace<LiteralInlineRenderer>(new MappedLiteralRenderer());
        var sb = writer.GetStringBuilder();

        var blocks = new List<RenderedBlock>(document.Count);
        foreach (var block in document)
        {
            if (block is LinkReferenceDefinitionGroup)
            {
                continue;
            }

            sb.Clear();
            renderer.Render(block);
            writer.Flush();

            // Not sanitized per block: raw HTML such as <details>...</details> can span several
            // top-level blocks, so sanitizing is measured on the assembled document (Sanitize).
            blocks.Add(new RenderedBlock(HashOf(source, block.Span), sb.ToString()));
        }

        return new RenderOutput(blocks, hasRawHtml, parseMs, sw.Elapsed.TotalMilliseconds);
    }

    public static string Sanitize(string html) => Sanitizer.Sanitize(html);

    private static bool Annotate(MarkdownDocument document)
    {
        var hasRawHtml = false;
        foreach (var node in document.Descendants())
        {
            if (node is HtmlBlock or HtmlInline)
            {
                hasRawHtml = true;
            }

            // Literal runs are mapped by MappedLiteralRenderer; the document itself needs no range.
            if (node is LiteralInline or MarkdownDocument || node.Span.IsEmpty)
            {
                continue;
            }

            // Only nodes rendered as their own element carry attributes.
            if (node is ContainerInline and not (EmphasisInline or LinkInline))
            {
                continue;
            }

            var attributes = node.GetAttributes();
            attributes.AddProperty("data-sp", node.Span.Start.ToString(CultureInfo.InvariantCulture));
            attributes.AddProperty("data-ep", (node.Span.End + 1).ToString(CultureInfo.InvariantCulture));
        }

        return hasRawHtml;
    }

    private static string HashOf(string source, SourceSpan span)
    {
        if (span.IsEmpty || span.Start < 0)
        {
            return "0000000000000000";
        }

        var length = Math.Min(span.Length, source.Length - span.Start);
        var bytes = MemoryMarshal.AsBytes(source.AsSpan(span.Start, length));
        return XxHash3.HashToUInt64(bytes).ToString("x16", CultureInfo.InvariantCulture);
    }

    private static HtmlSanitizer CreateSanitizer()
    {
        var sanitizer = new HtmlSanitizer { AllowDataAttributes = true };
        foreach (var tag in new[] { "details", "summary", "kbd", "sup", "sub", "picture", "source", "input" })
        {
            sanitizer.AllowedTags.Add(tag);
        }

        sanitizer.AllowedAttributes.Add("class");
        sanitizer.AllowedAttributes.Add("id");
        sanitizer.AllowedAttributes.Add("srcset");
        sanitizer.AllowedAttributes.Add("checked");
        sanitizer.AllowedAttributes.Add("disabled");
        sanitizer.AllowedAttributes.Add("type");
        return sanitizer;
    }

    private sealed class MappedLiteralRenderer : HtmlObjectRenderer<LiteralInline>
    {
        protected override void Write(HtmlRenderer renderer, LiteralInline obj)
        {
            var span = obj.Span;
            var isSole = obj.Parent is { } parent && ReferenceEquals(parent.FirstChild, obj) && ReferenceEquals(parent.LastChild, obj);
            if (isSole || span.IsEmpty)
            {
                renderer.WriteEscape(ref obj.Content);
                return;
            }

            renderer.Write("<span data-sp=\"");
            renderer.Write(span.Start.ToString(CultureInfo.InvariantCulture));
            renderer.Write("\" data-ep=\"");
            renderer.Write((span.End + 1).ToString(CultureInfo.InvariantCulture));
            renderer.Write("\">");
            renderer.WriteEscape(ref obj.Content);
            renderer.Write("</span>");
        }
    }
}
