namespace Atc.MarkdownViewer.Markdown;

/// <summary>
/// Builds the Markdig pipeline used for every render (plan ARC-003).
/// </summary>
public static class MarkdownPipelineFactory
{
    /// <summary>
    /// Gets the shared pipeline. Markdig pipelines are thread-safe once built.
    /// </summary>
    public static MarkdownPipeline Default { get; } = Create();

    /// <summary>
    /// Creates a new pipeline with GitHub Flavored Markdown extensions and precise source locations.
    /// </summary>
    /// <returns>The built pipeline.</returns>
    public static MarkdownPipeline Create()
        => new MarkdownPipelineBuilder()
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
}