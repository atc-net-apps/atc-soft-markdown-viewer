using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using SpikeMarkdown;

if (args is ["--check", var file])
{
    SourceMapCheck.Run(file);
    return;
}

BenchmarkSwitcher.FromAssembly(typeof(MarkdownBenchmarks).Assembly).Run(args);

/// <summary>Quick round-trip check of the prototype mapping: an element whose only content is plain
/// text must have source[sp..ep] equal to that text (after HTML-unescaping).</summary>
internal static class SourceMapCheck
{
    public static void Run(string file)
    {
        var source = File.ReadAllText(file);
        var output = SpikeRenderer.Render(source);
        var html = string.Concat(output.Blocks.Select(b => b.Html));
        var regex = new System.Text.RegularExpressions.Regex(
            "<(span|code|em|strong|del) data-sp=\"(\\d+)\" data-ep=\"(\\d+)\">([^<]*)</\\1>");
        int ok = 0, bad = 0;
        foreach (System.Text.RegularExpressions.Match m in regex.Matches(html))
        {
            var sp = int.Parse(m.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
            var ep = int.Parse(m.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture);
            var text = System.Net.WebUtility.HtmlDecode(m.Groups[4].Value);
            var slice = source[sp..ep];
            var tag = m.Groups[1].Value;
            // Plain spans must match exactly; for em/strong/del/code the slice includes the markers.
            var match = tag == "span" ? slice == text : slice.Contains(text, StringComparison.Ordinal);
            if (match) { ok++; } else if (bad++ < 5) { Console.WriteLine($"MISMATCH <{tag}> [{sp}..{ep}] text='{text}' slice='{slice}'"); }
        }

        Console.WriteLine($"{Path.GetFileName(file)}: blocks={output.Blocks.Count} html={html.Length} chars parse={output.ParseMs:F1}ms render={output.RenderMs:F1}ms (cold)");
        Console.WriteLine($"mapped runs checked: ok={ok} mismatch={bad}");
        Console.WriteLine("sample: " + html[..Math.Min(600, html.Length)]);
    }
}

[MemoryDiagnoser]
[Config(typeof(Config))]
public class MarkdownBenchmarks
{
    private string source = string.Empty;
    private string html = string.Empty;

    [Params("readme-500.md", "readme-5k.md", "readme-5k-rich.md", "readme-5k-crlf.md")]
    public string Fixture { get; set; } = string.Empty;

    [GlobalSetup]
    public void Setup()
    {
        source = File.ReadAllText(Path.Combine(FindFixtures(), Fixture));
        html = string.Concat(SpikeRenderer.Render(source).Blocks.Select(b => b.Html));
    }

    [Benchmark(Description = "Parse")]
    public object Parse() => SpikeRenderer.Parse(source);

    [Benchmark(Description = "Markdig default ToHtml", Baseline = true)]
    public string ToHtmlDefault() => SpikeRenderer.ToHtmlDefault(source);

    [Benchmark(Description = "Parse + source-mapped render")]
    public object RenderSourceMapped() => SpikeRenderer.Render(source);

    [Benchmark(Description = "Sanitize assembled HTML")]
    public string Sanitize() => SpikeRenderer.Sanitize(html);

    private static string FindFixtures()
    {
        // Walk up from the build output to the repository root.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "benchmarks", "fixtures")))
        {
            dir = dir.Parent;
        }

        return dir is null
            ? throw new DirectoryNotFoundException("benchmarks/fixtures not found above " + AppContext.BaseDirectory)
            : Path.Combine(dir.FullName, "benchmarks", "fixtures");
    }

    private sealed class Config : ManualConfig
    {
        public Config()
        {
            AddJob(Job.Default.WithWarmupCount(3).WithIterationCount(10));
        }
    }
}
