import "./bridge";
import { AgentBoardController } from "./components/AgentBoard";
import { AgentsPage } from "./components/AgentsPage";
import { ArtifactsPanelController } from "./components/ArtifactsPanel";
import { ChatController } from "./components/Chat";
import { MotionController } from "./components/MotionController";
import { initPlexus } from "./components/Plexus";
import { SettingsPanelController } from "./components/SettingsPanel";
import { TelegramPanelController } from "./components/TelegramPanel";
import { VaultPanelController } from "./components/VaultPanel";
import { WorkspacePanelsController } from "./components/WorkspacePanels";

function required<T extends HTMLElement>(id: string): T {
  const element = document.getElementById(id);
  if (!(element instanceof HTMLElement))
    throw new Error(`Required shell element is missing: #${id}`);
  return element as T;
}

const chat = new ChatController(
  required<HTMLFormElement>("chat-form"),
  required<HTMLInputElement>("chat-input"),
  required<HTMLButtonElement>("chat-send"),
  required("messages"),
  required("chat-scroll"),
  required("chat-status")
);
const agents = new AgentBoardController();
const agentsPage = new AgentsPage();
const artifactsPanel = new ArtifactsPanelController();
const vault = new VaultPanelController();
const settings = new SettingsPanelController();
const telegram = new TelegramPanelController();
const motion = new MotionController();
const workspacePanels = new WorkspacePanelsController();
initPlexus();
const bridgeStatus = required("bridge-status");
const systemScanButton = required<HTMLButtonElement>("system-scan");
const telemetryRefreshButton = required<HTMLButtonElement>("telemetry-refresh");
const personaStatusText = document.getElementById("persona-status-text");

interface PersonaStatus { mood: string; bond: string; connection: number; }

interface SettingsSnapshot {
  values: { llmProvider: string };
  credentials: { ollama: boolean; claude: boolean };
}

async function checkOnboarding(): Promise<void> {
  const banner = document.getElementById("onboarding-banner");
  if (!banner) return;
  try {
    const snapshot = await window.koko.call<SettingsSnapshot>("settings.get", null, 5000);
    const provider = snapshot.values.llmProvider;
    const needsKey = provider === "ollama-cloud" ? !snapshot.credentials.ollama
      : provider === "claude" ? !snapshot.credentials.claude
      : false;
    banner.style.display = needsKey ? "flex" : "none";
  } catch {
    banner.style.display = "none";
  }
}

document.getElementById("onboarding-settings-btn")?.addEventListener("click", () => {
  document.getElementById("settings-open")?.click();
});
document.getElementById("onboarding-dismiss")?.addEventListener("click", () => {
  document.getElementById("onboarding-banner")!.style.display = "none";
});

async function refreshPersona(): Promise<void> {
  if (!personaStatusText) return;
  try {
    const status = await window.koko.call<PersonaStatus>("persona.status", null, 5000);
    personaStatusText.textContent = `${status.mood} · ${status.bond} · ${status.connection.toFixed(2)}`;
  } catch (error) {
    personaStatusText.textContent = error instanceof Error ? error.message : String(error);
  }
}

console.info("[Kokonoe Web Shell] TypeScript runtime rendered");
document.documentElement.dataset.renderedAt = new Date().toISOString();
document.documentElement.dataset.runtime = "typescript";

