using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.Web.WebView2.Core;
using SpikeMarkdown;

namespace SpikeWinUI;

public sealed class MainWindow : Window
{
    private const int ChunkBlocks = 200;

    private readonly WebView2 web = new();
    private CoreWebView2Environment? environment;
    private string nonClientRegion = "not tried";

    public MainWindow(Task<CoreWebView2Environment> environmentTask)
    {
        Title = "spike-winui";
        ExtendsContentIntoTitleBar = true;
        SystemBackdrop = new MicaBackdrop();
        Content = web;
        _ = InitializeAsync(environmentTask);
    }

    private async Task InitializeAsync(Task<CoreWebView2Environment> environmentTask)
    {
        environment = await environmentTask;
        Marks.Add("envCreated");

        await web.EnsureCoreWebView2Async(environment);
        Marks.Add("coreReady");

        var core = web.CoreWebView2;
        try
        {
            core.Settings.IsNonClientRegionSupportEnabled = true;
            nonClientRegion = "enabled";
        }
        catch (Exception ex)
        {
            nonClientRegion = ex.GetType().Name;
        }

        core.SetVirtualHostNameToFolderMapping("app.mdv", Path.Combine(AppContext.BaseDirectory, "ui"), CoreWebView2HostResourceAccessKind.Allow);
        if (Options.Current.InlineFirstChunk && !Options.Current.Edit)
        {
            core.AddWebResourceRequestedFilter("https://app.mdv/index.html", CoreWebView2WebResourceContext.Document);
            core.WebResourceRequested += OnIndexRequested;
        }

        core.WebMessageReceived += OnWebMessageReceived;
        core.Navigate(Options.Current.Edit ? "https://app.mdv/edit.html" : "https://app.mdv/index.html");
        Marks.Add("navigate");
    }

