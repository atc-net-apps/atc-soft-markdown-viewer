// Spike viewer page (plan ARC-006 step 6): insert the first chunk, wait two frames, report first content paint,
// then append the remaining chunks when idle.
const doc = document.getElementById("doc");
const post = (msg) => window.chrome.webview.postMessage(msg);

window.chrome.webview.addEventListener("message", (e) => {
  const msg = e.data;
  if (msg.type === "open") {
    const t0 = performance.now();
    doc.insertAdjacentHTML("beforeend", msg.html);
    const insertMs = performance.now() - t0;
    requestAnimationFrame(() =>
      requestAnimationFrame(() => post({ type: "fcp", insertMs, pageMs: performance.now() })),
    );
  } else if (msg.type === "chunk") {
    requestIdleCallback(() => doc.insertAdjacentHTML("beforeend", msg.html));
  }
});

// --inline-first: the host served the first chunk inside the page itself.
const inline = doc.dataset.inline === "1";
if (inline) {
  requestAnimationFrame(() => requestAnimationFrame(() => post({ type: "fcp", insertMs: 0, pageMs: performance.now() })));
}

post({ type: "ready", inline });
