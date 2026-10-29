---
goal: Build ATC Markdown Viewer (atc-mdv), a fast, stable Windows markdown viewer and editor with a C# core (.NET 10, Markdig, WebView2), released with release-please and installed with winget
version: 2.0
date_created: 2026-10-23
last_updated: 2026-10-23
owner: Per Kops (atc-net-apps)
status: 'In progress'
tags: [feature, architecture, desktop, markdown, dotnet, release-please, winget]
---

# Introduction

![Status: In progress](https://img.shields.io/badge/status-In%20progress-yellow)

ATC Markdown Viewer (`atc-mdv`) is a Windows desktop app that:

- opens markdown files before the user finishes double-clicking,
- renders them exactly like GitHub does,
- follows the file live as it changes on disk,
- includes an editor whose source and preview stay locked together.

Selection, copy, scroll and cursor position map exactly between the two panes. This fixes the main annoyance with the VS Code markdown preview: copying from the preview gives the wrong text, and finding the same passage in the editor means scrolling by hand.

The core is C#. .NET 10 reads files, parses markdown with Markdig, renders HTML with exact source offsets, and owns all logic that does not need a browser. WebView2 displays the result. A thin TypeScript layer handles only what has to run in the browser: putting HTML on screen, DOM selection and scrolling, the CodeMirror editor, and the lazily loaded Mermaid and KaTeX libraries. The native window is WinUI 3 (DEC-001).

Every feature is in v1, but each is delivered as its own task, PR and test, so every merged PR leaves a working app and release-please produces a readable changelog. One-time manual setup (GitHub org, tokens, winget, code signing) lives in [process-manual-setup-1.md](process-manual-setup-1.md).

## For Future Agents

As work proceeds: mark each task completed with its date; set the front matter `status` and each phase's `Status`; run the phase's **Verification** and record the result before starting the next phase; write the **Phase Summary** when a phase completes. When all phases are done, fill in **Final Recap** and **Deployment Plan**.

Working agreement with the owner:
- After each task or phase, report what was built and why, and say whether it is a logical commit point. If it is, suggest a conventional commit message (REL-001) and ask for GO. Commit only after GO for that specific commit, and never push.
- Never commit `plans/`, `spikes/`, `benchmarks/` or `docs/`, and do not add them to `.gitignore`. They stay local until the project is finished.
- The code is local only for now. The steps in `plans/process-manual-setup-1.md` (GitHub org, tokens, winget) are done later by the owner and do not block any task.

## 1. Requirements & Constraints

### 1.1 Product requirements

- **REQ-001**: The app opens a `.md` or `.markdown` file passed on the command line, by double-click (file association), or via "Open with", and shows rendered content.
- **REQ-002**: Rendering matches GitHub Flavored Markdown (GFM, the markdown dialect github.com renders; spec at https://github.github.com/gfm/). That covers:
  - headings, emphasis, strikethrough, links, autolinks and images,
  - tables, task lists and footnotes,
  - fenced code with syntax highlighting,
  - GitHub alerts (`> [!NOTE]`, `[!TIP]`, `[!IMPORTANT]`, `[!WARNING]`, `[!CAUTION]`),
  - emoji shortcodes,
  - YAML front matter (shown as a collapsible metadata table),
  - heading anchors in GitHub's format,
  - sanitized raw HTML.
- **REQ-003**: Live reload. When the open file changes on disk and the buffer has no unsaved edits, the view re-renders within 150 ms of the change event. It keeps the reading position, anchored on the top visible block rather than a pixel offset.
- **REQ-004**: Relative links and images resolve against the document's folder. Clicking a link to another `.md` file opens it in the same window. Alt+Left and Alt+Right go back and forward. Clicking an external link opens the default browser.
- **REQ-005**: A table of contents sidebar shows the headings (H1 to H6) as a collapsible tree, nested by level. It highlights the current section while scrolling, keeps it in view, and jumps to a heading on click. Ctrl+Shift+O toggles it, and the setting `tocDepth` (default 4) limits how deep the tree goes.
- **REQ-006**: Find in document with Ctrl+F. All matches are highlighted, and Enter and Shift+Enter step through them.
- **REQ-007**: Folder mode.
  - `atc-mdv <folder>` shows a tree of the folder's markdown files, recursively.
  - It skips `.git`, `node_modules`, `bin` and `obj`.
  - Ctrl+P opens a fuzzy file picker.
- **REQ-008**: A command palette on Ctrl+K lists every command and its shortcut.
- **REQ-009**: The theme follows the system light or dark setting, with an override in settings. Reading width is configurable and defaults to 860 px.
- **REQ-010**: Code blocks have a copy button that copies the code without the fence lines.
- **REQ-011**: Copy as rich text puts HTML and plain text on the clipboard. Pasting into Outlook, Teams or Word keeps headings, lists, tables and code formatting.
- **REQ-012**: The CLI is `atc-mdv [path or -] [--line N] [--edit] [--new-window] [--quit] [--version] [--bench] [--bench-out <path>]`.
  - `-` reads markdown from stdin.
  - `--line N` scrolls to source line N, and in edit mode puts the cursor there.
- **REQ-013**: One window has three modes: View, Split (source on the left, preview on the right) and Edit. Ctrl+E cycles through them. Ctrl+1, Ctrl+2 and Ctrl+3 select one directly.
- **REQ-014**: Mermaid diagrams and KaTeX math render. Their libraries load only when the document contains a `mermaid` fence or math.
- **REQ-015**: Export to standalone HTML (CSS inlined, images as relative links) and to PDF (WebView2 `CoreWebView2.PrintToPdfAsync`).
- **REQ-016**: Tabs and session restore. Several files can be open in tabs, and the open tabs come back on the next launch when `restoreSession` is true.

### 1.2 Source and preview sync (the main differentiator)

- **SYNC-001**: Every rendered element and every rendered text run carries its exact source range, as offsets into the source string. These are UTF-16 code-unit offsets, which are the same in C# `string` and JavaScript, so no conversion is needed. Ranges are written in the HTML by the C# renderer (MD-003):
  - `data-sp` is the start offset (inclusive).
  - `data-ep` is the end offset (exclusive).
  - `data-x="0"` marks a run whose rendered text differs from its source, such as escapes, entities and emoji.
- **SYNC-002**: Ctrl+C copies exactly what is selected in the pane that has keyboard focus, never the other pane's selection. The VS Code preview gets this wrong today.
- **SYNC-003**: Two copy shortcuts in the preview:
  - Ctrl+C copies the rendered selection as plain text plus HTML.
  - Ctrl+Shift+C copies the markdown source of the selection. The selection's source range is expanded to whole inline nodes by the C# `SourceRangeExpander` (MD-005), so no markup is cut in half.

  The `copyFromPreview` setting can swap the two shortcuts.
- **SYNC-004**: In Split mode, selecting text in the preview highlights the matching source range in the editor and scrolls it into view. Selecting text in the editor highlights the matching rendered elements in the preview.
- **SYNC-005**: Double-clicking in the preview (Split mode), or Alt+click in any mode, moves the editor cursor to the exact source offset of the clicked character. In View mode it first switches to Split.
- **SYNC-006**: Scroll sync is anchored on blocks, not proportional.
  - The top visible preview block's `data-sp` maps to an editor line.
  - The editor's top visible line maps to the last block whose `data-sp` is at or before that line's start offset.
  - A scroll-origin flag and a 50 ms debounce prevent feedback loops.
- **SYNC-007**: Moving the editor cursor highlights the preview block that contains the cursor offset and keeps it visible.
- **SYNC-008**: The preview's right-click menu offers Copy, Copy as Markdown, Copy as Rich Text and Show in Source.

### 1.3 Editor requirements

- **EDT-001**: The editor is CodeMirror 6 with markdown language support, GFM, fenced-code language highlighting and line wrapping (on by default). It keeps the file's line endings (LF or CRLF, detected by the host).
- **EDT-002**: Smart typing:
  - Enter continues lists and task lists.
  - Tab and Shift+Tab indent and outdent list items.
  - Pasting a URL over a selection creates `[selection](url)`.
  - Ctrl+Shift+F aligns the columns of the GFM table under the cursor. The table formatter is C# (`TableFormatter`, MD-006), called through the bridge.
- **EDT-003**: Pasting an image saves the clipboard PNG to `./assets/<document-name>-<yyyyMMdd-HHmmss>.png` next to the document, creating the folder if needed, and inserts `![](assets/<file>.png)` at the cursor.
- **EDT-004**: Relative links and images that do not resolve to an existing file get `class="mdv-broken"` from the C# renderer (MD-004). The editor underlines the same ranges.
- **EDT-005**: Preview updates while typing are debounced to 30 ms. After the debounce, the full loop (ARC-007) is within PERF-003.

### 1.4 Stability and data safety

- **SAFE-001**: Atomic save.
  - Write to `<file>.mdv-tmp` in the same directory and flush it (`FileStream.Flush(flushToDisk: true)`).
  - Replace the original with `File.Replace` (`ReplaceFileW`), or with `File.Move` if the target does not exist.
  - Keep the original encoding (UTF-8 with or without BOM, or UTF-16 LE or BE when a BOM is present) and the original line endings.
- **SAFE-002**: Crash-safe drafts.
  - While the buffer is dirty, write it every 2 s to `<state-dir>\drafts\<sha256(fullPath)>.md`, with a sidecar `.json` holding `{ path, savedAtUtc, baseFileHashSha256 }`.
  - On the next open of that path, if a draft exists and is newer than the file, show a non-modal banner: "Unsaved changes from <time> — Restore / Discard".
- **SAFE-003**: If the file changes on disk while the buffer is dirty, show a non-modal banner: "File changed on disk — Reload (discard mine) / Keep mine / Compare". Compare opens a read-only side-by-side diff.
- **SAFE-004**: The app must not lose user text in any crash, kill or power-loss scenario in which the OS completed the last draft write.
- **SAFE-005**: Unhandled exceptions in the host and the frontend are written to `<state-dir>\logs\atc-mdv-<yyyyMMdd>.log`, keeping the last 7 days. Nothing leaves the machine.

### 1.5 Performance budgets

All measurements use `benchmarks/fixtures/readme-5k.md` (PERF-FIX-001) on the owner's dev machine (Windows 11, warm disk cache). Each figure is the median of 15 launches after one discarded warmup launch, measured by `benchmarks/measure.ps1`.

- **PERF-001**: Launch to first content paint: at most 520 ms median and 560 ms p90. This was set from the Phase 2 measurement (469 ms median, plus 10%, ADR 0001 D1). WebView2 accounts for about 80% of it, which is why the original 250 ms target is out of reach.
- **PERF-002**: In resident mode, a second `atc-mdv file.md` reaches first content paint in at most 50 ms (median).
- **PERF-003**: From the end of the 30 ms debounce to the painted preview update: at most 16 ms for `readme-500.md` and at most 50 ms for `readme-5k.md`.
- **PERF-004**: Private memory after opening `readme-5k.md`: host process at most 100 MB, and total including all WebView2 processes at most 350 MB (ADR 0001 D3). WebView2 alone uses about 190 MB.
- **PERF-005**: Markdig parse plus `SourceMappedHtmlRenderer` render of `readme-5k.md` takes at most 30 ms, measured by BenchmarkDotNet in `benchmarks/Atc.MarkdownViewer.Benchmarks` (ReadyToRun build, mean). DOM insertion of the first chunk (ARC-006) takes at most 40 ms in WebView2.
- **PERF-006**: In View mode, Mermaid, KaTeX and the editor bundle are not loaded for a document that uses none of them (TEST-012).
- **PERF-FIX-001**: Benchmark fixtures are generated deterministically by `benchmarks/generate-fixtures.mjs`:
  - `benchmarks/fixtures/readme-500.md` (500 lines) and `benchmarks/fixtures/readme-5k.md` (5,000 lines). Both mix headings, paragraphs, lists, task lists, tables, code fences in 8 languages, links, images and alerts, with no mermaid or math. The 5k file has 20 tables and 60 fences; the 500-line file has 2 and 6.
  - `readme-5k-rich.md`: the 5k fixture plus 10 mermaid fences, 30 math blocks and 40 raw HTML snippets.
  - `readme-5k-crlf.md`: the 5k fixture with CRLF line endings and tab indentation.
- **PERF-007**: The benchmarks are permanent and monitored continuously (owner decision, 2026-10-23).
  - `benchmarks/Atc.MarkdownViewer.Benchmarks` (BenchmarkDotNet), the fixtures and `benchmarks/measure.ps1` stay in the repository for the life of the project.
  - `benchmarks/run.ps1` runs the BenchmarkDotNet suite and the startup and edit measurements against the current build. It writes one dated JSON entry to `benchmarks/history/` (kept locally, not committed) and prints the change against the previous entry, flagging anything more than 10% slower.
  - Run it before every release PR merge, and after any change tagged `perf:`.

### 1.6 Security

- **SEC-001**: Raw HTML is sanitized in one place: the assembled HTML output of the C# renderer, never per node. Per-node sanitizing would break, because Markdig emits the opening and closing tags of inline HTML as separate nodes.
  - Sanitizing runs only when the parsed document contains an `HtmlBlock` or `HtmlInline` node, and only on the render units that contain raw HTML (MD-008).
  - It uses `HtmlSanitizer` (NuGet `HtmlSanitizer`, namespace `Ganss.Xss`) with its default allowlist plus the tags `details`, `summary`, `kbd`, `sup`, `sub`, `picture`, `source` and `input` (`type=checkbox`, `disabled`), and the attributes `data-sp`, `data-ep`, `data-x`, `data-h`, `data-href`, `class`, `id` and `aria-*`.
  - Sanitizing stays in C# (ADR 0001 D4). The measured cost is 34–49 ms for the whole 5,000-line HTML-heavy fixture. That is off the startup critical path, and per-unit sanitizing reduces it further.
- **SEC-002**: The renderer page uses this CSP: `default-src 'none'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' https: data:; font-src 'self'; connect-src 'self'`.
- **SEC-003**: The bridge exposes only the messages in ARC-004. Host handlers can read only paths under the open document's folder, the open folder in folder mode, or paths the user picked in a dialog. This is enforced by `Atc.MarkdownViewer.Core.Security.PathScope`.
- **SEC-004**: Remote images load by default. The `loadRemoteImages` setting can turn this off.
- **SEC-005**: No telemetry, no update pings, and no network calls except remote images the document references. Updates come through winget.
- **SEC-006**: Releases are unsigned until a free signing option is approved (`process-manual-setup-1.md` Phase 3: SignPath Foundation for open source). The README documents the SmartScreen prompt ("More info", then "Run anyway") and the `SHA256SUMS.txt` check. Paid signing (Azure Artifact Signing, CA certificates) is out of scope: there is no business entity and no budget.

### 1.7 Architecture

- **ARC-001**: A C# core with a thin web view. The work splits like this:

  | C# (.NET 10) | TypeScript (in WebView2) |
  |---|---|
  | CLI, single instance, resident mode | Bridge client (`postMessage` wrapper) |
  | File reading, encoding and EOL detection, atomic save, drafts, file watching | Inserting HTML chunks and patching changed blocks |
  | Markdig parsing and HTML rendering with source offsets | Mapping DOM selection to `data-sp`/`data-ep` offsets, mirror highlights, scroll sync |
  | Outline, word count and broken-link detection (from the AST) | CodeMirror 6 editor setup and keymaps |
  | Expanding a selection to whole inline nodes, table formatting | Find-in-page (CSS Custom Highlight API) |
  | HTML sanitizing, relative URL rewriting | Lazy Mermaid, KaTeX and Lezer code highlighting |
  | Fuzzy file search, folder listing | Shell UI: tabs, outline panel, palette, status bar, banners |
  | Clipboard (rich formats), dialogs, PDF export, settings, logging | |

  TypeScript cannot be avoided for the right-hand column, because those parts only exist inside the browser engine. The TypeScript layer contains no markdown parsing.
- **ARC-002**: Solution layout. The solution is `Atc.MarkdownViewer.slnx` at the repository root, with the projects:
  - `src/Atc.MarkdownViewer/`: the app, `AssemblyName=atc-mdv`, with the WinUI 3 window, WebView2 hosting and bridge handlers.
  - `src/Atc.MarkdownViewer.Core/`: no UI references. CLI, files, encoding, watcher, atomic writer, drafts, settings, single instance, path scope, fuzzy search, logging.
  - `src/Atc.MarkdownViewer.Markdown/`: no UI references. The Markdig pipeline, `SourceMappedHtmlRenderer`, `SourceRangeExpander`, `TableFormatter`, outline and link checking.
  - `src/frontend/`: the TypeScript and Vite web view.
  - Tests: `test/Atc.MarkdownViewer.Core.Tests/`, `test/Atc.MarkdownViewer.Markdown.Tests/` and `test/Atc.MarkdownViewer.AppTests/` (Playwright for .NET against the running app).
  - Benchmarks: `benchmarks/Atc.MarkdownViewer.Benchmarks/`.
- **ARC-003**: Markdig pipeline, built once in `MarkdownPipelineFactory.Create()` and cached (the pipeline is thread-safe): `new MarkdownPipelineBuilder().UsePreciseSourceLocation().UseAlertBlocks().UsePipeTables().UseTaskLists().UseFootnotes().UseAutoLinks().UseEmphasisExtras(EmphasisExtraOptions.Strikethrough).UseEmojiAndSmiley(enableSmileys: false).UseYamlFrontMatter().UseMathematics().UseDiagrams().UseAutoIdentifiers(AutoIdentifierOptions.GitHub).Build()`. Each extension's output is pinned by a test in TEST-001.
- **ARC-004**: Bridge contract. Messages are JSON `{ id, type, payload }`. The C# side is `src/Atc.MarkdownViewer/Bridge/Messages.cs` (records serialized with `System.Text.Json` source generation). The TypeScript mirror is `src/frontend/src/bridge/contract.ts`. TEST-031 checks that the two match.
  - Web view to host: `ready`, `render { docId, version, text }`, `expandRange { docId, version, start, end }`, `formatTable { docId, version, offset }`, `saveFile { docId, text }`, `writeDraft { docId, text }`, `deleteDraft { docId }`, `listFolder { path }`, `fuzzyFind { query }`, `openExternal { url }`, `openPath { path, newWindow }`, `pickFile`, `clipboardWrite { text, html }`, `saveImage { docId, pngBase64 }`, `exportPdf { targetPath }`, `exportHtml { targetPath, html }`, `getSettings`, `setSettings { patch }`, `firstContentPaint { tMs }`, `log { level, message }`.
  - Host to web view: `open { docId, path, text, encoding, eol, version, chunks[], outline[], words, line, mode }`, `chunk { docId, version, html }`, `rendered { docId, version, blocks[{ h, html }], outline[], words }`, `fileChanged { docId, text, version, blocks[], outline[], words }`, `fileDeleted { docId }`, `folderOpened { root, files[] }`, `draftFound { docId, draftText, savedAtUtc }`, `settingsChanged { settings }`, `result { id, ok, payload, error }`.
  - The frontend ships `src/frontend/src/bridge/mock.ts`, which replays pre-rendered JSON fixtures produced by the C# renderer (`pnpm fixtures:render`, TASK-033). The web view can then run in a normal browser for UI work and Playwright tests without the host.
- **ARC-005**: Asset loading. The frontend bundle is served from `https://app.mdv` through `CoreWebView2.SetVirtualHostNameToFolderMapping("app.mdv", <exeDir>\ui, CoreWebView2HostResourceAccessKind.Allow)`. The open document's folder is mapped to `https://doc.mdv` and re-mapped when it changes. The C# renderer rewrites relative image URLs to `https://doc.mdv/<relative path>`, and relative `.md` links to `href="#"` with `data-href="<relative path>"` for in-app navigation.
- **ARC-006**: Startup pipeline.
  1. The process starts and parses the CLI.
  2. On a background thread, read the file, parse it with Markdig, render it, and split the HTML into chunks of 200 top-level blocks.
  3. In parallel with step 2, create the window and `CoreWebView2Environment` and navigate to `https://app.mdv/index.html`.
  4. The web view boots and sends `ready`.
  5. The host sends `open` with the first chunk, then the remaining chunks as `chunk` messages.
  6. The web view sets the first chunk's HTML, waits two `requestAnimationFrame` callbacks, and sends `firstContentPaint`. It appends the remaining chunks in `requestIdleCallback`.
- **ARC-007**: Edit loop.
  1. CodeMirror reports a change; wait for a 30 ms debounce.
  2. The web view sends `render { docId, version, text }`.
  3. The host renders on a background thread, dropping any request whose `version` is older than the newest one received.
  4. The host sends `rendered` with one entry per render unit (MD-008), each carrying `hash` (a 64-bit XxHash3 of the unit's source slice in hex) and its HTML.
  5. The web view tracks which DOM nodes each unit inserted. It keeps units whose hash matches in sequence and replaces only the rest (`src/frontend/src/view/patch.ts`). The hash is carried in the message, not as an HTML attribute.

  If PERF-003 fails, send CodeMirror change sets instead of full text, so the host keeps the buffer and applies the changes (TASK-079).
- **ARC-008**: Single instance uses a named pipe `atc-mdv-<user SID>` (`NamedPipeServerStream` with `PipeOptions.CurrentUserOnly`). A second invocation forwards its argv to the running instance and exits with code 0. The running instance opens the file in a new tab, or a new window when `--new-window` is given.
- **ARC-009**: Resident mode. When `keepResident` is true (default false), closing the last window hides it and keeps the process and WebView2 alive, so the next invocation meets PERF-002. `atc-mdv --quit` ends the process.
- **ARC-010**: Paths.
  - Settings: `%APPDATA%\AtcMarkdownViewer\settings.json`.
  - State (drafts, logs, session, WebView2 user data folder): `%LOCALAPPDATA%\AtcMarkdownViewer\`.
  - Portable mode (a `portable.flag` file next to the exe) puts both under `<exe dir>\data\`.
- **ARC-011**: Settings are a JSON file with no settings UI in v1. The palette command "Open settings" opens `settings.json` in atc-mdv's own Edit mode. Defaults live in `Atc.MarkdownViewer.Core.Settings.AppSettings`: `theme: "system"`, `readingWidth: 860`, `keepResident: false`, `restoreSession: true`, `loadRemoteImages: true`, `copyFromPreview: "rendered"`, `wrap: true`, `fontSizePx: 16`.
- **ARC-012**: App chrome colours come from the concept page the owner approved (https://claude.ai/artifact/EFqiH6xjEv9Gc2RNqxKxA1). They apply to the title bar area, tabs, outline, status bar, palette, banners and mode switch, and are defined in `src/frontend/src/styles/tokens.css`.
  - Light: `--bg #f3f5f7`, `--surface #ffffff`, `--sunk #e8ecf0`, `--ink #15202b`, `--muted #5b6876`, `--line #d3dae1`, `--accent #0d6b66`, `--accent-soft #d6ece9`, `--warn #9a5b00`, `--warn-soft #fbefd9`, `--no #a1303a`, `--no-soft #f6dfe1`.
  - Dark: `--bg #0f1419`, `--surface #161d24`, `--sunk #1d262f`, `--ink #e4e9ee`, `--muted #93a0ad`, `--line #2a3540`, `--accent #4fc3b8`, `--accent-soft #143633`, `--warn #e5a54a`, `--warn-soft #3a2a12`, `--no #ef7f88`, `--no-soft #3b1c20`.
  - Alerts, the mirror-selection highlight and the live-reload indicator use `--accent` and `--accent-soft`.
  - The rendered markdown body keeps GitHub's layout and spacing, with link and alert colours taken from these tokens.
- **ARC-013**: Fonts are system fonts only: Segoe UI, plus Cascadia Mono with Consolas as fallback. No web fonts are bundled.

### 1.8 Markdown core (C#)

- **MD-001**: `IMarkdownRenderService.Render(string source, RenderContext ctx) -> RenderResult` lives in `Atc.MarkdownViewer.Markdown`.
  - `RenderContext` holds `{ DocumentDirectory, AssetRoot, AssetBaseUrl, LoadRemoteImages, FileExists }`, with factories `ForFile(path)` and `ForText()` (stdin).
  - `RenderResult` holds `{ Source, Document, Blocks, Outline, WordCount, HasRawHtml, HasMermaid, HasMath }`.
  - `RenderedBlock` is `{ Hash, Html, SourceStart, SourceEnd }`, one per render unit (MD-008).
- **MD-002**: Markdig's `SourceSpan.End` is inclusive. The renderer always emits `data-ep = Span.End + 1`. Every test compares against `source[sp..ep]` (C# range syntax, end-exclusive).
- **MD-003**: Source mapping is done in two steps. `SourceMapAnnotator` adds attributes to the parsed AST. `SourceMappedHtmlRendererFactory` then builds Markdig's `HtmlRenderer` with its own literal, HTML-entity and front-matter renderers swapped in.
  - Every block element and every inline element gets `data-sp` and `data-ep`.
  - A text run is exact when its text appears verbatim in its source span, with only whitespace around it; pipe-table cell spans include padding.
  - An exact run that is the only content of a paragraph, heading, emphasis or link puts `data-ts` (text start) on that element instead of getting a wrapper.
  - Every other run is wrapped in `<span data-sp data-ep>`. For exact runs that is the exact text range. For inexact runs it is the whole span plus `data-x="0"`: escapes, entities, emoji. Tight-list and table-cell paragraphs have no element of their own, so their runs are always wrapped.
  - `code` elements (inline and fenced) carry `data-ts`, plus `data-x="0"` when their text is not verbatim, for example with CRLF line endings.
  - Web view mapping rule: a text node whose parent has `data-ts` maps to `ts + offset`. A text node inside a wrapper span maps to `sp + offset`. With `data-x="0"`, it maps to the element start.
  - With the source-map additions stripped, the HTML is identical to Markdig's default `HtmlRenderer` output for the same pipeline (TEST-002).
- **MD-004**: Link handling in the renderer:
  - Relative URLs are rewritten per ARC-005.
  - An unresolved relative target (`ctx.FileExists` returns false) adds `class="mdv-broken"`.
  - External links keep `href` and get `rel="noopener noreferrer"`. The web view intercepts clicks and sends `openExternal`.
  - Relative images are served from `AssetBaseUrl` (default `https://doc.mdv/`) only when they are inside `AssetRoot`. Images outside it get class `mdv-outside-root` and no `src`.
  - Relative links get `href="#"` plus `data-href` holding the original target.
  - Only `http`, `https` and `mailto` URLs are kept. Others, such as `javascript:` and `file:`, get class `mdv-unsafe` and an empty URL.
  - When `LoadRemoteImages` is off, remote images keep their URL in `data-src` and get class `mdv-remote-blocked`.
- **MD-005**: `SourceRangeExpander.Expand(MarkdownDocument doc, string source, int start, int end) -> (int Start, int End)` returns the smallest range that covers `[start, end)` and cuts no element or inexact run. Exact text runs may still be cut. A range that spans more than one leaf block is expanded from the start of the first block's source line (so list and quote markers are included) to the end of the last block.
- **MD-006**: `TableFormatter.Format(string source, int offset) -> TextEdit?` returns `null` if `offset` is not inside a pipe table. Otherwise it returns one replacement edit for the table with columns padded to equal width, keeping alignment markers (`:---`, `:---:`, `---:`).
- **MD-008**: Render units. Raw HTML such as `<details>` … `</details>` can wrap several top-level markdown blocks, so a top-level `HtmlBlock` that opens an element left unclosed starts a render unit. That unit absorbs the following top-level blocks until the matching closing tag. Every other top-level block is its own unit. Units are the granularity for chunks, hashes and sanitizing.
- **MD-007**: The host keeps the last `RenderResult.Document` per `docId` and `version` for `expandRange` and `formatTable`. A request for an older version gets `result { ok: false, error: "stale" }`, and the web view retries with the current version.

### 1.9 Native window decision

- **DEC-001**: The native window is WinUI 3 (owner decision, 2026-10-23): Windows App SDK latest stable, unpackaged (`WindowsPackageType=None`), `Microsoft.UI.Xaml.Controls.WebView2`, Mica backdrop, `ExtendsContentIntoTitleBar=true`.
  - The app is fully standalone, with the Windows App SDK bundled (`WindowsAppSDKSelfContained=true`) and .NET self-contained (owner decision, 2026-10-24, ADR 0001 D2). This costs about 75 MB more disk than relying on the shared runtime, with the same startup time.
  - Tabs live in the title bar: WebView2's non-client region support (`IsNonClientRegionSupportEnabled` plus CSS `app-region: drag`) works, and the owner confirmed dragging by hand.
  - Native AOT was not pursued: it needs Visual Studio's C++ workload, and .NET is not the startup bottleneck. A WPF comparison was not built, because WPF would host the same WebView2 that accounts for about 80% of startup.
- **DEC-002**: The markdown core is C# with Markdig (owner decision, 2026-10-23). Parsing in TypeScript (ALT-004) is rejected.
- **DEC-003**: Linux and macOS are out of scope for v1. Phase 10 is On Hold. The core projects have no Windows UI references, so a Photino.NET host could reuse them later (ALT-008).

### 1.10 Local development

- **DEV-001**: Prerequisites:
  - Windows 10 22H2 or Windows 11.
  - .NET 10 SDK.
  - Visual Studio 2022 or later with the ".NET desktop development" workload and "Windows App SDK C# templates", or JetBrains Rider.
  - Node.js 22 LTS with pnpm 9 (`corepack enable`), and Git.
  - The WebView2 runtime (preinstalled on Windows 11).
  - Inno Setup 6 (`winget install JRSoftware.InnoSetup`).
- **DEV-002**: Day-to-day commands, run from the repository root:
  - `dotnet build` builds everything. The app project's MSBuild target runs `pnpm --dir src/frontend build` when `src/frontend/dist/index.html` is missing or older than the frontend sources.
  - `dotnet test` runs all C# tests.
  - `dotnet run --project src/Atc.MarkdownViewer -- README.md` runs the app. Visual Studio's F5 works the same way through `launchSettings.json`, with a profile per sample file.
  - `pnpm dev` starts the Vite dev server with `MockBridge` at `http://localhost:5173`, for UI work in a browser with hot reload and no .NET build.
  - `pnpm dev:app` starts Vite and the app with `MDV_DEV_URL=http://localhost:5173`. The app then loads the UI from Vite, so UI changes hot-reload inside the real window. F12 opens WebView2 DevTools in Debug builds.
  - `pnpm test` runs Vitest. `pnpm test:e2e` runs Playwright against the Vite build with `MockBridge`.
  - `dotnet test test/Atc.MarkdownViewer.AppTests` runs Playwright for .NET attached to the real `atc-mdv.exe` over WebView2 CDP (TEST-020).
  - `dotnet run -c Release --project benchmarks/Atc.MarkdownViewer.Benchmarks` runs the Markdig benchmarks (PERF-005).
  - `pwsh benchmarks/measure.ps1 -Exe artifacts/publish/win-x64/atc-mdv.exe -Fixture benchmarks/fixtures/readme-5k.md -Runs 15 -Label local` runs the startup benchmark (PERF-001).
  - `pwsh scripts/publish.ps1 -Arch x64` runs `dotnet publish` into `artifacts/publish/win-x64/`.
  - `pwsh scripts/package.ps1 -Arch x64 -Version 0.0.0-local` produces the installer and portable zip in `artifacts/`.
  - `dotnet format --verify-no-changes` and `pnpm lint` check style.
- **DEV-003**: The README's "Development" section documents DEV-001 and DEV-002, and how to debug: attach to `atc-mdv.exe` for C#, and press F12 for the web view's DevTools.
- **DEV-004**: atc-net .NET conventions apply, copied from `atc-net/atc-coding-rules-updater`:
  - Root `Directory.Build.props`, which holds the metadata, the release-please-managed version block, compile settings, analyzer settings, `TreatWarningsAsErrors` in Release, and the shared analyzers.
  - Package versions live in each project file. There is no central package management.
  - `global.json` and `.gitattributes`.
  - `atc-coding-rules-updater.json` and `atc-coding-rules-updater.ps1`, which fetch `.editorconfig` and the `src` and `test` `Directory.Build.props` files.
  - The `.slnx` solution format and xUnit tests.

### 1.11 Release and publishing

- **REL-001**: Commit messages follow Conventional Commits (`feat:`, `fix:`, `perf:`, `docs:`, `chore:`, `refactor:`, `test:`, `ci:`, and `!` for breaking changes). release-please builds the changelog from the commit messages that reach `main`, and silently skips any that are not conventional. There is no restriction on merge method: merge commits, squash and rebase are all allowed. With a merge commit, the individual commits in the PR are what count.
- **REL-002**: release-please follows the atc-net pattern used in `atc-net/atc-coding-rules-updater` and `atc-net/atc-claude-kanban`.
  - The workflow file is `.github/workflows/release-please.yml`, triggered on push to `main`.
  - It uses `googleapis/release-please-action@45996ed1f6d02564a971a2fa1b5860e934307cf7 # v5.0.0` with `token: ${{ secrets.RELEASE_PLEASE_PAT }}`.
  - This is the same token setup as the atc-net repos. The owner creates the token in `atc-net-apps` later (`process-manual-setup-1.md` TASK-M03).
  - release-please keeps an open "chore: release version x.y.z" PR with the generated `CHANGELOG.md`. Merging it creates the tag `vX.Y.Z` and a GitHub Release whose notes are the changelog section.
  - All third-party actions are pinned by commit SHA with a `# vX.Y.Z` comment.
- **REL-003**: `release-please-config.json` mirrors `atc-net/atc-coding-rules-updater/release-please-config.json`:
  - the `$schema` line and `"release-type": "simple"`,
  - `"pull-request-title-pattern": "chore: release version ${version}"`,
  - `"pull-request-header": ":robot: Preparing to release next version"` and `"pull-request-footer": "This pull request was generated by release-please."`,
  - `"packages": { ".": {} }`,
  - `changelog-sections`: `feat` "New features", `fix` with scope `deps` "Upgrades", `fix` "Bug fixes", `perf` "Performance improvements", `revert` "Reverts".

  The reference config's `bootstrap-sha` belongs to that repo's history and is left out. The `extra-files` are:
  - `Directory.Build.props`, using the `x-release-please-start-version` and `x-release-please-end` block, as in the reference repo;
  - `src/frontend/package.json` (type `json`, jsonpath `$.version`), added in TASK-030;
  - `installer/atc-mdv.iss` (the version line marked `; x-release-please-version`), added in TASK-085.

  `.release-please-manifest.json` starts at `{ ".": "0.0.0" }`.
- **REL-004**: `.github/workflows/release-please.yml` jobs:
  1. `release-please` (ubuntu-latest) has the outputs `release_created` and `tag_name`.
  2. `build-windows` (windows-latest, only if `release_created`) runs a matrix of `arch: [x64, arm64]`. Steps:
     - check out the tag; set up dotnet 10.0.x and node 22; `corepack enable`; `pnpm install --frozen-lockfile`;
     - `dotnet test -c Release`;
     - `pwsh scripts/publish.ps1 -Arch <arch>`;
     - sign `atc-mdv.exe` (REL-006; skipped while signing is not set up);
     - `pwsh scripts/package.ps1`;
     - sign the installer (same condition);
     - `gh release upload <tag> artifacts/*`.
  3. `winget` (windows-latest, needs `build-windows`, only if `vars.WINGET_ENABLED == 'true'`) uses `vedantmgoyal9/winget-releaser` with `identifier: atc-net.MarkdownViewer`, `installers-regex: 'atc-mdv-.*-win-(x64|arm64)-setup\.exe$'` and `token: ${{ secrets.WINGET_TOKEN }}`.
- **REL-005**: Release assets for each version: `atc-mdv-<ver>-win-x64-setup.exe`, `atc-mdv-<ver>-win-arm64-setup.exe`, `atc-mdv-<ver>-win-x64-portable.zip`, `atc-mdv-<ver>-win-arm64-portable.zip` and `SHA256SUMS.txt`. The installers are Inno Setup, per-user, and need no admin rights.
- **REL-006**: Signing is free or not done at all. Releases ship unsigned until SignPath Foundation approves the project (`process-manual-setup-1.md` Phase 3).
  - Once approved, the signing steps use SignPath's GitHub Action and the settings SignPath issues, recorded in `docs/adr/0002-code-signing.md`.
  - The steps run only when the repository variable `SIGNPATH_ORGANIZATION_ID` is set, so the pipeline works unchanged before and after approval.
- **REL-007**: winget. The first version must exist in `microsoft/winget-pkgs` before winget-releaser can update it. The first submission is manual (`process-manual-setup-1.md` Phase 2). Until it has merged, `WINGET_ENABLED` stays unset.
- **REL-008**: `.github/workflows/build.yml` mirrors `atc-net/atc-coding-rules-updater/.github/workflows/build.yml`: the `dotnet-build` and `dotnet-test` jobs, run on pushes to `main` and on pull requests. The OS matrix is reduced to `windows-latest` because the app is Windows only. Once the frontend exists (TASK-042), both jobs also set up Node and pnpm, because `dotnet build` builds the frontend bundle. Benchmarks run locally only.

### 1.12 Constraints and guidelines

- **CON-001**: No Electron, no bundled Chromium, and no Node.js at runtime. Node is a build-time tool only.
- **CON-002**: No plugin system in v1, no accounts or sync, no block-based WYSIWYG editing, no telemetry.
- **CON-003**: Supported systems are Windows 10 22H2 and later and Windows 11, on x64 and arm64. Windows 10 may lack the WebView2 runtime. The installer checks `HKLM\SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}` and the HKCU equivalent, and runs the Evergreen bootstrapper if the runtime is missing.
- **CON-004**: Names:
  - Product: "ATC Markdown Viewer".
  - Executable and command: `atc-mdv` (`atc-mdv.exe`). The `atc-` prefix avoids clashing with other tools named `mdv`.
  - winget identifier: `atc-net.MarkdownViewer`.
  - Repository: `github.com/atc-net-apps/atc-soft-markdown-viewer`.
- **GUD-001**: The frontend uses vanilla TypeScript modules in strict mode, with no UI framework. CodeMirror 6 is the only large dependency, and it is lazy-loaded.
- **GUD-002**: User-facing strings live in `src/frontend/src/strings.ts` (web view) and `Atc.MarkdownViewer.Core/Resources/Strings.resx` (host). English only in v1.
- **GUD-003**: Keyboard first. Every command has a shortcut or a palette entry, and focus is always visible.
- **PAT-001**: Lazy feature modules are loaded with dynamic `import()` behind a `features/` boundary: `features/editor.ts`, `features/mermaid.ts`, `features/katex.ts` and `features/highlight.ts`.

## 2. Implementation Steps

### Implementation Phase 1: Repository foundation and release-please

Status: Complete

- GOAL-001: The repository has its layout, tooling, CI and release-please, so every commit after this phase is conventional and produces changelog entries.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-001 | Add `atc-coding-rules-updater.json` (`projectTarget` `DotNet10`, mappings `src` and `test`) and `atc-coding-rules-updater.ps1`, both copied from `atc-net/atc-coding-rules-updater`. Run the updater to fetch `.editorconfig` and the `src` and `test` `Directory.Build.props` files. Folders are created by the tasks that fill them. | ✅ | 2026-10-23 |
| TASK-002 | Add `Atc.MarkdownViewer.slnx` (empty for now). Copy the root `Directory.Build.props`, `global.json` and `.gitattributes` from `atc-net/atc-coding-rules-updater`. In `Directory.Build.props`, set `OrganizationName` to `atc-net-apps`, `RepositoryName` to `atc-soft-markdown-viewer`, and `Version` to `0.0.0`. The WinUI project overrides `TargetFramework` itself (TASK-031). | ✅ | 2026-10-23 |
| TASK-004 | Write `README.md`: what it is, install (`winget install atc-net.MarkdownViewer`, marked as coming), the CLI (REQ-012), and a "Development" section (DEV-003) that grows with each phase. | ✅ | 2026-10-23 |
| TASK-005 | Add `release-please-config.json` and `.release-please-manifest.json` per REL-003. `extra-files` contains `Directory.Build.props` only; the other entries are added in TASK-030 and TASK-085. | ✅ | 2026-10-23 |
| TASK-006 | Add `.github/workflows/release-please.yml` with only the `release-please` job (REL-002, REL-004 step 1), copied from the reference repo without its NuGet steps. It runs once the repository is pushed and the token exists. | ✅ | 2026-10-23 |
| TASK-008 | Add `.github/workflows/build.yml` per REL-008. | ✅ | 2026-10-23 |

#### Verification

- **VER-001**: `dotnet build` and `dotnet test` exit 0 locally.
  - Result 2026-10-23: `dotnet build Atc.MarkdownViewer.slnx` reported 0 errors, with the solution still empty. PASS.
- **VER-002**: The JSON files parse (`Get-Content release-please-config.json -Raw | ConvertFrom-Json`), and the workflow YAML files parse. The GitHub-side check (release-please opens "chore: release version 0.1.0") is deferred until the repository is pushed.
  - Result 2026-10-23: all four JSON files and both workflow YAML files parse. PASS. The GitHub-side check is deferred.

#### Phase Summary

Phase 1 was completed on 2026-10-23 on branch `feature/init`. Nothing is committed yet; the owner commits.

What was done:
- The repository's base setup is copied from `atc-net/atc-coding-rules-updater`:
  - root `Directory.Build.props`, with `OrganizationName=atc-net-apps`, `RepositoryName=atc-soft-markdown-viewer` and version `0.0.0` inside the release-please start/end block;
  - `global.json` and `.gitattributes`;
  - `atc-coding-rules-updater.json` (DotNet10, with `src` and `test` mappings) and `atc-coding-rules-updater.ps1`.
- Running the coding-rules updater (installed v2.0.253, run directly without the global tool update) created:
  - the root `.editorconfig`;
  - `src/.editorconfig` and `src/Directory.Build.props`;
  - `test/.editorconfig` and `test/Directory.Build.props`.

  It also bumped the root analyzer versions to Meziantou.Analyzer 3.0.316 and SonarAnalyzer.CSharp 10.35.0.4138.
- `Atc.MarkdownViewer.slnx` contains only a Docs folder with the README.
- `release-please-config.json` is the reference config without `bootstrap-sha`, and `.release-please-manifest.json` is `{ ".": "0.0.0" }`.
- `.github/workflows/release-please.yml` is the reference workflow without the NuGet steps.
- `.github/workflows/build.yml` is the reference build workflow with the OS matrix reduced to `windows-latest`.
- `README.md` was written.

Notes for continuing:
- No GitHub-side behaviour has been verified, because the repository is local and the token does not exist yet.
- Phase 2 starts with TASK-010 (benchmark fixtures).

### Implementation Phase 2: Benchmarks, spikes and the window decision

Status: Complete

- GOAL-002: Measure WinUI 3 startup (DEC-001), the Markdig core speed (PERF-005), the sanitizer cost (SEC-001) and the keystroke round trip (PERF-003) before building the app.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-010 | Write `benchmarks/generate-fixtures.mjs`, which writes every PERF-FIX-001 fixture from a fixed seed (`20261023`) with a seeded PRNG, so output is byte-identical on every run. Commit the fixtures. | ✅ | 2026-10-24 |
| TASK-011 | Build `spikes/markdown-core/`: a minimal `SourceMappedHtmlRenderer` prototype covering paragraphs, headings, emphasis, links, lists, tables and code, plus a console runner that prints parse and render time for each fixture (median of 30 runs after 5 warmups, ReadyToRun Release build). | ✅ | 2026-10-24 |
| TASK-012 | Build `spikes/web/`: a static `index.html` and `main.js` that receive `open` and `chunk` messages, insert the HTML (ARC-006), send `firstContentPaint`, and log the insert time. Add a 30-line CodeMirror 6 page that sends `render` on each change and applies `rendered` with simple full replacement, logging the round-trip time. | ✅ | 2026-10-24 |
| TASK-013 | Build spike W1 in `spikes/host-winui/`, implementing ARC-006 with the TASK-011 renderer and TASK-012 page, in variants W1a (self-contained) and W1b (framework-dependent). Also try `PublishTrimmed` and `PublishAot` and record pass or fail with the exact error. Also test `CoreWebView2Settings.IsNonClientRegionSupportEnabled` with CSS `app-region: drag`, to find out whether tabs can live in the title bar. With `--bench`, write `MDV_STARTUP_MS=<ms since process start>`, `RENDER_MS` and `INSERT_MS` to `--bench-out <path>` and exit. Process start comes from `Process.GetCurrentProcess().StartTime`. | ✅ | 2026-10-24 |
| TASK-014 | Only if DEC-001's 375 ms fallback triggers: build a WPF comparison spike in `spikes/host-wpf/` with the same behaviour as TASK-013, using `Microsoft.Web.WebView2.Wpf`, and add its results to the ADR for the owner to decide. Otherwise mark this task "skipped, not needed" with the date. | Skipped, not needed (ADR 0001 D1) | 2026-10-24 |
| TASK-015 | Write `benchmarks/measure.ps1` with parameters `-Exe -Fixture -Runs -Label`. It launches once as warmup, discards that run, then runs `Runs` launches. It reads the bench file and samples the peak private working set of the process tree every 20 ms (`Get-CimInstance Win32_Process` descendants, `PrivatePageCount`). It writes `benchmarks/results/<Label>.json` with `{ label, runs, medianMs, p90Ms, minMs, maxMs, firstLaunchMs, medianRenderMs, medianInsertMs, medianPrivateMB, installSizeMB, machine, date }`. `firstLaunchMs` is the warmup run, recorded separately. | ✅ | 2026-10-24 |
| TASK-016 | Measure HtmlSanitizer on the assembled HTML of `readme-5k-rich.md` in the TASK-011 runner (median of 30). Record it and pick the sanitizer location per SEC-001. | ✅ | 2026-10-24 |
| TASK-017 | Run `measure.ps1 -Runs 15` on `readme-5k.md` for W1a, W1b and any trimmed or AOT variant that ran. Measure the keystroke round trip in the TASK-012 editor page for `readme-500.md` and `readme-5k.md` (median over 50 edits). Commit the results to `docs/adr/0001-native-window-results/`. | ✅ | 2026-10-24 |
| TASK-018 | Write `docs/adr/0001-native-window.md` with:<br>• the results table;<br>• the chosen variant (W1a or W1b) and its settings;<br>• the final PERF-001 value;<br>• the sanitizer location (TASK-016);<br>• whether tabs can live in the title bar (TASK-013);<br>• whether ARC-007's change-set fallback is needed.<br><br>Update TASK-031 and TASK-086 in this plan with the chosen variant. | ✅ | 2026-10-24 |

#### Verification

- **VER-003**: `docs/adr/0001-native-window-results/` holds one JSON file per measured variant, each with `runs == 15` and a numeric `medianMs`.
- **VER-004**: `docs/adr/0001-native-window.md` names the chosen WinUI 3 variant (W1a or W1b) and the final PERF-001 value.
- Results 2026-10-24: VER-003 PASS (one JSON file per variant, each with 15 runs). VER-004 PASS (the ADR names W1a, proposed). VER-005 PASS (6.9 ms warm, from BenchmarkDotNet).
- **VER-005**: The TASK-011 runner reports a median parse plus render of at most 30 ms for `readme-5k.md` (PERF-005). If it reports more, the ADR records the profiler finding and the mitigation chosen.

#### Phase Summary

Completed 2026-10-24. Everything lives in `spikes/`, `benchmarks/` and `docs/adr/`, all local and never committed. Details are in ADR 0001.

- **Startup:** about 470 ms. WebView2 accounts for about 80% of it; WinUI 3 and .NET take about 75 ms, and Markdig is never on the critical path.
- **Decisions:** standalone W1a (Windows App SDK bundled). PERF-001 set to 520 ms. PERF-004 set to host ≤ 100 MB and total ≤ 350 MB. Sanitizer stays in C#. Tabs go in the title bar; the owner confirmed dragging by hand.
- **Not pursued:** Native AOT needs the C++ workload. The startup tweaks (`--early-env`, `--inline-first`) gained nothing.
- **Edit round trip (full re-render):** 8.4 ms for 500 lines; 75 ms for 5,000 lines, which needs block patching (Phase 6).
- **Machine change:** the Windows App Runtime 2.5.1 was installed on the owner's PC for the W1b measurement.

### Implementation Phase 3: Markdown core in C#

Status: Complete

- GOAL-003: `Atc.MarkdownViewer.Markdown` renders all of REQ-002 with exact source ranges (SYNC-001, MD-001 to MD-007) and meets PERF-005, fully covered by xUnit tests.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-020 | Create `src/Atc.MarkdownViewer.Markdown/` (net10.0, packages `Markdig`, `HtmlSanitizer` unless TASK-016 moved sanitizing to the frontend, and `System.IO.Hashing`), and `test/Atc.MarkdownViewer.Markdown.Tests/` (xUnit). Add both to the solution. | ✅ | 2026-10-24 |
| TASK-021 | Implement `MarkdownPipelineFactory.Create()` per ARC-003. | ✅ | 2026-10-24 |
| TASK-022 | Implement `SourceMappedHtmlRenderer` per MD-002 and MD-003. Replace the object renderers for `ParagraphBlock`, `HeadingBlock`, `QuoteBlock`, `ListBlock`, `ListItemBlock`, `FencedCodeBlock`, `CodeBlock`, `ThematicBreakBlock`, `HtmlBlock`, `Table`, `TableRow`, `TableCell`, `AlertBlock`, `FootnoteGroup`, `Footnote`, `YamlFrontMatterBlock` (rendered as `<details class="mdv-frontmatter">` with a key-value table), `MathBlock`, `LiteralInline`, `EmphasisInline`, `CodeInline`, `LinkInline`, `AutolinkInline`, `LineBreakInline`, `HtmlInline`, `HtmlEntityInline`, `EmojiInline`, `TaskList`, `MathInline` and `FootnoteLink`. Fenced code renders as `<pre data-sp data-ep data-lang="<info>"><code>` with escaped text. Highlighting happens in the web view (PAT-001). | ✅ | 2026-10-24 |
| TASK-023 | Implement `MarkdownRenderService` per MD-001. It splits output per top-level block, computes `data-h` with `XxHash3`, builds the outline from `HeadingBlock`s (level, plain text, GitHub auto-identifier, `sp`), counts words over `LiteralInline`s, sets the `HasRawHtml`, `HasMermaid` and `HasMath` flags, and runs the sanitizer per SEC-001. | ✅ | 2026-10-24 |
| TASK-024 | Implement link handling per MD-004 in `LinkRewriter`. | ✅ | 2026-10-24 |
| TASK-025 | Implement `SourceRangeExpander` per MD-005 and `TableFormatter` per MD-006. | ✅ | 2026-10-24 |
| TASK-026 | Add `benchmarks/Atc.MarkdownViewer.Benchmarks` (BenchmarkDotNet; not in `Atc.MarkdownViewer.slnx`, because `benchmarks/` is never committed; run with `dotnet run -c Release --project benchmarks/Atc.MarkdownViewer.Benchmarks`) with `Parse`, `Render` and `Sanitize` for every PERF-FIX-001 fixture. Add `benchmarks/run.ps1` and `benchmarks/history/` per PERF-007. Add an xUnit perf guard test, `[Trait("Category","Perf")]`, that runs only when `MDV_PERF=1` and asserts that the median `Render_5k` is at most 30 ms (PERF-005). | ✅ | 2026-10-24 |
| TASK-027 | Write `test/Atc.MarkdownViewer.Markdown.Tests/Fixtures/gfm/*.md` (one fixture per REQ-002 construct, plus `crlf-tabs.md`, `escapes-entities.md`, `emoji.md` and `nested-lists-deep.md`) with expected HTML snapshots in `Snapshots/*.html`. Snapshot updates are explicit: they happen only when `UPDATE_SNAPSHOTS=1`. | ✅ | 2026-10-24 |

#### Verification

- **VER-006**: `dotnet test test/Atc.MarkdownViewer.Markdown.Tests` passes, including TEST-001 to TEST-005.
- **VER-007**: `MDV_PERF=1 dotnet test test/Atc.MarkdownViewer.Markdown.Tests --filter Category=Perf -c Release` passes (PERF-005).
- Results 2026-10-24:
  - VER-006 PASS: 135 tests, 134 passed, 1 skipped (the perf guard, opt-in).
  - VER-007 PASS.
  - `dotnet build -c Release` of the solution reports 0 warnings, and the CI-style `dotnet test` passes.

#### Phase Summary

Completed 2026-10-24. `src/Atc.MarkdownViewer.Markdown` and `test/Atc.MarkdownViewer.Markdown.Tests` are in `Atc.MarkdownViewer.slnx`.

- **Design refinements while building:** MD-003 uses `data-ts` plus wrapper spans, and exactness tolerates surrounding whitespace. ARC-007 carries the hash in the message, not as `data-h`. MD-004 gained an asset root and a scheme allowlist. MD-005 takes the source text.
- **Bugs found by the tests and fixed:**
  - Markdig's `Descendants()` on a leaf block skips its inlines, so inline raw HTML escaped sanitizing.
  - `StringLineGroup.ToString()` keeps CRLF.
  - Math blocks are fenced code blocks with added delimiters.
- **Tests:**
  - Snapshots: `Snapshots/*.html`; rewrite with `UPDATE_SNAPSHOTS=1`.
  - Conformance against Markdig: all 16 fixtures (LF and CRLF/tabs) and all 672 GFM spec examples match.
  - Source-map round trips, plus unit tests for the expander, table formatter, render units, link rewriting, sanitizer and document facts.
  - GitHub fidelity: Markdig matches cmark-gfm on 651 of 672 spec examples, with the 21 known differences in `Fixtures/spec/markdig-vs-github.txt`. The test fails only on new differences.
  - The GFM spec text is in `Fixtures/spec/gfm-spec.txt` (CC-BY-SA 4.0).
- **Performance (BenchmarkDotNet, first entry in `benchmarks/history/`):** Render takes 9.8 ms for 5,000 lines and 10.4 ms for the HTML-heavy file including per-unit sanitizing. Parse takes 2.7 ms.
- **Upstream issue:** the atc-coding-rules dotnet10 `test/Directory.Build.props` references a non-existent package, `Microsoft.Testing.Extensions.Testing ` (trailing space). It was removed locally and will return when the updater runs again, until it is fixed in `atc-net/atc-coding-rules`.

### Implementation Phase 4: App host and the web view

Status: Not started

- GOAL-004: The real app opens files from the CLI and Explorer, renders through the C# core, watches files, runs as a single instance, and meets PERF-001 and PERF-002.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-030 | Add root `package.json` (private, `"packageManager": "pnpm@9"`, scripts `dev`, `dev:app`, `test`, `test:e2e`, `lint` and `fixtures:render`, delegating to `src/frontend`), `pnpm-workspace.yaml` and `.nvmrc` (`22`), and add `node_modules/`, `dist/`, `artifacts/`, `benchmarks/results/`, `playwright-report/` and `test-results/` to `.gitignore`. Scaffold `src/frontend/`: Vite 6, TypeScript 5 strict, ESLint (flat config with `typescript-eslint`), Vitest and Playwright. `index.html` carries the SEC-002 CSP and loads `src/main.ts`. Add `src/frontend/package.json` to `release-please-config.json` `extra-files`. | | |
| TASK-031 | Create `src/Atc.MarkdownViewer/` as an unpackaged WinUI 3 app (`MainWindow.xaml` hosting `WebView2`, Mica, `ExtendsContentIntoTitleBar`), `AssemblyName=atc-mdv`, ReadyToRun, self-contained with the Windows App SDK bundled (DEC-001). The spike's startup tweaks (`--early-env`, `--inline-first`) are not carried over. Add the MSBuild target from DEV-002 that builds the frontend and copies `src/frontend/dist/**` to `<outdir>\ui\`. Create `src/Atc.MarkdownViewer.Core/` and `test/Atc.MarkdownViewer.Core.Tests/`. | | |
| TASK-032 | Implement the bridge. `Bridge/Messages.cs` holds the ARC-004 records with a `JsonSerializerContext`. `Bridge/BridgeRouter.cs` dispatches to `Bridge/Handlers/*Handler.cs`, one class per message type. On the web side, write `src/frontend/src/bridge/contract.ts`, `bridge/webview2.ts` (`window.chrome.webview.postMessage` and the `message` event) and `bridge/index.ts`. | | |
| TASK-033 | Add the console tool `src/Atc.MarkdownViewer.FixtureRenderer/` and the script `pnpm fixtures:render`. The tool renders `src/frontend/test/fixtures/*.md` through `MarkdownRenderService` into `*.render.json` (the `open` payload). `bridge/mock.ts` serves these files. CI checks that the committed JSON is up to date. | | |
| TASK-034 | Implement the CLI per REQ-012 in `Core/Cli/CliParser.cs` with `System.CommandLine`. Exit codes are 0 for OK, 2 for a missing file and 3 for an unreadable file. | | |
| TASK-035 | Implement `Core/Files/TextFileReader.cs`. Encoding detection: check for a UTF-8, UTF-16 LE or UTF-16 BE BOM; otherwise do a strict UTF-8 decode, falling back to Windows-1252 and reporting `encoding: "windows-1252"`. EOL detection: `crlf` if at least 50% of line breaks are CRLF, otherwise `lf`. | | |
| TASK-036 | Implement the startup pipeline (ARC-006) in `Program.cs` and the window class, plus `--bench` and `--bench-out`. Implement the web side in `src/frontend/src/view/insert.ts`. | | |
| TASK-037 | Implement virtual origins per ARC-005 in `WebView/OriginMapper.cs`. | | |
| TASK-038 | Implement `Core/Files/DocumentWatcher.cs`. A `FileSystemWatcher` on the document's directory matches by full path, which handles atomic-rename saves from other editors, with a 75 ms debounce. On change: read, render and send `fileChanged`. The web side (`view/patch.ts`) patches blocks and restores the top block anchor (REQ-003). | | |
| TASK-039 | Implement `Core/Ipc/SingleInstance.cs` (ARC-008) and resident mode (ARC-009). | | |
| TASK-040 | Implement the `listFolder`, `fuzzyFind` (`Core/Search/FuzzyMatcher.cs`, subsequence match with bonuses for consecutive characters and segment starts), `openExternal` (`Process.Start` with `UseShellExecute=true`, allowed only for `http`, `https` and `mailto`), `openPath` and `pickFile` handlers. Path scope is enforced through `Core/Security/PathScope.cs` (SEC-003). | | |
| TASK-041 | Implement settings (`Core/Settings/`, ARC-010 and ARC-011) and logging (`Core/Logging/RollingFileLogger.cs`, SAFE-005). | | |
| TASK-042 | Extend both `build.yml` jobs with `actions/setup-node`, `corepack enable` and `pnpm install --frozen-lockfile`, and extend `dotnet-test` with `pnpm test` (REL-008). | | |

#### Verification

- **VER-008**: `pwsh scripts/publish.ps1 -Arch x64` produces `artifacts/publish/win-x64/atc-mdv.exe` and `artifacts/publish/win-x64/ui/index.html`.
- **VER-009**: `dotnet test test/Atc.MarkdownViewer.AppTests` passes TEST-020. It launches `atc-mdv.exe benchmarks/fixtures/readme-5k.md` with `WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS=--remote-debugging-port=9222`, connects with `Playwright.Chromium.ConnectOverCDPAsync`, and asserts the first `h1` text.
- **VER-010**: `measure.ps1 -Runs 15` reports a median of at most 250 ms and a p90 of at most 350 ms (PERF-001). With `keepResident: true`, a second-file launch has a median of at most 50 ms (PERF-002).
- **VER-011**: `dotnet test test/Atc.MarkdownViewer.Core.Tests` passes TEST-021 to TEST-024.

#### Phase Summary

_(Write when the phase completes.)_

### Implementation Phase 5: Viewer features (one task per PR)

Status: Not started

- GOAL-005: Complete viewer feature set REQ-004 to REQ-012.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-050 | Add `src/frontend/src/styles/tokens.css` (ARC-012), `styles/markdown.css` (GitHub markdown layout using the tokens) and the shell layout in `src/frontend/src/shell/` (`tabs.ts`, `modes.ts` with the mode switch, `toc.ts`, `statusbar.ts` with live state, word count, encoding and EOL, and `layout.css`). The theme follows `prefers-color-scheme`, with an override set as `data-theme` on `html` (REQ-009). | | |
| TASK-051 | Table of contents tree (REQ-005) in `src/frontend/src/shell/toc.ts`, drawn from the host-provided `outline[]` as nested, collapsible `<details>` groups. An `IntersectionObserver` tracks the current section, and Ctrl+Shift+O toggles the panel. | | |
| TASK-052 | Link navigation and history (REQ-004) in `src/frontend/src/nav/history.ts`, keeping `{ path, topBlockSp }` entries. Clicks on `[data-href]` send `openPath`. | | |
| TASK-053 | Find (REQ-006) in `src/frontend/src/find/find.ts`, using the CSS Custom Highlight API so the DOM is not modified. | | |
| TASK-054 | Folder mode and the Ctrl+P picker (REQ-007). The tree UI is in `src/frontend/src/folder/`, and the listing and scoring use the host `listFolder` and `fuzzyFind`. | | |
| TASK-055 | Command palette (REQ-008) in `src/frontend/src/commands/`, with a registry `registerCommand({ id, title, shortcut, run })` and all shortcuts in `commands/keymap.ts`. | | |
| TASK-056 | Code highlighting (`features/highlight.ts`): lazily load Lezer grammars through `@codemirror/language-data` for each `pre[data-lang]` and highlight with `@lezer/highlight` `highlightCode`. Add the code-block copy button (REQ-010). | | |
| TASK-057 | Copy as Rich Text (REQ-011). Build HTML from the selected DOM with inline styles from `styles/clipboard-inline.css` and send `clipboardWrite`. The host writes `CF_HTML` (with a correct header) and `CF_UNICODETEXT` through `Windows.ApplicationModel.DataTransfer.Clipboard` (`DataPackage.SetHtmlFormat` with `HtmlFormatHelper.CreateHtmlFormat`, plus `SetText`). | | |
| TASK-058 | Stdin mode (`atc-mdv -`). The host reads stdin to EOF before opening the window, with a 16 MB cap, and the tab title becomes "stdin". | | |

#### Verification

- **VER-012**: `pnpm test:e2e` passes TEST-007 to TEST-011.
- **VER-013**: `docs/smoke-test.md` (written in this phase) is run once on Windows 11 and once on Windows 10, and every item is ticked with a date.

#### Phase Summary

_(Write when the phase completes.)_

### Implementation Phase 6: Editor and source-preview sync (one task per PR)

Status: Not started

- GOAL-006: Editing with exact two-way sync (SYNC-001 to SYNC-008, EDT-001 to EDT-005) and data safety (SAFE-001 to SAFE-004).

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-070 | `src/frontend/src/features/editor.ts`, lazy-loaded (PAT-001): a CodeMirror 6 `EditorView` with `@codemirror/lang-markdown` (GFM base), `@codemirror/language-data`, history, search and line wrapping, themed from the ARC-012 tokens. | | |
| TASK-071 | Modes (REQ-013) in `shell/modes.ts`. Switching modes keeps the scroll anchor. | | |
| TASK-072 | The edit loop (ARC-007): debounced `render`, version handling, and `view/patch.ts` block patching. | | |
| TASK-073 | Implement `src/frontend/src/sync/offsets.ts`. `pointToSource(node, offset)` walks up to the nearest element with `data-sp`. For an exact text run it returns `sp + offset`; for `data-x="0"` it returns `sp`. `rangeToSource(range)` returns raw `{ start, end }`. `sourceToElement(offset)` binary-searches a sorted array of block `data-sp` values built after each insert or patch. | | |
| TASK-074 | Implement `src/frontend/src/sync/selection.ts` (SYNC-002 to SYNC-005 and SYNC-007).<br>• Track `lastFocusedPane`.<br>• Handle `copy` on the preview, and Ctrl+Shift+C: `expandRange` on the host, then the source substring is written through `clipboardWrite`.<br>• On a preview `selectionchange`, add the CodeMirror decoration `mdv-mirror-selection` and scroll it into view.<br>• On an editor selection change, register a `CSS.highlights` entry `mdv-mirror`. | | |
| TASK-075 | Scroll sync (SYNC-006) in `sync/scroll.ts`. | | |
| TASK-076 | Preview context menu (SYNC-008) in `sync/contextmenu.ts`: a custom DOM menu, keyboard accessible. | | |
| TASK-077 | Smart typing (EDT-002) in `features/editor-commands.ts`, using `insertNewlineContinueMarkup` and `deleteMarkupBackward`. Ctrl+Shift+F calls the host `formatTable` and applies the returned edit. | | |
| TASK-078 | Paste image (EDT-003): the web view reads the `image/png` item from the `paste` event and sends `saveImage`, the host writes the file, and the web view inserts the relative link. Broken-link underlines in the editor (EDT-004) use the `mdv-broken` ranges from `rendered`. | | |
| TASK-079 | Only if TASK-018 recorded that it is needed: switch `render` to change sets (ARC-007 fallback). The host keeps the text per `docId` in `Core/Documents/DocumentBuffer.cs`. Otherwise mark this task "skipped, not needed" with the date. | | |
| TASK-080 | Atomic save (SAFE-001) in `Core/Files/AtomicFileWriter.cs`, on Ctrl+S. A dirty marker `●` is shown in the tab and the window title. | | |
| TASK-081 | Drafts (SAFE-002) in `Core/Documents/DraftStore.cs`, and the external-change banner (SAFE-003). Compare uses `@codemirror/merge`, lazy-loaded. | | |

#### Verification

- **VER-014**: `pnpm test:e2e` passes TEST-013 to TEST-017.
- **VER-015**: `dotnet test` passes TEST-025 and TEST-026.
- **VER-016**: The PERF-003 app test (TEST-019) passes on both fixtures.

#### Phase Summary

_(Write when the phase completes.)_

### Implementation Phase 7: Lazy extras (Mermaid, math, export, tabs)

Status: Not started

- GOAL-007: REQ-014 to REQ-016, without regressing PERF-001 or PERF-006.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-082 | `features/mermaid.ts`: when `HasMermaid` is set, dynamically `import('mermaid')`, render the `div.mermaid` elements emitted by `UseDiagrams` to SVG with `securityLevel: 'strict'` and the app theme, and keep `data-sp` and `data-ep` on the container. | | |
| TASK-083 | `features/katex.ts`: when `HasMath` is set, dynamically `import('katex')`, render the `.math` elements emitted by `UseMathematics`, and load the KaTeX CSS on demand. | | |
| TASK-084 | Export (REQ-015). HTML: the web view assembles the rendered DOM with inlined `markdown.css` and theme, and the host writes the file after a save dialog. PDF: the host calls `PrintToPdfAsync` after a save dialog. | | |
| TASK-085 | Tabs and session restore (REQ-016), with state in `<state-dir>\session.json`. Add `installer/atc-mdv.iss` to `extra-files` (needed in Phase 8). | | |

#### Verification

- **VER-017**: TEST-012 passes: opening `readme-5k.md` loads no `mermaid`, `katex` or `editor` chunk.
- **VER-018**: The startup benchmark still meets PERF-001.
- **VER-019**: TEST-018 passes on `readme-5k-rich.md`.

#### Phase Summary

_(Write when the phase completes.)_

### Implementation Phase 8: Packaging and Windows integration

Status: Not started

- GOAL-008: A per-user Windows installer and a portable zip that register file associations and the `atc-mdv` command.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-086 | Write `installer/atc-mdv.iss` (Inno Setup 6) with `PrivilegesRequired=lowest`, `PrivilegesRequiredOverridesAllowed=dialog`, install dir `{localappdata}\Programs\AtcMarkdownViewer`, a fixed `AppId` GUID generated once and recorded in the file, `ArchitecturesAllowed` per build, and the version line marked for release-please. | | |
| TASK-087 | Register `.md` and `.markdown` under `HKCU\Software\Classes` with the ProgID `AtcMarkdownViewer.md`, adding to `OpenWithProgids` without forcing the default handler. Add the install dir to the user `PATH` and register `App Paths\atc-mdv.exe`. | | |
| TASK-088 | Check for the WebView2 runtime (CON-003) and run the bundled `MicrosoftEdgeWebview2Setup.exe /silent /install` if it is missing. | | |
| TASK-089 | Write `scripts/publish.ps1 -Arch x64 or arm64` (`dotnet publish src/Atc.MarkdownViewer -c Release -r win-<arch>` to `artifacts/publish/win-<arch>/`) and `scripts/package.ps1 -Arch -Version`. The package script runs `ISCC.exe`, zips the portable build with an empty `portable.flag` (ARC-010), and writes `artifacts/SHA256SUMS.txt`. | | |
| TASK-090 | Add `SHA256SUMS.txt` verification instructions to the README (`Get-FileHash -Algorithm SHA256`), so users of unsigned builds can check their download. | | |
| TASK-091 | Add `assets/icon/atc-mdv.ico` (16, 24, 32, 48 and 256 px) and `assets/icon/md-file.ico`. | | |

#### Verification

- **VER-020**: `pwsh scripts/package.ps1 -Arch x64 -Version 0.0.0-local` produces `artifacts/atc-mdv-0.0.0-local-win-x64-setup.exe`, `artifacts/atc-mdv-0.0.0-local-win-x64-portable.zip` and `artifacts/SHA256SUMS.txt`.
- **VER-021**: TEST-027 passes in Windows Sandbox (`docs/sandbox/atc-mdv-test.wsb`):
  - the silent install (`/VERYSILENT /CURRENTUSER`) succeeds;
  - `atc-mdv --version` prints the version in a new shell;
  - `.md` appears under "Open with";
  - the silent uninstall removes the install dir and the PATH entry.

#### Phase Summary

_(Write when the phase completes.)_

### Implementation Phase 9: Release pipeline (release-please, GitHub Releases, winget)

Status: Not started

- GOAL-009: Merging the release-please PR publishes a complete GitHub Release with release notes, and winget picks up each new version automatically.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-092 | Add the `build-windows` job to `release-please.yml` (REL-004 step 2). | | |
| TASK-093 | [MANUAL: M20] Only after SignPath approval: add the signing steps (REL-006) for `atc-mdv.exe` before packaging and for `*-setup.exe` after it, each with `if: vars.SIGNPATH_ORGANIZATION_ID != ''`. Until then, mark this task "waiting for SignPath". | | |
| TASK-094 | Add `gh release upload ${{ needs.release-please.outputs.tag_name }} artifacts/* --clobber`. | | |
| TASK-095 | Add the `winget` job (REL-004 step 3), gated by `vars.WINGET_ENABLED == 'true'`. | | |
| TASK-096 | Merge the open `chore: release version 0.1.0` PR. Confirm that GitHub Release v0.1.0 has notes from `CHANGELOG.md` and every REL-005 asset. | | |
| TASK-097 | [MANUAL: M10 to M13] Do the first winget submission for 0.1.0, as described in `process-manual-setup-1.md` Phase 2. After the winget-pkgs PR merges, set `WINGET_ENABLED=true`. | | |
| TASK-098 | Write `docs/releasing.md` covering:<br>• how conventional commits drive the version, and that merging the release PR publishes;<br>• the required secrets and variables;<br>• re-running a failed upload;<br>• yanking a release (delete the GitHub Release and open a winget-pkgs removal PR). | | |

#### Verification

- **VER-022**: `gh release view v0.1.0 --json assets -q '.assets[].name'` lists every REL-005 asset.
- **VER-023**: In a clean Windows Sandbox, `winget install atc-net.MarkdownViewer` installs the app and `atc-mdv --version` prints `0.1.0`.
- **VER-024**: After the next release (v0.1.1), `gh pr list -R microsoft/winget-pkgs --search "atc-net.MarkdownViewer 0.1.1"` returns one PR that was opened automatically.

#### Phase Summary

_(Write when the phase completes.)_

### Implementation Phase 10: Linux (On Hold)

Status: On Hold <!-- Revive only when the owner asks for Linux (DEC-003) -->

- GOAL-010: Ubuntu 24.04 x64 builds, using a Photino.NET host that reuses `Atc.MarkdownViewer.Core`, `Atc.MarkdownViewer.Markdown` and the unchanged frontend.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-100 | Add `src/Atc.MarkdownViewer.Linux/` (Photino.NET), `src/frontend/src/bridge/photino.ts`, XDG paths and a Unix-socket single instance. | | |
| TASK-101 | Package `dotnet publish -r linux-x64 --self-contained` as an AppImage, a `.deb` and a `.tar.gz`, with a `.desktop` file that registers `text/markdown`. | | |
| TASK-102 | Add the Linux CI job (`xvfb-run` smoke test) and the release job. | | |

#### Verification

- **VER-025**: On Ubuntu 24.04, the AppImage writes a numeric `MDV_STARTUP_MS` with `--bench --bench-out /tmp/b.txt`.

#### Phase Summary

_(Write when the phase completes.)_

### Implementation Phase 11: Hardening and 1.0

Status: Not started

- GOAL-011: Stability and accessibility, ready for 1.0.

| Task | Description | Completed | Date |
|------|-------------|-----------|------|
| TASK-110 | Property-based test with `FsCheck.Xunit`. Generate 10,000 random markdown strings, including unbalanced fences, nesting to depth 200, 1 MB lines and lone surrogates. Assert that `Render` never throws, takes at most 500 ms per input, and that every emitted `data-sp`/`data-ep` pair satisfies `0 <= sp <= ep <= source.Length`. | | |
| TASK-111 | Large file: a 20 MB generated file reaches first paint within PERF-001 plus 100 ms. Scrolling keeps a p95 frame time of at most 33 ms, measured by `PerformanceObserver` in an app test. | | |
| TASK-112 | Kill test (TEST-028, SAFE-004). | | |
| TASK-113 | Accessibility: run `Deque.AxeCore.Playwright` in app tests on View, Split and Edit modes, and fix every `serious` or `critical` issue. | | |
| TASK-114 | Cut 1.0: add `"release-as": "1.0.0"` to the `"."` package in `release-please-config.json` and merge the resulting release PR, then remove `release-as` in a follow-up `chore:` PR. | | |

#### Verification

- **VER-026**: `dotnet test`, `pnpm test` and `pnpm test:e2e` all pass, including TEST-028 to TEST-030.
- **VER-027**: The v1.0.0 GitHub Release exists, and `winget upgrade atc-net.MarkdownViewer` moves a 0.x install to 1.0.0.

#### Phase Summary

_(Write when the phase completes.)_

## 3. Alternatives

- **ALT-001**: Electron. Rejected by CON-001.
- **ALT-002**: Native rendering without a web view (Markdig AST to WinUI or WPF controls). Rejected for three reasons: GitHub-level fidelity for tables, alerts, Mermaid, math and raw HTML would need a large custom layout engine; exact character-level selection mapping across native text controls is much harder than with DOM Ranges; and CodeMirror-class editing has no native equivalent.
- **ALT-003**: WinForms as the window. Rejected by the owner as outdated. WPF is not planned: the owner prefers WinUI 3, and WPF is built only as a comparison if DEC-001's fallback triggers.
- **ALT-004**: Parsing in TypeScript (micromark and mdast) inside the web view. This was the previous plan. Rejected by DEC-002: the owner wants the core logic in C#. A C# parse can also run in parallel with WebView2 startup (ARC-006), which a parse in the web view cannot.
- **ALT-005**: A native editor (AvalonEdit in WPF) next to a WebView2 preview. Rejected because the selection, scroll and cursor sync would cross the native-to-web boundary on every event, and CodeMirror's markdown editing is far richer.
- **ALT-006**: MSIX packaging. Rejected for v1: it needs a trusted signature for sideloading, and Inno Setup is directly supported by winget and wingetcreate.
- **ALT-007**: Shiki for syntax highlighting. Rejected because its WASM Oniguruma engine adds startup cost. Lezer grammars are shared with the editor.
- **ALT-008**: Photino.NET host. Deferred for Linux (Phase 10).
- **ALT-009**: Rust with Tauri v2. Rejected: the owner maintains .NET, and on Windows it would use the same WebView2 engine, so rendering speed would not differ.
- **ALT-010**: Scoop distribution. Dropped by the owner; winget is enough.
- **ALT-011**: Paid code signing (Azure Artifact Signing at about USD 10 per month, or a CA certificate). Rejected: Artifact Signing needs a business with 3 or more years of tax history, individuals qualify only in the US and Canada, and the owner does not want to pay.
- **ALT-013**: Microsoft.UI.Reactor, a C#-first MVU way to build WinUI UI, announced on the Windows developer blog. Not adopted: it is experimental (0.1.0-preview, no support policy or API guarantees), and the native UI here is one window hosting WebView2, already written in C# with no XAML views. Worth revisiting only if native UI grows, for example a native settings window.
- **ALT-012**: Requiring squash merges so that PR titles become the commit messages. Rejected by the owner; any merge method is allowed (REL-001).

## 4. Dependencies

- **DEP-001**: .NET 10 SDK (LTS).
- **DEP-002**: NuGet packages, latest stable at implementation time, with versions in each project file:
  - `Markdig`, `HtmlSanitizer`, `System.IO.Hashing`, `System.CommandLine`;
  - `Microsoft.WindowsAppSDK` (which includes the WinUI 3 `WebView2` control) and `Microsoft.Web.WebView2`;
  - tests and benchmarks: `xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`, `FsCheck.Xunit`, `Microsoft.Playwright`, `Deque.AxeCore.Playwright`, `BenchmarkDotNet`.
- **DEP-003**: npm packages (build time only): `vite`, `typescript`, `vitest`, `@playwright/test`, `eslint`, `typescript-eslint`, `@codemirror/state`, `@codemirror/view`, `@codemirror/lang-markdown`, `@codemirror/language-data`, `@codemirror/merge`, `@lezer/highlight`, `mermaid`, `katex`, and `dompurify` only if TASK-016 moves sanitizing to the frontend.
- **DEP-004**: Microsoft Edge WebView2 Runtime (Evergreen). The Windows App SDK is bundled, so it is not a runtime dependency.
- **DEP-005**: Inno Setup 6 on build machines and runners (`choco install innosetup -y` if the runner image lacks it).
- **DEP-006**: GitHub Actions, all pinned by SHA: `googleapis/release-please-action` v5.0.0, `actions/checkout`, `actions/setup-dotnet`, `actions/setup-node`, `vedantmgoyal9/winget-releaser`, and SignPath's action once approved.
- **DEP-007**: The manual setup in `plans/process-manual-setup-1.md`: org policies, `RELEASE_PLEASE_PAT`, `WINGET_TOKEN`, the winget-pkgs fork and the optional SignPath application.

## 5. Files

- **FILE-001**: `plans/feature-markdown-viewer-1.md`: this plan. `plans/process-manual-setup-1.md`: the one-time manual setup.
- **FILE-002**: `docs/adr/0001-native-window.md` and `docs/adr/0001-native-window-results/*.json`: the window decision and its evidence.
- **FILE-003**: At the repository root: `Atc.MarkdownViewer.slnx`, `Directory.Build.props`, `global.json`, `.gitattributes`, `.editorconfig`, `atc-coding-rules-updater.json` and `atc-coding-rules-updater.ps1`. The `src/` and `test/` folders get their own `Directory.Build.props` from the updater.
- **FILE-004**: `src/Atc.MarkdownViewer/`: the app, covering the window, `Bridge/`, `Bridge/Handlers/` and `WebView/`.
- **FILE-005**: `src/Atc.MarkdownViewer.Core/`: `Cli/`, `Files/`, `Documents/`, `Ipc/`, `Search/`, `Security/`, `Settings/`, `Logging/` and `Resources/`.
- **FILE-006**: `src/Atc.MarkdownViewer.Markdown/`: `MarkdownPipelineFactory`, `SourceMappedHtmlRenderer` and its renderers, `MarkdownRenderService`, `LinkRewriter`, `SourceRangeExpander` and `TableFormatter`.
- **FILE-007**: `src/Atc.MarkdownViewer.FixtureRenderer/`: the console tool that produces the web view's test fixtures.
- **FILE-008**: `src/frontend/`: `src/bridge/`, `src/view/`, `src/sync/`, `src/features/`, `src/shell/`, `src/find/`, `src/folder/`, `src/nav/`, `src/commands/`, `src/styles/`, `src/strings.ts` and `test/`.
- **FILE-009**: `test/Atc.MarkdownViewer.Core.Tests/`, `test/Atc.MarkdownViewer.Markdown.Tests/` and `test/Atc.MarkdownViewer.AppTests/`.
- **FILE-010**: `benchmarks/generate-fixtures.mjs`, `benchmarks/fixtures/*.md`, `benchmarks/measure.ps1` and `benchmarks/Atc.MarkdownViewer.Benchmarks/`.
- **FILE-011**: `installer/atc-mdv.iss`, `scripts/publish.ps1`, `scripts/package.ps1`, `scripts/sign.ps1` and `assets/icon/*`.
- **FILE-012**: `.github/workflows/build.yml` and `.github/workflows/release-please.yml`.
- **FILE-013**: `release-please-config.json`, `.release-please-manifest.json` and `CHANGELOG.md` (generated).
- **FILE-014**: `README.md`, `docs/releasing.md`, `docs/smoke-test.md` and `docs/sandbox/*`.
- **FILE-015**: `spikes/`: the Phase 2 spikes. They are kept for re-measuring and never shipped.

## 6. Testing

- **TEST-001**: Renderer snapshot tests: every fixture in `Fixtures/gfm/` matches its `Snapshots/*.html`. One test per ARC-003 extension pins its output.
- **TEST-002**: Conformance: with every `data-*` attribute and every added `<span>` wrapper stripped, `SourceMappedHtmlRenderer` output equals Markdig's default `HtmlRenderer` output for every fixture and for all GFM spec examples (`spec.txt` from the GFM repository, embedded as a test resource).
- **TEST-003**: Source-map round trip: for every element with `data-sp`/`data-ep` that is not `data-x="0"` and contains only a text node, `source[sp..ep] == element.TextContent`. Runs over every fixture, including `crlf-tabs.md` and `readme-5k-crlf.md`.
- **TEST-004**: `SourceRangeExpander`:
  - selecting "Map consumers" inside `- [x] Map consumers to owners` returns the substring offsets unchanged;
  - a range starting inside `**bold**` expands to include both `**` markers;
  - a range spanning two paragraphs expands to both whole blocks.
- **TEST-005**: Sanitizer: `<img src=x onerror=alert(1)>`, `<script>` and `javascript:` hrefs are removed, and `<details>`, `<kbd>` and every `data-sp` attribute survive.
- **TEST-007**: Live reload (Playwright and `MockBridge`): after scrolling to block 300 and receiving `fileChanged`, the top visible block has the same `data-sp`.
- **TEST-008**: Link navigation and Alt+Left back across multi-file fixtures.
- **TEST-009**: Find highlights the correct match count, and Enter cycles through the matches.
- **TEST-010**: `FuzzyMatcher` (xUnit) ranks `docs/architecture.md` first for `arch`.
- **TEST-011**: Rich copy: the `clipboardWrite` payload's `html` contains `<table>` and inline styles for a table selection.
- **TEST-012**: Lazy loading: for `readme-5k.md` in View mode, the network log has no request containing `mermaid`, `katex` or `editor`.
- **TEST-013**: Focus-correct copy (SYNC-002): with editor selection "alpha", preview selection "beta" and the preview focused, Ctrl+C puts "beta" on the clipboard.
- **TEST-014**: Copy as Markdown (SYNC-003): selecting the whole rendered task item yields `- [x] Map consumers to owners`.
- **TEST-015**: Mirror selection (SYNC-004): selecting a preview word decorates the exact matching editor range.
- **TEST-016**: Double-click to source (SYNC-005): the editor cursor offset equals the clicked character's source offset.
- **TEST-017**: Scroll sync (SYNC-006): after scrolling the preview to a heading, the editor's top visible line equals that heading's line, within one line either way.
- **TEST-018**: Mermaid and KaTeX produce SVG and `.katex` elements on `readme-5k-rich.md`.
- **TEST-019**: App test for PERF-003: type 50 characters into `readme-500.md` and `readme-5k.md`, measuring from keydown to the patched DOM through `performance.mark`.
- **TEST-020**: App launch test (VER-009).
- **TEST-021**: CLI parsing for every REQ-012 flag and exit code.
- **TEST-022**: Encoding and EOL detection for UTF-8, UTF-8 BOM, UTF-16 LE and BE BOM, Windows-1252, LF, CRLF and mixed.
- **TEST-023**: Watcher: five writes within 50 ms produce one `fileChanged`, and an atomic-rename save produces one `fileChanged`.
- **TEST-024**: Single instance: a second process forwards its argv and exits 0 within 200 ms.
- **TEST-025**: Atomic save keeps encoding, BOM and EOL, and a simulated failure after the temp write leaves the original unchanged.
- **TEST-026**: Drafts: a draft is written within 2.5 s, restore returns its text, and discard deletes it.
- **TEST-027**: Install and uninstall in Windows Sandbox (VER-021), scripted in `docs/sandbox/install-test.ps1`.
- **TEST-028**: Kill test: type 500 characters, wait 2.5 s, kill the process tree, relaunch, and the draft banner offers exactly the typed text.
- **TEST-029**: Property-based renderer test (TASK-110).
- **TEST-030**: Large-file test (TASK-111).
- **TEST-031**: Bridge contract parity: a C# test serializes one sample of every `Messages.cs` record to `src/frontend/test/contract-samples.json`, and a Vitest test type-checks each sample against `contract.ts`.

## 7. Risks & Assumptions

- **RISK-001**: WebView2 dominates cold start (measured: about 80% of 469 ms), and the first launch after boot is 1.0–1.3 s. Mitigations: resident mode (PERF-002), the ARC-006 overlap, and the PERF-007 history to catch regressions.
- **RISK-002**: Microsoft's docs note that self-contained, unpackaged WinUI 3 apps load slower and use more memory. Measured: no startup difference against the shared runtime (469 vs 468 ms).
- **RISK-003**: The host process uses 127 MB at first paint, against PERF-004's 100 MB. Mitigation: free the rendered HTML after posting it, and track the number in the PERF-007 history.
- **RISK-004**: Large HTML strings over `PostWebMessageAsJson` on every keystroke may exceed PERF-003 for 5,000-line files. The ARC-007 change-set fallback (TASK-079) and block-level patching cover this.
- **RISK-005**: Wrapping text runs in `<span>`s adds DOM nodes. MD-003 skips the wrapper for sole exact runs, and TASK-017 measures insertion time.
- **RISK-006**: A wrong source offset breaks the core promise. TEST-002 and TEST-003, run over every fixture plus the GFM spec, guard against this.
- **RISK-007**: Unsigned releases show a SmartScreen warning for browser downloads, and some company machines block unsigned apps outright. Mitigations: the README explains "Run anyway" and the checksum check, and SignPath Foundation is the free route to signing. Signing would not remove the warning instantly anyway, because reputation builds over weeks of installs.
- **RISK-008**: The new GitHub org has no secrets or policies yet. Nothing on GitHub runs until the owner does the manual setup. Until then all work and verification is local.
- **ASSUMPTION-001**: The command is `atc-mdv`. The owner suggested it, and the winget id `atc-net.MarkdownViewer` and product name "ATC Markdown Viewer" were confirmed on 2026-10-23.
- **ASSUMPTION-002**: The repository is `github.com/atc-net-apps/atc-soft-markdown-viewer`, licensed MIT (the existing `LICENSE`).
- **ASSUMPTION-003**: `atc-soft-json-viewer` is a separate browser extension (Edge, Chrome, Firefox) built by another agent, sharing no code or pipeline with this project.

## 8. Related Specifications / Further Reading

- [Manual setup plan](process-manual-setup-1.md)
- [GitHub Flavored Markdown spec](https://github.github.com/gfm/)
- [GitHub alerts syntax](https://docs.github.com/en/get-started/writing-on-github/getting-started-with-writing-and-formatting-on-github/basic-writing-and-formatting-syntax#alerts)
- [Markdig](https://github.com/xoofx/markdig): see the docs for the AST and source spans, renderers, and the alert-blocks extension.
- [HtmlSanitizer](https://github.com/mganss/HtmlSanitizer)
- [WebView2 documentation](https://learn.microsoft.com/microsoft-edge/webview2/)
- [Distribute an unpackaged WinUI 3 app](https://learn.microsoft.com/windows/apps/package-and-deploy/unpackage-winui-app)
- [CodeMirror 6 reference](https://codemirror.net/docs/ref/)
- [release-please](https://github.com/googleapis/release-please) and the atc-net reference config in `atc-net/atc-coding-rules-updater`
- [winget-releaser](https://github.com/vedantmgoyal9/winget-releaser) and [wingetcreate](https://github.com/microsoft/winget-create)
- Concept page: https://claude.ai/artifact/EFqiH6xjEv9Gc2RNqxKxA1

## 9. Final Recap

_(Write when all phases complete: a summary of the entire piece of work.)_

## 10. Deployment Plan

_(Write when all phases complete: step-by-step deployment instructions.)_
