(function () {
  "use strict";
  const shell = document.querySelector(".shell");
  const runtime = document.getElementById("runtime");
  const dot = document.getElementById("runtime-dot");
  const footer = document.getElementById("footer-state");
  const messageRoot = document.getElementById("messages");

  function setHostState(state) {
    const labels = {
      ready: ["Runtime linked", "● HOST READY"],
      busy: ["Kokonoe is responding", "● STREAMING"],
      error: ["Host error", "● HOST ERROR"],
      preview: ["Static preview", "● HOST ONLY"]
    };
    const value = labels[state] || labels.preview;
    runtime.textContent = value[0];
    footer.textContent = value[1];
    dot.className = "runtime-dot " + (state === "preview" ? "" : state);
    footer.className = state === "error" ? "telegram-error" : state === "busy" ? "pending" : "ready";
    shell.classList.toggle("is-busy", state === "busy");
  }

  function flash(element) {
    if (!element) return;
    element.classList.remove("live-update");
    void element.offsetWidth;
    element.classList.add("live-update");
    element.addEventListener("animationend", () => element.classList.remove("live-update"), { once: true });
  }

  const observer = new MutationObserver(records => {
    for (const record of records) {
      for (const node of record.addedNodes) {
        if (node instanceof HTMLElement && node.classList.contains("message")) {
          node.classList.add("entering");
          node.addEventListener("animationend", () => node.classList.remove("entering"), { once: true });
        }
      }
    }
  });
  observer.observe(messageRoot, { childList: true });

  window.koko.on("chat.started", () => setHostState("busy"));
  window.koko.on("chat.completed", () => setHostState("ready"));
  window.koko.on("chat.canceled", () => setHostState("ready"));
  window.koko.on("chat.error", () => setHostState("error"));
  window.koko.on("agent.activity", () => flash(document.getElementById("agent-activity")));
  window.koko.on("agent.completed", () => flash(document.getElementById("agent-tasks")));
  window.koko.on("vault.status", () => flash(document.getElementById("vault-panel")));
  window.koko.on("telegram.status", () => flash(document.getElementById("telegram-panel")));

  window.kokoMotion = { setHostState, flash };
})();
