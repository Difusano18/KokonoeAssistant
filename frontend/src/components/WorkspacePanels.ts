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

interface RuntimeHeartbeatEntry {
  service: string;
  status: string;
  detail: string;
  updatedAt: string;
  ageSeconds: number;
}

interface RuntimeSnapshot {
  takenAt: string;
  process?: {
    pid: number;
    workingSetMb: number;
    privateMemoryMb: number;
    uptimeSeconds: number;
    responding: boolean;
  };
  llm?: {
    status: string;
    provider: string;
    model: string;
    inFlight: number;
    totalRequests: number;
    totalFailures: number;
    lastLatencyMs: number;
    lastError: string;
  };
  wearable?: {
    currentBpm: number;
    deviceId: string;
    fresh: boolean;
    bridgeState: string;
    bridgeReason: string;
    bridgePort: number;
    bridgeSamples: number;
    bridgeAuthFailures: number;
    bridgePendingCommands: number;
    summary: string;
  };
  heartbeat?: {
    markdownPath: string;
    htmlPath: string;
    entries: RuntimeHeartbeatEntry[];
  };
}

interface MemoryFact {
  id: string;
  content: string;
  category: string;
  importance: number;
  confirmCount: number;
  lastSeen: string;
  tags: string[];
}

interface MemoryEpisode {
  id: string;
  summary: string;
  emotionalTone: string;
  intensity: number;
  when: string;
  keywords: string[];
}

interface MemorySnapshot {
  takenAt: string;
  factCount: number;
  episodeCount: number;
  sessionFactCount: number;
  sessionStartedAt: string;
  currentContext: string;
  lastUserMessage: string;
  facts: MemoryFact[];
  episodes: MemoryEpisode[];
  sessionFacts: string[];
}

interface SystemSnapshot {
  takenAt: string;
  status: string;
  error: string;
  scannedFiles: number;
  totalBytes: number;
  console: string;
  files: unknown[];
  processes: unknown[];
  proposals: unknown[];
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
    window.koko.on("memory.snapshot", payload => this.renderMemory(payload as MemorySnapshot));
    window.koko.on("runtime.snapshot", payload => this.renderRuntime(payload as RuntimeSnapshot));
    window.koko.on("system.snapshot", payload => this.renderSystem(payload as SystemSnapshot));
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

  renderMemory(snapshot: unknown): void {
    const memory = snapshot as MemorySnapshot;
    this.setText("memory-state", "online");
    this.setText("memory-path", `${memory.factCount} facts / ${memory.episodeCount} episodes / ${memory.sessionFactCount} session`);
    this.setText("memory-notes", String(memory.factCount));
    this.setText("memory-folders", String(memory.episodeCount));
    this.setText("memory-meta", `session ${this.time(memory.sessionStartedAt)} / refresh ${this.time(memory.takenAt)}`);

    const recent = document.getElementById("memory-recent");
    if (!recent) return;

    const rows = [
      ...memory.facts.slice(0, 6).map(fact => this.memoryRow(
        fact.content,
        `${fact.category} · ${(fact.importance * 100).toFixed(0)}% · x${fact.confirmCount}`)),
      ...memory.episodes.slice(0, 4).map(episode => this.memoryRow(
        episode.summary,
        `${episode.emotionalTone} · ${(episode.intensity * 100).toFixed(0)}% · ${this.time(episode.when)}`)),
      ...memory.sessionFacts.slice(-4).map(fact => this.memoryRow(fact, "session"))
    ];

    recent.replaceChildren(...rows);
    if (!rows.length)
      recent.append(Object.assign(document.createElement("p"), { className: "agent-empty", textContent: "No memory facts yet." }));
    this.bump();
  }

  renderRuntime(snapshot: unknown): void {
    const runtime = snapshot as RuntimeSnapshot;
    const process = runtime.process;
    const llm = runtime.llm;
    const wearable = runtime.wearable;

    if (process) {
      this.setText("telemetry-host", process.responding ? "healthy" : "hung");
      this.setText("telemetry-host-detail", `pid ${process.pid} / ${process.workingSetMb} MB / uptime ${this.duration(process.uptimeSeconds)}`);
    }
    if (llm) {
      this.setText("telemetry-agent", llm.status || "idle");
      const model = [llm.provider, llm.model].filter(Boolean).join(" / ") || "model unknown";
      this.setText("telemetry-agent-detail", `${model}; ${llm.inFlight} active; ${llm.totalRequests} req; ${llm.totalFailures} fail; ${llm.lastLatencyMs} ms`);
    }
    if (wearable) {
      const bpm = wearable.currentBpm > 0 ? `${Math.round(wearable.currentBpm)} bpm` : "no bpm";
      this.setText("telemetry-vault", wearable.bridgeState || "wearable");
      this.setText("telemetry-vault-detail", `${bpm}; ${wearable.deviceId || "no device"}; samples ${wearable.bridgeSamples}; auth ${wearable.bridgeAuthFailures}; queue ${wearable.bridgePendingCommands}`);
    }
    const feed = document.getElementById("runtime-feed");
    const entries = runtime.heartbeat?.entries ?? [];
    if (feed) {
      feed.replaceChildren(...entries.slice(0, 10).map(entry => this.runtimeRow(entry)));
      if (!entries.length)
        feed.append(Object.assign(document.createElement("p"), { className: "agent-empty", textContent: "No heartbeat entries." }));
    }
    this.setText("telemetry-updated", this.time(runtime.takenAt));
    this.bump();
  }

  renderSystem(snapshot: unknown): void {
    const system = snapshot as SystemSnapshot;
    const proposalCount = system.proposals?.length ?? 0;
    const processCount = system.processes?.length ?? 0;
    const fileCount = system.scannedFiles ?? 0;
    this.setText("telemetry-system", system.status || "idle");
    const detail = system.error
      ? system.error
      : `${fileCount} files / ${proposalCount} proposals / ${processCount} processes`;
    this.setText("telemetry-system-detail", detail);

    const feed = document.getElementById("system-feed");
    if (feed) {
      const lines = (system.console || "")
        .split(/\r?\n/)
        .filter(Boolean)
        .slice(0, 10)
        .map(line => this.simpleRow(line, this.time(system.takenAt)));
      feed.replaceChildren(...lines);
      if (!lines.length)
        feed.append(Object.assign(document.createElement("p"), { className: "agent-empty", textContent: "No system snapshot yet." }));
    }
    this.bump();
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

  private memoryRow(content: string, meta: string): HTMLElement {
    const row = document.createElement("div");
    row.append(
      Object.assign(document.createElement("span"), { textContent: content, title: content }),
      Object.assign(document.createElement("time"), { textContent: meta })
    );
    return row;
  }

  private runtimeRow(entry: RuntimeHeartbeatEntry): HTMLElement {
    const row = document.createElement("div");
    row.append(
      Object.assign(document.createElement("span"), { textContent: `${entry.service}: ${entry.status} — ${entry.detail}`, title: entry.detail }),
      Object.assign(document.createElement("time"), { textContent: `${Math.round(entry.ageSeconds)}s` })
    );
    return row;
  }

  private simpleRow(content: string, meta: string): HTMLElement {
    const row = document.createElement("div");
    row.append(
      Object.assign(document.createElement("span"), { textContent: content, title: content }),
      Object.assign(document.createElement("time"), { textContent: meta })
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

  private duration(seconds: number): string {
    const total = Math.max(0, Math.floor(seconds));
    const hours = Math.floor(total / 3600);
    const minutes = Math.floor((total % 3600) / 60);
    return hours > 0 ? `${hours}h ${minutes}m` : `${minutes}m`;
  }
}