async function connectHost(): Promise<void> {
  try {
    const result = await window.koko.call<string>("ping", { source: "web-shell" });
    bridgeStatus.textContent = String(result).toUpperCase();
    bridgeStatus.className = "ok";
    chat.setAvailable(true);
    settings.setAvailable(true);
    motion.setHostState("ready");
    workspacePanels.setHost("linked", String(result));
    await Promise.allSettled([agents.connect(), vault.connect(), telegram.connect()]);
    const [agentSnapshot, vaultStatus, memorySnapshot, systemSnapshot] = await Promise.all([
      window.koko.call("agent.snapshot"),
      window.koko.call("vault.status"),
      window.koko.call("memory.snapshot"),
      window.koko.call("system.snapshot")
    ]);
    workspacePanels.renderInitial(agentSnapshot, vaultStatus);
    workspacePanels.renderMemory(memorySnapshot);
    workspacePanels.renderSystem(systemSnapshot);
    workspacePanels.renderRuntime(await window.koko.call("runtime.snapshot"));
    systemScanButton.disabled = false;
    telemetryRefreshButton.disabled = false;
    void refreshPersona();
    void checkOnboarding();
    void agentsPage.init();
    void artifactsPanel.init();
    window.setInterval(() => void refreshPersona(), 20000);
    window.setInterval(() => {
      refreshRuntime()
        .catch(error => workspacePanels.setHost("error", error instanceof Error ? error.message : String(error)));
    }, 15000);
    window.setInterval(() => {
      window.koko.call("memory.refresh", null, 5000)
        .then(snapshot => workspacePanels.renderMemory(snapshot))
        .catch(error => workspacePanels.setHost("error", error instanceof Error ? error.message : String(error)));
    }, 30000);
    console.info("[Kokonoe Web Bridge] ping ->", result);
  } catch (error) {
    bridgeStatus.textContent = window.koko.available ? "PING FAILED" : "HOST ONLY";
    bridgeStatus.className = "pending";
    chat.setAvailable(false);
    settings.setAvailable(false);
    systemScanButton.disabled = true;
    telemetryRefreshButton.disabled = true;
    motion.setHostState(window.koko.available ? "error" : "preview");
    workspacePanels.setHost(window.koko.available ? "error" : "preview", error instanceof Error ? error.message : String(error));
    console.warn("[Kokonoe Web Bridge]", error instanceof Error ? error.message : String(error));
  }
}

async function refreshRuntime(): Promise<void> {
  const snapshot = await window.koko.call("runtime.refresh", null, 5000);
  workspacePanels.renderRuntime(snapshot);
}

async function scanSystem(): Promise<void> {
  systemScanButton.disabled = true;
  workspacePanels.setRuntimeBusy("scanning");
  try {
    const snapshot = await window.koko.call("system.scan", null, 30000);
    workspacePanels.renderSystem(snapshot);
  } catch (error) {
    workspacePanels.setRuntimeError(error);
  } finally {
    systemScanButton.disabled = !window.koko.available;
  }
}

void connectHost();

const panels: Record<string, HTMLElement | null> = {
  chat: document.getElementById("chat-scroll"),
  tasks: document.getElementById("panel-tasks"),
  agents: document.getElementById("panel-agents"),
  artifacts: document.getElementById("panel-artifacts"),
  memory: document.getElementById("panel-memory"),
  telemetry: document.getElementById("panel-telemetry"),
  settings: document.getElementById("panel-settings"),
};
const composer = required<HTMLFormElement>("chat-form");

function switchPanel(id: string): void {
  const target = panels[id] ? id : "chat";
  Object.entries(panels).forEach(([key, element]) => {
    if (element)
      element.style.display = key === target ? "" : "none";
  });
  composer.style.display = target === "chat" ? "" : "none";
  document.querySelectorAll<HTMLButtonElement>(".rail button[data-panel]").forEach(button => {
    button.classList.toggle("active", button.dataset.panel === target);
  });
}

document.querySelectorAll<HTMLButtonElement>(".rail button[data-panel]").forEach(button => {
  button.addEventListener("click", () => switchPanel(button.dataset.panel ?? "chat"));
});

if (window.location.hash === "#settings")
  switchPanel("settings");

async function resetChat(): Promise<void> {
  try {
    await window.koko.call("chat.clear_history");
  } catch (error) {
    console.warn("[Kokonoe Web Bridge] chat.clear_history failed", error instanceof Error ? error.message : String(error));
  }
  document.getElementById("messages")!.innerHTML = "";
  (document.getElementById("chat-input") as HTMLInputElement)?.focus();
}

