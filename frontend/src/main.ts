import "./bridge";
import { AgentBoardController } from "./components/AgentBoard";
import { ChatController } from "./components/Chat";
import { MotionController } from "./components/MotionController";
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
const vault = new VaultPanelController();
const settings = new SettingsPanelController();
const telegram = new TelegramPanelController();
const motion = new MotionController();
const workspacePanels = new WorkspacePanelsController();
const bridgeStatus = required("bridge-status");

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
    const [agentSnapshot, vaultStatus] = await Promise.all([
      window.koko.call("agent.snapshot"),
      window.koko.call("vault.status")
    ]);
    workspacePanels.renderInitial(agentSnapshot, vaultStatus);
    console.info("[Kokonoe Web Bridge] ping ->", result);
  } catch (error) {
    bridgeStatus.textContent = window.koko.available ? "PING FAILED" : "HOST ONLY";
    bridgeStatus.className = "pending";
    chat.setAvailable(false);
    settings.setAvailable(false);
    motion.setHostState(window.koko.available ? "error" : "preview");
    workspacePanels.setHost(window.koko.available ? "error" : "preview", error instanceof Error ? error.message : String(error));
    console.warn("[Kokonoe Web Bridge]", error instanceof Error ? error.message : String(error));
  }
}

if (window.location.hash === "#settings")
  void settings.open();

void connectHost();

const panels: Record<string, HTMLElement | null> = {
  chat: document.getElementById("chat-scroll"),
  tasks: document.getElementById("panel-tasks"),
  memory: document.getElementById("panel-memory"),
  telemetry: document.getElementById("panel-telemetry"),
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
