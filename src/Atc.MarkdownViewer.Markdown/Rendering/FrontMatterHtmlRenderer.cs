namespace Atc.MarkdownViewer.Markdown.Rendering;

/// <summary>
/// Shows YAML front matter as a collapsed metadata table (plan REQ-002). Simple <c>key: value</c> lines become
/// table rows; anything else (nested YAML, lists) is shown as preformatted text.
/// </summary>
internal sealed partial class FrontMatterHtmlRenderer : HtmlObjectRenderer<YamlFrontMatterBlock>
{
    protected override void Write(
        HtmlRenderer renderer,
        YamlFrontMatterBlock obj)
    {
        var lines = new List<string>(obj.Lines.Count);
        for (var i = 0; i < obj.Lines.Count; i++)
        {
            lines.Add(obj.Lines.Lines[i].Slice.ToString());
        }

        renderer.Write("<details class=\"mdv-frontmatter\"").WriteAttributes(obj).Write("><summary>Front matter</summary>");
        var pairs = lines.Where(l => l.Length > 0).Select(l => KeyValueLine().Match(l)).ToList();
        if (pairs.Count > 0 && pairs.TrueForAll(m => m.Success))
        {
            renderer.Write("<table><tbody>");
            foreach (var groups in pairs.Select(m => m.Groups))
            {
                renderer.Write("<tr><th>").WriteEscape(groups["key"].Value).Write("</th><td>")
                    .WriteEscape(groups["value"].Value).Write("</td></tr>");
            }

            renderer.Write("</tbody></table>");
        }
        else
        {
            renderer.Write("<pre><code>").WriteEscape(string.Join('\n', lines)).Write("</code></pre>");
        }

        renderer.WriteLine("</details>");
    }

    [GeneratedRegex(@"^(?<key>[A-Za-z0-9_.-]+):\s*(?<value>.*)$", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex KeyValueLine();
}