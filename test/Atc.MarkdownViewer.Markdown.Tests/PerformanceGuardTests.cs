namespace Atc.MarkdownViewer.Markdown.Tests;

/// <summary>
/// PERF-005 guard: parse plus source-mapped render of a 5,000-line document within 30 ms (median, warm).
/// Runs only with <c>MDV_PERF=1</c>, in Release, because timings on shared CI runners are noisy.
/// </summary>
public sealed class PerformanceGuardTests
{
    private const int Lines = 5000;
    private const double BudgetMilliseconds = 30;

    [Fact]
    [Trait("Category", "Perf")]
    public void Render5kLines_IsWithinBudget()
    {
        Assert.SkipUnless(Environment.GetEnvironmentVariable("MDV_PERF") == "1", "Set MDV_PERF=1 to run performance guards.");

        // Arrange
        var service = new MarkdownRenderService();
        var source = BuildDocument();
        for (var i = 0; i < 5; i++)
        {
            service.Render(source, RenderContext.ForText());
        }

        // Act
        var timings = new List<double>();
        for (var i = 0; i < 15; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            service.Render(source, RenderContext.ForText());
            timings.Add(stopwatch.Elapsed.TotalMilliseconds);
        }

        // Assert
        timings.Sort();
        timings[timings.Count / 2].Should().BeLessThanOrEqualTo(BudgetMilliseconds);
    }

    /// <summary>
    /// Repeats the GFM fixtures (without raw HTML, as in readme-5k.md) until the document has 5,000 lines.
    /// </summary>
    private static string BuildDocument()
    {
        var parts = FixtureFiles.GfmNames
            .Where(n => n is not "raw-html" and not "front-matter")
            .Select(FixtureFiles.ReadGfm)
            .ToList();
        var sb = new StringBuilder();
        var lines = 0;
        var index = 0;
        while (lines < Lines)
        {
            var part = parts[index++ % parts.Count];
            sb.Append(part).Append('\n');
            lines += part.Count(c => c == '\n') + 1;
        }

        return sb.ToString();
    }
}