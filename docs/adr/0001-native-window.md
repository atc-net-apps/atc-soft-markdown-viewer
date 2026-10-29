# ADR 0001: Native window, startup budget and Markdig core measurements

- Status: Accepted (2026-10-24). D2 to D4 follow the owner's direction ("standalone", "sure about the memory", "keep core in C#"). D1 follows the owner's earlier choice of WinUI 3, with the target set from measurement.
- Date: 2026-10-24
- Plan: `plans/feature-markdown-viewer-1.md`, Phase 2 (DEC-001, PERF-001 to PERF-005, SEC-001)
- Raw results: [`0001-native-window-results/`](0001-native-window-results/)

## Context

The plan picks a WinUI 3 window hosting WebView2, with Markdig rendering HTML in C# (DEC-001, DEC-002). Phase 2 measures whether that stack meets the startup, edit and memory budgets before any real code is written. It also checks the Markdig source-mapping approach and the cost of HTML sanitizing.

## Setup

- Machine: AMD Ryzen 9 9950X3D, 64 GB RAM, Windows 11 25H2 (10.0.26200), WebView2 Runtime 155, fresh boot, idle (3% CPU).
- Spikes: `spikes/` (see `spikes/README.md`). A WinUI 3 window (Windows App SDK 2.5.1, unpackaged, Mica, `ExtendsContentIntoTitleBar`) hosts WebView2. A background task reads and renders the file with Markdig and posts the first 200 blocks as soon as the page is ready.
- Startup (`benchmarks/measure.ps1`): the median of 15 launches after 1 warmup launch, measured from process start to first content paint (two `requestAnimationFrame` callbacks after inserting the HTML).
- Markdig timings: BenchmarkDotNet, warm, mean of 10 iterations.
- Fixtures: `benchmarks/fixtures/` (deterministic, from `benchmarks/generate-fixtures.mjs`).

## Results

### Startup (5,000-line fixture unless noted)

| Variant | Median | p90 | First launch after boot | Install size |
|---------|-------:|----:|------------------------:|-------------:|
| W1a: Windows App SDK bundled | 469 ms | 476 ms | 1,267 ms | 278 MB |
| W1b: shared Windows App Runtime 2.5 | 468 ms | 472 ms | 979 ms | 203 MB |
| W1a + `--early-env` (WebView2 environment created in `Main`) | 477 ms | 484 ms | – | – |
| W1a + `--inline-first` (first chunk served inside `index.html`) | 469 ms | 480 ms | – | – |
| W1a + both | 480 ms | 489 ms | – | – |
| W1a, 500-line fixture | 460 ms | 467 ms | – | – |

Median marks for W1a, in milliseconds after process start:

| Step | ms | Δ |
|------|---:|--:|
| `Main` entered (.NET and Windows App SDK bootstrap) | 38 | 38 |
| `OnLaunched` (XAML application up) | 75 | 37 |
| WebView2 environment created | 203 | 128 |
| Window activated | 220 | 17 |
| **WebView2 controller ready** | **357** | **137** |
| Page script ready | 398 | 41 |
| First chunk posted | 410 | 12 |
| **First content paint** | **469** | **59** |
| Markdig read, parse and render done (off the critical path) | 157 | – |

Findings:

- **WebView2 is about 80% of the critical path.** Environment, controller, page load and first paint together take about 370 of 469 ms. WinUI and .NET take about 75 ms. Markdig finishes at 157 ms, long before the page is ready, so it is never what the user waits for.
- **Document size barely matters.** 500 lines takes 460 ms and 5,000 lines takes 469 ms.
- **Neither tweak helps.** WebView2 does not create the environment faster when started earlier, and inlining the first chunk saves nothing measurable. Both are dropped.
- **Bundled versus shared runtime makes no difference to warm startup.** The shared runtime is 75 MB smaller, but needs the runtime installed. A portable zip would then not be self-contained, and winget must declare the dependency.
- **The first launch after boot is 1.0–1.3 s.** That is one sample each, with cold disk caches.

### Markdig core (BenchmarkDotNet, warm)

| 5,000-line fixture | Mean | Allocated |
|--------------------|-----:|----------:|
| Parse | 2.4 ms | 3.6 MB |
| Markdig default `ToHtml` | 4.8 ms | 4.7 MB |
| **Parse + source-mapped render** (`data-sp`/`data-ep` on every element) | **6.9 ms** | 9.7 MB |
| Sanitize the assembled HTML (HtmlSanitizer) | 34–49 ms | 41 MB |

- Source mapping costs about 1.4× Markdig's default renderer. PERF-005's 30 ms budget is met with a lot of room.
- A source-map check (`SpikeBench --check`) compared about 2,750 mapped text runs per 5,000-line fixture against `source[sp..ep]` and found **0 mismatches**, including the CRLF and tab fixture.
- The first parse in a fresh process costs about 80 ms, even with ReadyToRun, because of JIT. It is off the startup critical path, as the marks above show.

