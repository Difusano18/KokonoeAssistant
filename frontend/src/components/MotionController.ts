export class MotionController {
  private readonly shell = document.querySelector(".shell")!;
  private readonly runtime = document.getElementById("runtime")!;
  private readonly dot = document.getElementById("runtime-dot")!;
  private readonly footer = document.getElementById("footer-state")!;
  private hostErrorResetHandle = 0;

  constructor() {
    window.koko.on("chat.started", () => { window.clearTimeout(this.hostErrorResetHandle); this.setHostState("busy"); });
    window.koko.on("chat.completed", () => { window.clearTimeout(this.hostErrorResetHandle); this.setHostState("ready"); });
    window.koko.on("chat.canceled", () => { window.clearTimeout(this.hostErrorResetHandle); this.setHostState("ready"); });
    window.koko.on("chat.error", () => {
      this.setHostState("error");
      // A fast retry within this window already clears the timeout via the
      // chat.started handler above — guards against this stale reset firing
      // "ready" over a state that's moved on to busy/error again by then.
      window.clearTimeout(this.hostErrorResetHandle);
      this.hostErrorResetHandle = window.setTimeout(() => this.setHostState("ready"), 5000);
    });
    window.koko.on("agent.activity", () => this.flash(document.getElementById("agent-activity")));
    window.koko.on("agent.completed", () => {
      this.flash(document.getElementById("agent-tasks"));
      this.flash(document.getElementById("tasks-list"));
    });
    window.koko.on("vault.status", () => {
      this.flash(document.getElementById("vault-panel"));
      this.flash(document.getElementById("panel-memory"));
    });
    window.koko.on("telegram.status", () => this.flash(document.getElementById("telegram-panel")));

    const messages = document.getElementById("messages");
    if (messages) {
      new MutationObserver(records => {
        for (const record of records) {
          for (const node of record.addedNodes) {
            if (node instanceof HTMLElement && node.classList.contains("message")) {
              node.classList.add("entering");
              node.addEventListener("animationend", () => node.classList.remove("entering"), { once: true });
            }
          }
        }
      }).observe(messages, { childList: true });
    }
  }

  setHostState(state: "ready" | "busy" | "error" | "preview"): void {
    const labels = {
      ready: ["Runtime linked", "● HOST READY"], busy: ["Kokonoe is responding", "● STREAMING"],
      error: ["Host error", "● HOST ERROR"], preview: ["Static preview", "● HOST ONLY"]
    } as const;
    this.runtime.textContent = labels[state][0];
    this.footer.textContent = labels[state][1];
    this.dot.className = `runtime-dot ${state === "preview" ? "" : state}`;
    this.shell.classList.toggle("is-busy", state === "busy");
  }

  private flash(element: HTMLElement | null): void {
    if (!element) return;
    element.classList.remove("event-flash");
    requestAnimationFrame(() => element.classList.add("event-flash"));
    window.setTimeout(() => element.classList.remove("event-flash"), 480);
  }
}
