interface DevEntry {
  kind: "llm_request" | "llm_response" | "tool_call" | string;
  label: string;
  content: string;
  at: string;
}

interface DevSnapshot {
  entries: DevEntry[];
}

const KIND_LABEL: Record<string, string> = {
  llm_request: "req",
  llm_response: "res",
  tool_call: "tool",
};

export class DevPanelController {
  private loaded = false;

  constructor(
    private readonly feed: HTMLElement,
    private readonly clearBtn: HTMLButtonElement
  ) {
    window.koko.on("dev.entry", payload => this.append(payload as DevEntry));
    this.clearBtn.addEventListener("click", () => {
      this.feed.innerHTML = "";
    });
  }

  // Snapshot pull is deferred until the tab is actually opened once, not on app start -
  // this tab is debug-only and most sessions will never click it.
  async ensureLoaded(): Promise<void> {
    if (this.loaded) return;
    this.loaded = true;
    try {
      const snapshot = await window.koko.call<DevSnapshot>("dev.snapshot", {}, 10000);
      for (const entry of snapshot.entries) this.append(entry, /*prepend*/ false);
    } catch (error) {
      console.error("[dev panel] snapshot failed", error);
    }
  }

  private append(entry: DevEntry, prepend = true): void {
    const row = document.createElement("div");
    row.className = "dev-entry";

    const head = document.createElement("div");
    head.className = "dev-entry-head";

    const kind = document.createElement("span");
    kind.className = `dev-kind ${entry.kind}`;
    kind.textContent = KIND_LABEL[entry.kind] ?? entry.kind;

    const time = document.createElement("span");
    time.className = "dev-entry-time";
    time.textContent = entry.at;

    const label = document.createElement("span");
    label.className = "dev-entry-label";
    label.textContent = entry.label;

    head.append(kind, time, label);

    const content = document.createElement("pre");
    content.className = "dev-entry-content";
    content.textContent = entry.content || "(empty)";

    head.addEventListener("click", () => row.classList.toggle("open"));

    row.append(head, content);
    if (prepend) this.feed.prepend(row);
    else this.feed.append(row);

    // Keep the DOM bounded - KokoDevLogBus already caps its own buffer, this just mirrors
    // that limit client-side so a long session doesn't keep growing the panel forever.
    while (this.feed.children.length > 300) this.feed.removeChild(this.feed.lastChild!);
  }
}