### Edit round trip (edit → host render → full preview replacement → paint, no debounce)

| Fixture | Median | p90 | Budget (PERF-003) |
|---------|-------:|----:|------------------:|
| 500 lines | 8.4 ms | 8.7 ms | 16 ms ✅ |
| 5,000 lines | 75.5 ms | 77 ms | 50 ms ❌ |

This is the worst case: the whole document is re-rendered and the whole preview (about 800 KB of HTML) is replaced. The 5,000-line miss is dominated by the DOM replacement, which is what block patching (ARC-007, `data-h`) avoids.

### Memory (W1a, private bytes at first paint)

| Process | MB |
|---------|---:|
| Host (.NET, WinUI, Markdig, HTML strings) | 127 |
| WebView2 GPU | 84 |
| WebView2 renderer | 52 |
| WebView2 browser | 34 |
| WebView2 utility | 20 |
| **Total** | **315** |

WebView2 alone uses about 190 MB, so PERF-004's 200 MB total cannot be met by any WebView2-based design.

### Other checks

- **Tabs in the title bar.** `CoreWebView2Settings.IsNonClientRegionSupportEnabled` plus a CSS `app-region: drag` bar lets the window be dragged by web content. The owner confirmed it by hand on 2026-10-24.
- **Microsoft.UI.Reactor** (C#-first MVU UI for WinUI, experimental 0.1.0-preview). Not relevant now: the native UI is a single window hosting WebView2, already written in C# with no XAML views.
- **Native AOT.** The build fails with "Platform linker not found", because Native AOT needs Visual Studio's C++ workload. Not pursued: startup is dominated by WebView2, not .NET.
- **Sanitizing and chunking conflict.** Raw HTML such as `<details>` … `</details>` can wrap several top-level markdown blocks. Sanitizing per block would break it. The renderer must treat such a span as one render unit, with one chunk and one `data-h`, and sanitize that unit (Phase 3 follow-up).
- **PERF-003 conflicts with EDT-005.** A 30 ms debounce plus a 16 ms budget cannot both hold. PERF-003 should be measured from the end of the debounce.

## Decisions

- **D1 – Window.** Keep WinUI 3 and do not build the WPF comparison (TASK-014). WPF would host the same WebView2, which is 80% of the time, so it cannot get close to 250 ms. Set **PERF-001 to 520 ms** (the measured 469 ms median plus 10%), and rely on resident mode (PERF-002, ≤ 50 ms) for an instant second open. For comparison, VS Code's markdown preview takes several seconds.
- **D2 – Deployment variant.** Use **W1a (bundled Windows App SDK)**: the owner wants the app standalone, whatever it costs. Startup is the same, there is no Windows App Runtime dependency, and the winget manifest stays simple. The cost is 75 MB more disk. Whether the WebView2 runtime is bundled too is open (see "Open question").
- **D3 – Memory budget.** Replace PERF-004 with **total ≤ 350 MB** and **host ≤ 100 MB** after opening `readme-5k.md`. The host target means trimming about 27 MB, for example by not keeping the full rendered HTML string after it has been posted.
- **D4 – Sanitizer.** Keep sanitizing in **C# (HtmlSanitizer)**, and run it only for documents that contain raw HTML (`HasRawHtml`) and only on the render units that contain it. That is off the critical path at startup. In the edit loop it costs at most about 49 ms for a 5,000-line, HTML-heavy document, and less with per-unit sanitizing. The CSP (SEC-002) blocks scripts, event handlers and `javascript:` URLs in any case. This departs from SEC-001's "move to DOMPurify above 15 ms" rule, and keeps the core in C# as the owner asked.

## Open question

Should "standalone" include WebView2? Today the app uses the Evergreen WebView2 runtime. It ships with Windows 11, is updated by Windows, and is installed by our installer when missing, but a portable zip cannot install it. Microsoft also allows bundling a Fixed Version runtime inside the app. That adds roughly 150–250 MB and makes the app fully self-contained, but every browser security fix then requires an atc-mdv release.

## Consequences

- PERF-001 → 520 ms, PERF-003 measured after the debounce, PERF-004 → total ≤ 350 MB and host ≤ 100 MB, SEC-001 updated per D4.
- TASK-014 (WPF spike) is not needed. TASK-079 (change sets) waits on block patching: patching comes first, and change sets only if 5,000-line edits still exceed 50 ms.
- The startup tweaks (`--early-env`, `--inline-first`) are not carried into the app.
- `spikes/` stays as it is, for re-measuring. The permanent benchmarks (PERF-007) arrive in Phase 3.
