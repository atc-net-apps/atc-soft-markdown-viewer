// Generates the benchmark fixtures in benchmarks/fixtures/ deterministically (fixed seed, seeded PRNG).
// Usage: node benchmarks/generate-fixtures.mjs
import { mkdirSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const SEED = 20261023;
const outDir = join(dirname(fileURLToPath(import.meta.url)), "fixtures");

function mulberry32(seed) {
  let a = seed >>> 0;
  return () => {
    a = (a + 0x6d2b79f5) >>> 0;
    let t = a;
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

const WORDS = (
  "markdown viewer editor render source preview queue consumer service owner deploy plan phase " +
  "inventory cutover rollback window stable fast build test release config parser table column " +
  "header footer anchor link image alert note tip warning caution important task list item code " +
  "block inline emphasis strong heading outline folder search palette command shortcut scroll sync"
).split(" ");

const LANGS = ["csharp", "typescript", "json", "powershell", "bash", "yaml", "xml", "sql"];

const CODE = {
  csharp: ["public sealed class Renderer", "{", "    public string Render(string source) => Markdown.ToHtml(source);", "}"],
  typescript: ["export function insert(html: string): void {", "  const root = document.getElementById('doc');", "  root?.insertAdjacentHTML('beforeend', html);", "}"],
  json: ["{", "  \"theme\": \"system\",", "  \"readingWidth\": 860,", "  \"keepResident\": false", "}"],
  powershell: ["$files = Get-ChildItem -Recurse -Filter *.md", "foreach ($f in $files) {", "    atc-mdv $f.FullName", "}"],
  bash: ["#!/usr/bin/env bash", "set -euo pipefail", "for f in docs/*.md; do", "  echo \"$f\"", "done"],
  yaml: ["name: build", "on:", "  push:", "    branches: [main]", "jobs:", "  build:", "    runs-on: windows-latest"],
  xml: ["<Project Sdk=\"Microsoft.NET.Sdk\">", "  <PropertyGroup>", "    <TargetFramework>net10.0</TargetFramework>", "  </PropertyGroup>", "</Project>"],
  sql: ["SELECT o.Id, o.Name, COUNT(*) AS Consumers", "FROM Owners o", "JOIN Consumers c ON c.OwnerId = o.Id", "GROUP BY o.Id, o.Name;"],
};

function makeGen(seed) {
  const rnd = mulberry32(seed);
  const int = (min, max) => min + Math.floor(rnd() * (max - min + 1));
  const pick = (arr) => arr[Math.floor(rnd() * arr.length)];
  const words = (n) => Array.from({ length: n }, () => pick(WORDS)).join(" ");
  const sentence = () => {
    const w = words(int(6, 16));
    return w.charAt(0).toUpperCase() + w.slice(1) + ".";
  };
  const decorate = (s) => {
    const r = rnd();
    if (r < 0.15) return s.replace(/(\w+) (\w+)/, "**$1 $2**");
    if (r < 0.25) return s.replace(/(\w+)/, "*$1*");
    if (r < 0.35) return s.replace(/(\w+)\./, "[$1](https://example.com/$1).");
    if (r < 0.45) return s.replace(/(\w+) /, "`$1` ");
    if (r < 0.5) return s.replace(/(\w+)\./, "[$1](./docs/$1.md).");
    if (r < 0.55) return s.replace(/(\w+) /, "~~$1~~ ");
    return s;
  };
  const paragraph = () => Array.from({ length: int(2, 5) }, () => decorate(sentence())).join(" ");
  return { rnd, int, pick, words, sentence, paragraph };
}

function table(g) {
  const cols = g.int(3, 5);
  const rows = g.int(3, 8);
  const head = Array.from({ length: cols }, () => g.pick(WORDS));
  const align = Array.from({ length: cols }, (_, i) => (i === 0 ? ":---" : g.pick(["---", ":---:", "---:"])));
  const lines = [`| ${head.join(" | ")} |`, `| ${align.join(" | ")} |`];
  for (let r = 0; r < rows; r++) {
    lines.push(`| ${Array.from({ length: cols }, () => g.words(g.int(1, 3))).join(" | ")} |`);
  }
  return lines;
}

function fence(g) {
  const lang = g.pick(LANGS);
  return ["```" + lang, ...CODE[lang], "```"];
}

function list(g, kind, indent) {
  const n = g.int(3, 7);
  const lines = [];
  for (let i = 0; i < n; i++) {
    const marker = kind === "ordered" ? `${i + 1}.` : kind === "task" ? (g.rnd() < 0.5 ? "- [x]" : "- [ ]") : "-";
    lines.push(`${marker} ${g.sentence()}`);
    if (kind === "bullet" && g.rnd() < 0.25) {
      lines.push(`${indent}- ${g.sentence()}`);
      lines.push(`${indent}- ${g.sentence()}`);
    }
  }
  return lines;
}

function alert(g) {
  const kind = g.pick(["NOTE", "TIP", "IMPORTANT", "WARNING", "CAUTION"]);
  return [`> [!${kind}]`, `> ${g.sentence()}`, `> ${g.sentence()}`];
}

function image(g) {
  return g.rnd() < 0.5
    ? [`![${g.pick(WORDS)} diagram](./images/${g.pick(WORDS)}.png)`]
    : [`![${g.pick(WORDS)}](https://example.com/img/${g.pick(WORDS)}.png)`];
}

function mermaid(g) {
  const a = g.pick(WORDS);
  const b = g.pick(WORDS);
  return ["```mermaid", "flowchart LR", `  A[${a}] --> B[${b}]`, `  B --> C{${g.pick(WORDS)}?}`, "  C -->|yes| D[done]", "  C -->|no| A", "```"];
}

function math(g) {
  return g.rnd() < 0.5
    ? ["$$", `\\sum_{i=1}^{${g.int(2, 99)}} x_i^2 = \\frac{${g.int(1, 9)}}{${g.int(2, 9)}}`, "$$"]
    : [`The cost is $O(n \\log n)$ for ${g.words(4)} and $k = ${g.int(1, 50)}$.`];
}

const HTML_SNIPPETS = [
  ["<details>", "<summary>More details</summary>", "", "Hidden content with **markdown** inside.", "", "</details>"],
  ["Press <kbd>Ctrl</kbd>+<kbd>K</kbd> to open the palette."],
  ["Water is H<sub>2</sub>O and area is m<sup>2</sup>."],
  ["<img src=\"x\" onerror=\"alert(1)\">"],
  ["<a href=\"javascript:alert(1)\">bad link</a>"],
  ["<div align=\"center\"><b>centered</b></div>"],
  ["<script>alert('x')</script>"],
  ["<picture><source srcset=\"a.webp\"><img src=\"a.png\" alt=\"a\"></picture>"],
];

function generate({ lines: target, seed, tables, fences, rich, crlf }) {
  const g = makeGen(seed);
  const out = ["---", "title: Benchmark fixture", "owner: atc-net-apps", `lines: ${target}`, "---", "", "# Benchmark fixture", "", g.paragraph(), ""];
  // Spread the fixed-count blocks evenly by line position.
  const schedule = [];
  for (let i = 0; i < tables; i++) schedule.push({ at: ((i + 0.5) / tables) * target, kind: "table" });
  for (let i = 0; i < fences; i++) schedule.push({ at: ((i + 0.3) / fences) * target, kind: "fence" });
  if (rich) {
    for (let i = 0; i < 10; i++) schedule.push({ at: ((i + 0.6) / 10) * target, kind: "mermaid" });
    for (let i = 0; i < 30; i++) schedule.push({ at: ((i + 0.4) / 30) * target, kind: "math" });
    for (let i = 0; i < 40; i++) schedule.push({ at: ((i + 0.7) / 40) * target, kind: "html", idx: i });
  }
  schedule.sort((a, b) => a.at - b.at);
  let next = 0;
  let heading = 0;
  const indent = crlf ? "\t" : "  ";
  const push = (block) => out.push(...block, "");

  while (out.length < target) {
    while (next < schedule.length && schedule[next].at <= out.length) {
      const s = schedule[next++];
      if (s.kind === "table") push(table(g));
      else if (s.kind === "fence") push(fence(g));
      else if (s.kind === "mermaid") push(mermaid(g));
      else if (s.kind === "math") push(math(g));
      else push(HTML_SNIPPETS[s.idx % HTML_SNIPPETS.length]);
    }
    const r = g.rnd();
    if (r < 0.1) {
      heading++;
      push([`${"#".repeat(g.int(2, 4))} Section ${heading} ${g.words(g.int(1, 3))}`]);
    } else if (r < 0.45) push([g.paragraph()]);
    else if (r < 0.6) push(list(g, "bullet", indent));
    else if (r < 0.68) push(list(g, "ordered", indent));
    else if (r < 0.76) push(list(g, "task", indent));
    else if (r < 0.82) push(alert(g));
    else if (r < 0.87) push(image(g));
    else push([g.paragraph()]);
  }
  out.length = target;
  return out.join(crlf ? "\r\n" : "\n") + (crlf ? "\r\n" : "\n");
}

mkdirSync(outDir, { recursive: true });
const fixtures = {
  "readme-500.md": { lines: 500, seed: SEED, tables: 2, fences: 6 },
  "readme-5k.md": { lines: 5000, seed: SEED, tables: 20, fences: 60 },
  "readme-5k-rich.md": { lines: 5000, seed: SEED, tables: 20, fences: 60, rich: true },
  "readme-5k-crlf.md": { lines: 5000, seed: SEED, tables: 20, fences: 60, crlf: true },
};
for (const [name, opts] of Object.entries(fixtures)) {
  const text = generate(opts);
  writeFileSync(join(outDir, name), text, "utf8");
  console.log(`${name}: ${text.split(/\r?\n/).length - 1} lines, ${text.length} chars`);
}
