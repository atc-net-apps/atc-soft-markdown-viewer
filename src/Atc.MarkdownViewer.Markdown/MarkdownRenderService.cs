namespace Atc.MarkdownViewer.Markdown;

/// <summary>
/// Parses markdown with Markdig and renders source-mapped HTML split into render units (plan MD-001 to MD-008).
/// </summary>
public sealed class MarkdownRenderService : IMarkdownRenderService
{
    private readonly MarkdownPipeline pipeline;
    private readonly HtmlSanitizer sanitizer = MarkdownHtmlSanitizer.Create();

    public MarkdownRenderService()
        : this(MarkdownPipelineFactory.Default)
    {
    }

    public MarkdownRenderService(MarkdownPipeline pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        this.pipeline = pipeline;
    }

    /// <inheritdoc />
    public RenderResult Render(
        string source,
        RenderContext context)
        => Render(source, context, RenderOptions.Default);

    internal RenderResult Render(
        string source,
        RenderContext context,
        RenderOptions options)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(options);

        var document = Markdig.Markdown.Parse(source, pipeline);
        var facts = DocumentFacts.Collect(document);
        if (options.RewriteLinks)
        {
            LinkRewriter.Rewrite(document, context);
        }

        SourceMapAnnotator.Annotate(document, source);

        using var writer = new StringWriter(new StringBuilder(source.Length * 2), CultureInfo.InvariantCulture);
        var renderer = SourceMappedHtmlRendererFactory.Create(writer, pipeline);
        var builder = writer.GetStringBuilder();
        var units = RenderUnitBuilder.Build(document);
        var blocks = new List<RenderedBlock>(units.Count);
        foreach (var unit in units)
        {
            builder.Clear();
            foreach (var block in unit)
            {
                renderer.Render(block);
            }

            writer.Flush();
            var html = builder.ToString();
            if (options.Sanitize && facts.HasRawHtml && ContainsRawHtml(unit))
            {
                html = sanitizer.Sanitize(html);
            }

            var start = Math.Max(0, unit[0].Span.Start);
            var end = Math.Max(start, unit[^1].Span.End + 1);
            blocks.Add(new RenderedBlock(Hash(source, start, end), html, start, end));
        }

        return new RenderResult(source, document, blocks, facts.Outline, facts.WordCount, facts.HasRawHtml, facts.HasMermaid, facts.HasMath);
    }

    private static bool ContainsRawHtml(List<Block> unit)
        => unit.Exists(ContainsRawHtml);

    /// <summary>
    /// Markdig's <c>Descendants()</c> on a leaf block does not visit its inlines, so leaf blocks are checked
    /// through their inline container.
    /// </summary>
    private static bool ContainsRawHtml(Block block)
        => block switch
        {
            HtmlBlock => true,
            LeafBlock { Inline: { } inline } => inline.Descendants<HtmlInline>().Any(),
            ContainerBlock container => container.Descendants().Any(node => node is HtmlBlock or HtmlInline),
            _ => false,
        };

    private static string Hash(
        string source,
        int start,
        int end)
    {
        var length = Math.Min(end, source.Length) - start;
        var bytes = MemoryMarshal.AsBytes(source.AsSpan(start, Math.Max(0, length)));
        return XxHash3.HashToUInt64(bytes).ToString("x16", CultureInfo.InvariantCulture);
    }
}