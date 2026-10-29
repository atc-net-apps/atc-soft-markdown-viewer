using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json.Serialization;

namespace SpikeWinUI;

internal sealed record OpenMessage(string Type, string Html, string Text);

internal sealed record ChunkMessage(string Type, string Html);

internal sealed record RenderedMessage(string Type, int Version, string Html);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(OpenMessage))]
[JsonSerializable(typeof(ChunkMessage))]
[JsonSerializable(typeof(RenderedMessage))]
internal sealed partial class SpikeJson : JsonSerializerContext;

/// <summary>Timestamps in milliseconds since the OS started the process.</summary>
internal static class Marks
{
    private static readonly DateTime ProcessStart = Process.GetCurrentProcess().StartTime;
    private static readonly ConcurrentQueue<(string Name, double Ms)> Items = new();

    public static IEnumerable<(string Name, double Ms)> All => Items;

    public static void Add(string name) => Items.Enqueue((name, (DateTime.Now - ProcessStart).TotalMilliseconds));

    public static double Get(string name) => Items.Last(i => i.Name == name).Ms;
}

/// <param name="EarlyEnvironment">--early-env: create the WebView2 environment in Main, before XAML starts.</param>
/// <param name="InlineFirstChunk">--inline-first: serve index.html with the first chunk already inside it.</param>
internal sealed record Options(string? File, bool Bench, string? BenchOut, bool Edit, bool EarlyEnvironment, bool InlineFirstChunk)
{
    public static Options Current { get; } = Parse(Environment.GetCommandLineArgs().Skip(1).ToArray());

    private static Options Parse(string[] args)
    {
        string? file = null;
        string? benchOut = null;
        bool bench = false, edit = false, earlyEnvironment = false, inlineFirstChunk = false;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--bench": bench = true; break;
                case "--edit": edit = true; break;
                case "--early-env": earlyEnvironment = true; break;
                case "--inline-first": inlineFirstChunk = true; break;
                case "--bench-out" when i + 1 < args.Length: benchOut = args[++i]; break;
                default: file ??= Path.GetFullPath(args[i]); break;
            }
        }

        return new Options(file, bench, benchOut, edit, earlyEnvironment, inlineFirstChunk);
    }
}
