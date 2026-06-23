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

interface AgentActivity { phase: string; tool: string; focus: string; thought: string; }
interface AgentSnapshot {
  maxParallel: number;
  runningSteps: number;
  activity: AgentActivity;
  tasks: unknown[];
}

export class WorkspacePanelsController {
  private chatCompleted = 0;
  private chatErrors = 0;
  private events = 0;

  constructor() {
    window.koko.on("chat.started", () => this.markChat("streaming"));
    window.koko.on("chat.completed", () => {
      this.chatCompleted++;
      this.markChat("idle");
    });
    window.koko.on("chat.error", payload => {
      this.chatErrors++;
      const error = (payload as { error?: string })?.error ?? "chat failed";
      this.setText("telemetry-chat", "error");
      this.setText("telemetry-chat-detail", error);
      this.bump();
    });
    window.koko.on("agent.activity", payload => this.renderAgent((payload as { snapshot: AgentSnapshot }).snapshot));
    window.koko.on("agent.completed", payload => this.renderAgent((payload as { snapshot: AgentSnapshot }).snapshot));
    window.koko.on("vault.status", payload => this.renderVault(payload as VaultStatus));
  }

  setHost(status: "linked" | "preview" | "error", detail: string): void {
    this.setText("telemetry-host", status);
    this.setText("telemetry-host-detail", detail);
    this.bump();
  }

  renderInitial(agent: unknown, vault: unknown): void {
    this.renderAgent(agent as AgentSnapshot);
    this.renderVault(vault as VaultStatus);
  }

  private renderAgent(snapshot: AgentSnapshot): void {
    const current = snapshot.activity;
    this.setText("telemetry-agent", current.phase || "idle");
    this.setText("telemetry-agent-detail", `${current.tool || "agent"}: ${current.thought || current.focus || "no activity"}`);
    this.setText("telemetry-events", String(this.events));
    this.bump();
  }

  private renderVault(status: VaultStatus): void {
    this.setText("memory-state", status.available ? "online" : "offline");
    this.setText("memory-path", status.path || "Vault path unavailable.");
    this.setText("memory-notes", String(status.noteCount));
    this.setText("memory-folders", String(status.folderCount));
    this.setText("memory-meta", `scan ${status.scanMs} ms / ${this.time(status.scannedAt)}`);
    this.setText("telemetry-vault", status.available ? "online" : "offline");
    this.setText("telemetry-vault-detail", `${status.noteCount} notes / scan ${status.scanMs} ms`);
    const recent = document.getElementById("memory-recent");
    if (recent) {
      recent.replaceChildren(...status.recentNotes.map(note => this.noteRow(note)));
      if (!status.recentNotes.length)
        recent.append(Object.assign(document.createElement("p"), { className: "agent-empty", textContent: "No recent notes." }));
    }
    this.bump();
  }

  private markChat(status: string): void {
    this.setText("telemetry-chat", status);
    this.setText("telemetry-chat-detail", `${this.chatCompleted} completed / ${this.chatErrors} errors`);
    this.bump();
  }

  private noteRow(note: VaultRecentNote): HTMLElement {
    const row = document.createElement("div");
    row.append(
      Object.assign(document.createElement("span"), { textContent: note.path, title: note.path }),
      Object.assign(document.createElement("time"), { textContent: this.time(note.modifiedAt) })
    );
    return row;
  }

  private bump(): void {
    this.events++;
    this.setText("telemetry-events", String(this.events));
    this.setText("telemetry-updated", new Date().toLocaleTimeString([], { hour: "2-digit", minute: "2-digit", second: "2-digit" }));
  }

  private setText(id: string, value: string): void {
    const element = document.getElementById(id);
    if (element)
      element.textContent = value;
  }

  private time(value: string): string {
    const date = new Date(value);
    return Number.isNaN(date.getTime())
      ? "--"
      : date.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" });
  }
}
