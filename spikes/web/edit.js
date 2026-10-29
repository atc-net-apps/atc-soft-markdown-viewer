// Spike edit page (plan ARC-007, worst case): every edit sends the full text to the host, which re-renders
// everything; the preview is replaced in full. Measures edit -> painted preview, excluding the debounce.
// A textarea stands in for CodeMirror: the editor's own cost is not part of this round trip.
const src = document.getElementById("src");
const doc = document.getElementById("doc");
const post = (msg) => window.chrome.webview.postMessage(msg);

const EDITS = 50;
let version = 0;
let sentAt = 0;
let pending = null;
const samples = [];

function sendEdit() {
  const mid = Math.floor(src.value.length / 2);
  src.value = src.value.slice(0, mid) + "x" + src.value.slice(mid);
  version++;
  sentAt = performance.now();
  post({ type: "render", version, text: src.value });
}

window.chrome.webview.addEventListener("message", (e) => {
  const msg = e.data;
  if (msg.type === "open") {
    src.value = msg.text;
    doc.innerHTML = msg.html;
    setTimeout(sendEdit, 200);
  } else if (msg.type === "rendered" && msg.version === version) {
    doc.innerHTML = msg.html;
    requestAnimationFrame(() => {
      samples.push(performance.now() - sentAt);
      if (samples.length < EDITS) {
        setTimeout(sendEdit, 10);
      } else {
        const sorted = [...samples].sort((a, b) => a - b);
        const at = (q) => sorted[Math.min(sorted.length - 1, Math.ceil(q * sorted.length) - 1)];
        post({ type: "editStats", median: at(0.5), p90: at(0.9), min: sorted[0], max: sorted[sorted.length - 1] });
      }
    });
  }
});

post({ type: "ready" });
