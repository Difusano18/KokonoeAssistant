interface VaultRecentNote { path: string; modifiedAt: string; }
interface VaultStatus {
  available: boolean;
  path: string;
  noteCount: number;
  folderCount: number;
  recentNotes: VaultRecentNote[];
  scannedAt: string;
  scanMs: number;
}

export class VaultPanelController {
  private readonly state = document.getElementById("vault-state")!;
  private readonly notes = document.getElementById("vault-notes")!;
  private readonly folders = document.getElementById("vault-folders")!;
  private readonly path = document.getElementById("vault-path")!;
  private readonly recent = document.getElementById("vault-recent")!;
  private readonly meta = document.getElementById("vault-meta")!;
  private readonly refresh = document.getElementById("vault-refresh") as HTMLButtonElement;

  constructor() {
    window.koko.on("vault.status", payload => this.render(payload as VaultStatus));
    this.refresh.addEventListener("click", () => void this.reload());
  }

  async connect(): Promise<void> {
    this.refresh.disabled = false;
    await this.load("vault.status");
  }

  private async reload(): Promise<void> {
    await this.load("vault.refresh");
  }

  private async load(method: string): Promise<void> {
    this.refresh.disabled = true;
    try { this.render(await window.koko.call(method) as VaultStatus); }
    catch (error) { this.fail(error instanceof Error ? error.message : String(error)); }
    finally { this.refresh.disabled = false; }
  }

  private render(status: VaultStatus): void {
    this.state.className = `vault-state ${status.available ? "online" : "offline"}`;
    this.state.querySelector("span")!.textContent = status.available ? "ONLINE" : "UNAVAILABLE";
    this.notes.textContent = String(status.noteCount);
    this.folders.textContent = String(status.folderCount);
    this.path.textContent = status.path;
    this.recent.replaceChildren(...status.recentNotes.map(note => this.noteRow(note)));
    if (!status.recentNotes.length)
      this.recent.append(Object.assign(document.createElement("p"), { className: "agent-empty", textContent: "No recent notes." }));
    this.meta.textContent = `scan ${status.scanMs} ms / ${new Date(status.scannedAt).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" })}`;
  }

  private noteRow(note: VaultRecentNote): HTMLElement {
    const row = document.createElement("div");
    row.className = "vault-note";
    row.append(
      Object.assign(document.createElement("span"), { textContent: note.path, title: note.path }),
      Object.assign(document.createElement("time"), { textContent: new Date(note.modifiedAt).toLocaleDateString([], { month: "short", day: "2-digit" }) })
    );
    return row;
  }

  private fail(message: string): void {
    this.state.className = "vault-state offline";
    this.state.querySelector("span")!.textContent = "ERROR";
    this.meta.textContent = message;
  }
}