document.getElementById("new-chat-btn")?.addEventListener("click", () => void resetChat());

interface BrowserStatusPayload {
  status: string;
  url?: string;
  title?: string;
  detail?: string | null;
}
interface BrowserScreenshotPayload {
  path: string;
  url: string;
  dataUrl?: string | null;
}

function setBrowserDotState(status: string): string {
  if (status === "closed") return "idle";
  if (status === "error") return "error";
  if (status === "ready" || status === "idle" || status === "navigated") return "ready";
  return "busy";
}

window.koko.on("browser.status", payload => {
  const p = payload as BrowserStatusPayload;
  const heading = document.getElementById("browser-section");
  const panel = document.getElementById("browser-panel");
  if (heading) heading.style.display = "flex";
  if (panel) panel.style.display = "block";

  const dot = document.getElementById("browser-dot");
  if (dot) {
    dot.classList.remove("ready", "busy", "error", "idle");
    dot.classList.add(setBrowserDotState(p.status));
  }
  const statusText = document.getElementById("browser-status-text");
  if (statusText) statusText.textContent = p.status;
  const urlEl = document.getElementById("browser-url");
  if (urlEl) urlEl.textContent = p.url ?? "";
  const titleEl = document.getElementById("browser-title");
  if (titleEl) titleEl.textContent = p.title ?? "";

  const closeBtn = document.getElementById("browser-close-btn");
  if (closeBtn) closeBtn.style.display = p.status === "closed" ? "none" : "flex";
});

window.koko.on("browser.screenshot", payload => {
  const p = payload as BrowserScreenshotPayload;
  if (!p.dataUrl) return;
  const wrap = document.getElementById("browser-screenshot-wrap");
  const img = document.getElementById("browser-screenshot-img") as HTMLImageElement | null;
  const label = document.getElementById("browser-screenshot-label");
  if (!wrap || !img) return;
  img.src = p.dataUrl;
  if (label) {
    try { label.textContent = new URL(p.url).hostname; }
    catch { label.textContent = p.url; }
  }
  wrap.style.display = "block";
});

document.getElementById("browser-close-btn")?.addEventListener("click", () => void window.koko.call("browser.close"));

document.getElementById("browser-task-form")?.addEventListener("submit", event => {
  event.preventDefault();
  const input = document.getElementById("browser-task-input") as HTMLInputElement | null;
  const task = input?.value.trim() ?? "";
  if (!task || !input) return;

  const chatInput = document.getElementById("chat-input") as HTMLInputElement | null;
  if (chatInput) chatInput.value = `[browser task] ${task}`;
  switchPanel("chat");
  (document.getElementById("chat-form") as HTMLFormElement | null)?.requestSubmit();
  input.value = "";
});

const panelShortcutIds = ["chat", "tasks", "memory", "telemetry", "agents"];

document.addEventListener("keydown", e => {
  const mod = e.ctrlKey || e.metaKey;

  if (e.key === "Escape") {
    switchPanel("chat");
    return;
  }

  if (mod && e.key === "/") {
    e.preventDefault();
    (document.getElementById("chat-input") as HTMLInputElement)?.focus();
    return;
  }

  if (mod && ["1", "2", "3", "4", "5"].includes(e.key)) {
    e.preventDefault();
    const panelId = panelShortcutIds[Number(e.key) - 1];
    if (panelId) switchPanel(panelId);
    return;
  }

  if (mod && e.key.toLowerCase() === "n") {
    e.preventDefault();
    void resetChat();
  }
});

systemScanButton.addEventListener("click", () => void scanSystem());
telemetryRefreshButton.addEventListener("click", () => {
  telemetryRefreshButton.disabled = true;
  refreshRuntime()
    .catch(error => workspacePanels.setRuntimeError(error))
    .finally(() => {
      telemetryRefreshButton.disabled = !window.koko.available;
    });
});
