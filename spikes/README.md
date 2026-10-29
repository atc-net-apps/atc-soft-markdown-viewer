# Spikes

Throwaway measurement code for plan Phase 2 (WinUI 3 host variants, Markdig speed, sanitizer cost, edit round trip). Nothing here ships. Results go into `docs/adr/0001-native-window.md`.

| Folder | What it is |
|--------|------------|
| `SpikeMarkdown/` | Prototype of the source-mapped Markdig renderer (`data-sp`/`data-ep` on every element) |
| `SpikeBench/` | BenchmarkDotNet benchmarks for parse, render and sanitize, plus a `--check` source-map sanity check |
| `web/` | The web pages loaded in WebView2: `index.html` (viewer, reports first paint) and `edit.html` (edit round trip) |
| `SpikeWinUI/` | WinUI 3 host: Mica window, WebView2, background render, `--bench` instrumentation |
| `publish.ps1` | Publishes the host variants into `SpikeWinUI/bin/publish/<variant>` |

All commands run from the repository root in PowerShell.

> When running them through Claude Code's `!` prefix (which uses bash), use forward slashes and absolute paths, and add `-NoProfile` so your PowerShell profile doesn't run, for example `! pwsh -NoProfile -File E:/Dev/Code/atc-net-apps/atc-soft-markdown-viewer/spikes/publish.ps1 -Variant w1a`.

## Fixtures

```powershell
node benchmarks/generate-fixtures.mjs
```

Writes `benchmarks/fixtures/readme-500.md`, `readme-5k.md`, `readme-5k-rich.md` and `readme-5k-crlf.md` deterministically.

## Markdig benchmarks (BenchmarkDotNet)

```powershell
dotnet run -c Release --project spikes/SpikeBench -- --filter *
dotnet run -c Release --project spikes/SpikeBench -- --check benchmarks/fixtures/readme-5k.md
```

## Host variants

```powershell
pwsh -NoProfile -File spikes/publish.ps1                     # all variants
pwsh -NoProfile -File spikes/publish.ps1 -Variant w1a,w1b    # some variants
```

| Variant | Meaning |
|---------|---------|
| `w1a` | Windows App SDK bundled (self-contained), ReadyToRun |
| `w1b` | Shared Windows App Runtime 2.5 (must be installed), ReadyToRun |
| `w1a-aot` | `w1a` with Native AOT (attempt; may fail) |

## Measure startup and edit round trip

```powershell
pwsh benchmarks/measure.ps1 -Exe spikes/SpikeWinUI/bin/publish/w1a/spike-winui.exe -Fixture benchmarks/fixtures/readme-5k.md -Label w1a -OutDir docs/adr/0001-native-window-results
pwsh benchmarks/measure.ps1 -Exe spikes/SpikeWinUI/bin/publish/w1a/spike-winui.exe -Fixture benchmarks/fixtures/readme-5k.md -Label w1a-edit-5k -Edit -Runs 3 -OutDir docs/adr/0001-native-window-results
pwsh benchmarks/measure.ps1 -Exe spikes/SpikeWinUI/bin/publish/w1a/spike-winui.exe -Fixture benchmarks/fixtures/readme-5k.md -Label w1a-tuned -ExtraArgs '--early-env','--inline-first' -OutDir docs/adr/0001-native-window-results
```

Startup tweaks (host flags, combinable):

| Flag | Effect |
|------|--------|
| `--early-env` | Create the WebView2 environment in `Main`, before XAML starts |
| `--inline-first` | Serve `index.html` with the first 200 blocks already inside it, skipping the ready/open message round trip |

## Try it by hand

```powershell
spikes/SpikeWinUI/bin/publish/w1a/spike-winui.exe benchmarks/fixtures/readme-5k.md
```

The grey bar at the top uses CSS `app-region: drag`. If the window can be dragged by it, tabs can live in the title bar.