    /// <summary>--inline-first: answer the page request with the first chunk already in the document,
    /// so content paints with the page instead of after a ready/open message round trip.</summary>
    private async void OnIndexRequested(CoreWebView2 sender, CoreWebView2WebResourceRequestedEventArgs args)
    {
        var deferral = args.GetDeferral();
        try
        {
            var (_, output) = await App.Document!;
            Marks.Add("indexRequested");
            var first = string.Concat(output.Blocks.Take(ChunkBlocks).Select(b => b.Html));
            var template = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "ui", "index.html"));
            var page = template.Replace("<article id=\"doc\"></article>", "<article id=\"doc\" data-inline=\"1\">" + first + "</article>", StringComparison.Ordinal);
            var stream = new MemoryStream(Encoding.UTF8.GetBytes(page)).AsRandomAccessStream();
            args.Response = environment!.CreateWebResourceResponse(stream, 200, "OK", "Content-Type: text/html; charset=utf-8");
        }
        finally
        {
            deferral.Complete();
        }
    }

    private async void OnWebMessageReceived(CoreWebView2 sender, CoreWebView2WebMessageReceivedEventArgs args)
    {
        using var json = JsonDocument.Parse(args.WebMessageAsJson);
        var root = json.RootElement;
        switch (root.GetProperty("type").GetString())
        {
            case "ready":
                Marks.Add("jsReady");
                if (App.Document is null)
                {
                    return;
                }

                var (text, output) = await App.Document;
                var chunks = output.Blocks.Chunk(ChunkBlocks).Select(c => string.Concat(c.Select(b => b.Html))).ToList();
                if (root.TryGetProperty("inline", out var inline) && inline.GetBoolean())
                {
                    foreach (var chunk in chunks.Skip(1))
                    {
                        sender.PostWebMessageAsJson(JsonSerializer.Serialize(new ChunkMessage("chunk", chunk), SpikeJson.Default.ChunkMessage));
                    }

                    return;
                }

                sender.PostWebMessageAsJson(JsonSerializer.Serialize(
                    new OpenMessage("open", chunks.Count > 0 ? chunks[0] : string.Empty, Options.Current.Edit ? text : string.Empty),
                    SpikeJson.Default.OpenMessage));
                Marks.Add("openPosted");
                if (Options.Current.Edit)
                {
                    return;
                }

                foreach (var chunk in chunks.Skip(1))
                {
                    sender.PostWebMessageAsJson(JsonSerializer.Serialize(new ChunkMessage("chunk", chunk), SpikeJson.Default.ChunkMessage));
                }

                break;

            case "fcp":
                Marks.Add("fcp");
                if (Options.Current.Bench)
                {
                    var (_, rendered) = await App.Document!;
                    WriteBench(
                        ("MDV_STARTUP_MS", Marks.Get("fcp")),
                        ("PARSE_MS", rendered.ParseMs),
                        ("RENDER_MS", rendered.RenderMs),
                        ("INSERT_MS", root.GetProperty("insertMs").GetDouble()));
                    Environment.Exit(0);
                }

                break;

            case "render":
                var version = root.GetProperty("version").GetInt32();
                var source = root.GetProperty("text").GetString() ?? string.Empty;
                var result = await Task.Run(() => SpikeRenderer.Render(source));
                var html = string.Concat(result.Blocks.Select(b => b.Html));
                sender.PostWebMessageAsJson(JsonSerializer.Serialize(new RenderedMessage("rendered", version, html), SpikeJson.Default.RenderedMessage));
                break;

            case "editStats":
                WriteBench(
                    ("EDIT_MEDIAN_MS", root.GetProperty("median").GetDouble()),
                    ("EDIT_P90_MS", root.GetProperty("p90").GetDouble()),
                    ("EDIT_MIN_MS", root.GetProperty("min").GetDouble()),
                    ("EDIT_MAX_MS", root.GetProperty("max").GetDouble()));
                Environment.Exit(0);
                break;
        }
    }

    private void WriteBench(params (string Key, double Value)[] values)
    {
        if (Options.Current.BenchOut is not { } path)
        {
            return;
        }

        var sb = new StringBuilder();
        foreach (var (key, value) in values)
        {
            sb.Append(key).Append('=').AppendLine(value.ToString("F1", CultureInfo.InvariantCulture));
        }

        var memory = MemoryBreakdown();
        sb.Append("PRIVATE_MB=").AppendLine(memory.Values.Sum().ToString("F1", CultureInfo.InvariantCulture));
        foreach (var (kind, mb) in memory)
        {
            sb.Append("MEM_").Append(kind).Append("_MB=").AppendLine(mb.ToString("F1", CultureInfo.InvariantCulture));
        }

        sb.Append("NONCLIENT_REGION=").AppendLine(nonClientRegion);
        foreach (var (name, ms) in Marks.All)
        {
            sb.Append("MARK_").Append(name).Append('=').AppendLine(ms.ToString("F1", CultureInfo.InvariantCulture));
        }

        File.WriteAllText(path, sb.ToString());
    }

    /// <summary>Private memory in MB: the host process, then every WebView2 process summed per kind
    /// (Browser, Renderer, Gpu, Utility, ...).</summary>
    private SortedDictionary<string, double> MemoryBreakdown()
    {
        const double Mb = 1024.0 * 1024.0;
        var result = new SortedDictionary<string, double>(StringComparer.Ordinal)
        {
            ["Host"] = Process.GetCurrentProcess().PrivateMemorySize64 / Mb,
        };

        if (environment is not null)
        {
            foreach (var info in environment.GetProcessInfos())
            {
                try
                {
                    var kind = "WebView2" + info.Kind;
                    result[kind] = result.GetValueOrDefault(kind) + (Process.GetProcessById(info.ProcessId).PrivateMemorySize64 / Mb);
                }
                catch (ArgumentException)
                {
                    // The process exited between listing and reading.
                }
            }
        }

        return result;
    }
}
