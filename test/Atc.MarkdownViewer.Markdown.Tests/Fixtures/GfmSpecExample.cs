namespace Atc.MarkdownViewer.Markdown.Tests.Fixtures;

/// <summary>
/// One example from the GitHub Flavored Markdown spec (<c>Fixtures/spec/gfm-spec.txt</c>, from
/// github/cmark-gfm, licensed CC-BY-SA 4.0).
/// </summary>
/// <param name="Number">The example number in the spec.</param>
/// <param name="Section">The spec section the example belongs to.</param>
/// <param name="Markdown">The example's markdown, with the spec's tab marker replaced by tabs.</param>
/// <param name="ExpectedHtml">The HTML that GitHub's renderer (cmark-gfm) produces, per the spec.</param>
public sealed record GfmSpecExample(
    int Number,
    string Section,
    string Markdown,
    string ExpectedHtml)
{
    private static readonly string Fence = new('`', 32);

    public static IReadOnlyList<GfmSpecExample> Load()
    {
        var examples = new List<GfmSpecExample>();
        var section = string.Empty;
        var inExample = false;
        var inMarkdown = false;
        var markdown = new StringBuilder();
        var html = new StringBuilder();
        foreach (var line in File.ReadAllLines(FixtureFiles.SpecFile))
        {
            if (!inExample && line.StartsWith('#'))
            {
                section = line.TrimStart('#').Trim();
            }
            else if (!inExample && line.StartsWith(Fence + " example", StringComparison.Ordinal))
            {
                inExample = true;
                inMarkdown = true;
                markdown.Clear();
                html.Clear();
            }
            else if (inExample && line.StartsWith(Fence, StringComparison.Ordinal))
            {
                inExample = false;
                examples.Add(new GfmSpecExample(
                    examples.Count + 1,
                    section,
                    markdown.ToString().Replace('→', '\t'),
                    html.ToString().Replace('→', '\t')));
            }
            else if (inMarkdown && line == ".")
            {
                inMarkdown = false;
            }
            else if (inExample)
            {
                (inMarkdown ? markdown : html).Append(line).Append('\n');
            }
        }

        return examples;
    }

    public override string ToString()
        => $"#{Number.ToString(CultureInfo.InvariantCulture)} {Section}";
}