namespace Atc.MarkdownViewer.Benchmarks;

/// <summary>
/// Markdown core timings for the fixtures in <c>benchmarks/fixtures</c> (plan PERF-005, PERF-007).
/// </summary>
[MemoryDiagnoser]
public class MarkdownRenderBenchmarks
{
    private readonly MarkdownRenderService service = new();
    private string source = string.Empty;

    [Params("readme-500.md", "readme-5k.md", "readme-5k-rich.md", "readme-5k-crlf.md")]
    public string Fixture { get; set; } = string.Empty;

    [GlobalSetup]
    public void Setup()
        => source = File.ReadAllText(Path.Combine(FixturesDirectory(), Fixture));

    [Benchmark(Description = "Parse")]
    public MarkdownDocument Parse()
        => Markdig.Markdown.Parse(source, MarkdownPipelineFactory.Default);

    [Benchmark(Description = "Render")]
    public RenderResult Render()
        => service.Render(source, RenderContext.ForText());

    /// <summary>
    /// Walks up from the build output to the folder that contains <c>benchmarks/fixtures</c>.
    /// </summary>
    private static string FixturesDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "benchmarks", "fixtures")))
        {
            directory = directory.Parent;
        }

        return directory is null
            ? throw new DirectoryNotFoundException("benchmarks/fixtures was not found above " + AppContext.BaseDirectory)
            : Path.Combine(directory.FullName, "benchmarks", "fixtures");
    }
}