import { populateOllamaCloudModelSelect } from "../ollamaCloudModels";

type SecretOp = { op: string; value?: string };
type SettingsValues = Record<string, boolean | number | string | SecretOp>;
interface SettingsSnapshot { values: SettingsValues; credentials: Record<string, boolean>; }
interface SettingsUpdateResult { settings: SettingsSnapshot; changed: string[]; restartRequired: boolean; }
interface WearStatus {
  connected: boolean;
  deviceId: string;
  bpm: number;
  battery: number | null;
  charging: boolean | null;
}

const PROVIDER_ROWS: Record<string, string[]> = {
  lmstudio: ["row-lm-url", "row-lm-model"],
  ollama: ["row-ollama-url", "row-ollama-key", "row-ollama-model"],
  "ollama-cloud": ["row-ollama-url", "row-ollama-key", "row-ollama-model"],
  claude: ["row-claude-key", "row-claude-model"],
  "ollama-cloud-proxy": ["row-ollama-cloud-proxy-url", "row-ollama-cloud-proxy-model", "row-ollama-cloud-proxy-key"]
};

export class SettingsPanelController {
  private readonly form = document.getElementById("settings-form") as HTMLFormElement;
  private readonly save = document.getElementById("settings-save") as HTMLButtonElement;
  private readonly status = document.getElementById("settings-status")!;
  private readonly segment = document.getElementById("autonomy-segment")!;
  private readonly providerSegment = document.getElementById("llm-provider-segment")!;
  private readonly providerRows = Object.fromEntries(
    Object.values(PROVIDER_ROWS).flat()
      .filter((id, index, all) => all.indexOf(id) === index)
      .map(id => [id, document.getElementById(id)!])
  );
  private readonly lmUrl = document.getElementById("lm-url") as HTMLInputElement;
  private readonly lmModel = document.getElementById("lm-model") as HTMLInputElement;
  private readonly ollamaUrl = document.getElementById("ollama-url") as HTMLInputElement;
  private readonly ollamaKey = document.getElementById("ollama-key") as HTMLInputElement;
  private readonly ollamaKeyClear = document.getElementById("ollama-key-clear") as HTMLButtonElement;
  private readonly ollamaKeyReveal = document.getElementById("ollama-key-reveal") as HTMLButtonElement;
  private readonly ollamaModel = document.getElementById("ollama-model") as HTMLInputElement;
  private readonly ollamaCloudProxyModel = document.getElementById("ollama-cloud-proxy-model") as HTMLSelectElement;
  private readonly ollamaCloudProxyKey = document.getElementById("ollama-cloud-proxy-key") as HTMLInputElement;
  private readonly ollamaCloudProxyKeyClear = document.getElementById("ollama-cloud-proxy-key-clear") as HTMLButtonElement;
  private readonly ollamaCloudProxyKeyReveal = document.getElementById("ollama-cloud-proxy-key-reveal") as HTMLButtonElement;
  private readonly claudeKey = document.getElementById("claude-key") as HTMLInputElement;
  private readonly claudeKeyClear = document.getElementById("claude-key-clear") as HTMLButtonElement;
  private readonly claudeKeyReveal = document.getElementById("claude-key-reveal") as HTMLButtonElement;
  private readonly claudeModel = document.getElementById("claude-model") as HTMLInputElement;
  private readonly tavilyKey = document.getElementById("tavily-key") as HTMLInputElement;
  private readonly tavilyKeyClear = document.getElementById("tavily-key-clear") as HTMLButtonElement;
  private readonly tavilyKeyReveal = document.getElementById("tavily-key-reveal") as HTMLButtonElement;
  private readonly responseStyle = document.getElementById("response-style") as HTMLSelectElement;
  private readonly color = document.getElementById("matrix-color") as HTMLInputElement;
  private readonly colorText = document.getElementById("matrix-color-text")!;
  private readonly plexusToggle = document.getElementById("plexus-enabled") as HTMLInputElement;
  private readonly wearStatus = document.getElementById("wear-status")!;
  private readonly credentials = document.getElementById("credential-grid")!;
  private available = false;
  private loaded = false;
  private autonomy = 2;
  private llmProvider = "ollama-cloud";
  private readonly fields: Record<string, string> = {
    spontaneousEnabled: "spontaneous-enabled", spontaneousIntervalMins: "spontaneous-mins",
    neuralGovernorEnabled: "neural-governor", screenAwarenessEnabled: "screen-enabled",
    screenAwarenessSendComments: "screen-comments", screenAwarenessIntervalMins: "screen-interval",
    screenAwarenessCommentCooldownMins: "screen-cooldown", systemOverlordEnabled: "overlord-enabled",
    browserEnabled: "browser-enabled", browserHeadless: "browser-headless",
    voiceInputEnabled: "voice-enabled", ttsEnabled: "tts-enabled", wearBridgeEnabled: "wear-enabled",
    wearBridgeIncludePromptContext: "wear-context", minimizeToTray: "tray-enabled",
    maxTokens: "max-tokens"
  };

  constructor() {
    this.segment.addEventListener("click", event => {
      const button = (event.target as HTMLElement).closest<HTMLButtonElement>("button[data-value]");
      if (button) this.setAutonomy(Number(button.dataset.value));
    });
    this.providerSegment.addEventListener("click", event => {
      const button = (event.target as HTMLElement).closest<HTMLButtonElement>("button[data-value]");
      if (button) this.setProvider(button.dataset.value ?? "ollama-cloud");
    });
    this.color.addEventListener("input", () => {
      this.colorText.textContent = this.color.value.toUpperCase();
      document.documentElement.style.setProperty("--accent", this.color.value);
    });
    this.plexusToggle.addEventListener("change", () => {
      (window as unknown as { plexusEnabled?: boolean }).plexusEnabled = this.plexusToggle.checked;
    });
    this.form.addEventListener("submit", event => { event.preventDefault(); void this.persist(); });
    this.wireSecretClear(this.ollamaKey, this.ollamaKeyClear, "sk-...");
    this.wireSecretClear(this.ollamaCloudProxyKey, this.ollamaCloudProxyKeyClear, "залиш порожнім — auth через ollama signin");
    this.wireSecretClear(this.claudeKey, this.claudeKeyClear, "sk-ant-...");
    this.wireSecretClear(this.tavilyKey, this.tavilyKeyClear, "tvly-...");
    this.wireSecretReveal(this.ollamaKey, this.ollamaKeyReveal, "ollama");
    this.wireSecretReveal(this.ollamaCloudProxyKey, this.ollamaCloudProxyKeyReveal, "ollamaCloudProxy");
    this.wireSecretReveal(this.claudeKey, this.claudeKeyReveal, "claude");
    this.wireSecretReveal(this.tavilyKey, this.tavilyKeyReveal, "tavily");
  }

  // Toggling type back to "password" alone would leave the real fetched value sitting in
  // the input - secretState() can't tell that apart from a real edit and would save it
  // as a literal "replace" on next Save. Always clearing back to "" on hide keeps the
  // unchanged/replace/clear contract exactly as it was before reveal was ever clicked.
  private wireSecretReveal(input: HTMLInputElement, button: HTMLButtonElement, which: string): void {
    button.addEventListener("click", () => void this.toggleReveal(input, button, which));
  }

  private async toggleReveal(input: HTMLInputElement, button: HTMLButtonElement, which: string): Promise<void> {
    if (input.type === "text") {
      input.type = "password";
      input.value = "";
      delete input.dataset.cleared;
      button.classList.remove("active");
      return;
    }
    if (input.value.trim() !== "") {
      // Unsaved typed value - just reveal what's already there, no fetch needed.
      input.type = "text";
      button.classList.add("active");
      return;
    }
    try {
      const r = await window.koko.call<{ key: string; value: string }>("secrets.reveal", { key: which });
      if (!r.value) return;
      input.type = "text";
      input.value = r.value;
      button.classList.add("active");
    } catch (error) {
      console.error("secrets.reveal failed:", error);
    }
  }

  // Pending-clear is tracked via data-cleared rather than sent immediately so the user can
  // still cancel by typing a replacement key before hitting Save.
  private wireSecretClear(input: HTMLInputElement, button: HTMLButtonElement, emptyPlaceholder: string): void {
    button.addEventListener("click", () => {
      input.value = "";
      input.dataset.cleared = "true";
      input.placeholder = "буде очищено при збереженні";
    });
    input.addEventListener("input", () => {
      delete input.dataset.cleared;
      if (input.value === "") input.placeholder = emptyPlaceholder;
    });
  }

  private secretState(input: HTMLInputElement): { op: string; value?: string } {
    const value = input.value.trim();
    if (value === "") return { op: input.dataset.cleared === "true" ? "clear" : "unchanged" };
    return { op: "replace", value };
  }

  // A blanked-after-save secret field shows a placeholder like "sk-..." to mean "leave
  // blank to keep". Nothing stops someone from literally typing that placeholder back in
  // thinking it's a mask rather than a real new value - secretState() can't tell the
  // difference, so it gets saved as the literal key. This is the one place that can.
  private looksLikePlaceholder(value: string): boolean {
    return /^\.{2,}$/.test(value) || /^\*+$/.test(value) || /^[a-z]+-\.{2,}$/i.test(value);
  }

  setAvailable(value: boolean): void {
    this.available = value;
    this.save.disabled = !value;
    if (value && !this.loaded)
      void this.load();
  }

  private async load(): Promise<void> {
    this.loaded = true;
    this.save.disabled = true;
    this.status.textContent = "Loading...";
    try {
      this.fill(await window.koko.call("settings.get") as SettingsSnapshot);
      this.status.textContent = "Settings synchronized.";
    } catch (error) {
      this.loaded = false;
      this.status.textContent = error instanceof Error ? error.message : String(error);
    } finally {
      this.save.disabled = !this.available;
    }
    void this.loadWearStatus();
  }

  private async loadWearStatus(): Promise<void> {
    try {
      const wear = await window.koko.call<WearStatus>("wear.status", null, 5000);
      this.wearStatus.textContent = wear.connected
        ? `Connected · ${wear.deviceId} · ${wear.bpm.toFixed(0)} bpm` + (wear.battery != null ? ` · battery ${wear.battery.toFixed(0)}%` : "")
        : "Disconnected";
    } catch {
      this.wearStatus.textContent = "Disconnected";
    }
  }

  private async persist(): Promise<void> {
    const suspicious = [
      { input: this.ollamaKey, label: "Ollama API Key" },
      { input: this.ollamaCloudProxyKey, label: "Ollama Cloud Proxy API Key" },
      { input: this.claudeKey, label: "Claude API Key" },
      { input: this.tavilyKey, label: "Tavily API Key" }
    ].filter(({ input }) => this.looksLikePlaceholder(input.value.trim()));
    if (suspicious.length > 0) {
      const names = suspicious.map(s => s.label).join(", ");
      const proceed = window.confirm(
        `Поле "${names}" виглядає як заглушка (наприклад "..."), а не реальний ключ.\n` +
        `Зберегти це значення буквально як ключ?`
      );
      if (!proceed) return;
    }

    this.save.disabled = true;
    this.status.textContent = "Saving...";
    try {
      const result = await window.koko.call("settings.update", this.read()) as SettingsUpdateResult;
      this.fill(result.settings);
      this.status.textContent = result.restartRequired ? "Saved. Restart required for device service changes." : "Saved.";
    } catch (error) {
      this.status.textContent = error instanceof Error ? error.message : String(error);
    } finally {
      this.save.disabled = !this.available;
    }
  }

  private setAutonomy(value: number): void {
    this.autonomy = value;
    for (const button of this.segment.querySelectorAll<HTMLButtonElement>("button"))
      button.classList.toggle("selected", Number(button.dataset.value) === value);
  }

  private setProvider(value: string): void {
    this.llmProvider = value;
    for (const button of this.providerSegment.querySelectorAll<HTMLButtonElement>("button"))
      button.classList.toggle("selected", button.dataset.value === value);
    const visible = new Set(PROVIDER_ROWS[value] ?? []);
    for (const [id, row] of Object.entries(this.providerRows))
      row.style.display = visible.has(id) ? "" : "none";
  }

  private fill(snapshot: SettingsSnapshot): void {
    const values = snapshot.values ?? {};
    this.setAutonomy(Number(values.proactiveAutonomyLevel ?? 2));
    for (const [name, id] of Object.entries(this.fields)) {
      const input = document.getElementById(id) as HTMLInputElement;
      if (input.type === "checkbox") input.checked = Boolean(values[name]);
      else input.value = String(values[name] ?? "");
    }
    this.setProvider(String(values.llmProvider ?? "ollama-cloud"));
    this.lmUrl.value = String(values.lmUrl ?? "");
    this.lmModel.value = String(values.lmModel ?? "");
    this.ollamaUrl.value = String(values.ollamaUrl ?? "");
    this.ollamaModel.value = String(values.ollamaModel ?? "");
    populateOllamaCloudModelSelect(this.ollamaCloudProxyModel, String(values.ollamaCloudProxyModel ?? ""));
    this.claudeModel.value = String(values.claudeModel ?? "");
    this.ollamaKey.value = "";
    delete this.ollamaKey.dataset.cleared;
    this.ollamaKey.placeholder = snapshot.credentials?.ollama ? "•••• configured (leave blank to keep)" : "sk-...";
    this.ollamaCloudProxyKey.value = "";
    delete this.ollamaCloudProxyKey.dataset.cleared;
    this.ollamaCloudProxyKey.placeholder = snapshot.credentials?.ollamaCloudProxy
      ? "•••• configured (leave blank to keep)"
      : "залиш порожнім — auth через ollama signin";
    this.claudeKey.value = "";
    delete this.claudeKey.dataset.cleared;
    this.claudeKey.placeholder = snapshot.credentials?.claude ? "•••• configured (leave blank to keep)" : "sk-ant-...";
    this.tavilyKey.value = "";
    delete this.tavilyKey.dataset.cleared;
    this.tavilyKey.placeholder = snapshot.credentials?.tavily ? "•••• configured (leave blank to keep)" : "tvly-...";
    this.responseStyle.value = ["concise", "balanced", "deep"].includes(String(values.responseStyle))
      ? String(values.responseStyle)
      : "balanced";
    this.color.value = /^#[0-9a-f]{6}$/i.test(String(values.matrixColor ?? "")) ? String(values.matrixColor) : "#5fc1b3";
    this.colorText.textContent = this.color.value.toUpperCase();
    document.documentElement.style.setProperty("--accent", this.color.value);
    this.renderCredentials(snapshot.credentials ?? {});
  }

  private read(): SettingsValues {
    const values: SettingsValues = {
      proactiveAutonomyLevel: this.autonomy,
      responseStyle: this.responseStyle.value,
      matrixColor: this.color.value.toUpperCase(),
      llmProvider: this.llmProvider,
      lmUrl: this.lmUrl.value.trim(),
      lmModel: this.lmModel.value.trim(),
      ollamaUrl: this.ollamaUrl.value.trim(),
      ollamaModel: this.ollamaModel.value.trim(),
      ollamaCloudProxyModel: this.ollamaCloudProxyModel.value,
      ollamaCloudProxyApiKey: this.secretState(this.ollamaCloudProxyKey),
      ollamaApiKey: this.secretState(this.ollamaKey),
      claudeModel: this.claudeModel.value.trim(),
      claudeApiKey: this.secretState(this.claudeKey),
      tavilyApiKey: this.secretState(this.tavilyKey)
    };
    for (const [name, id] of Object.entries(this.fields)) {
      const input = document.getElementById(id) as HTMLInputElement;
      values[name] = input.type === "checkbox" ? input.checked : Number(input.value);
    }
    return values;
  }

  private renderCredentials(values: Record<string, boolean>): void {
    const labels: Record<string, string> = {
      telegramBot: "Telegram bot", telegramUser: "Telegram user", openAi: "OpenAI",
      claude: "Claude", ollama: "Ollama", ollamaCloudProxy: "Ollama Cloud (proxy)", tavily: "Tavily"
    };
    this.credentials.replaceChildren(...Object.entries(labels).map(([key, label]) => {
      const row = document.createElement("div");
      row.className = "credential";
      const name = document.createElement("span");
      name.textContent = label;
      const state = document.createElement("output");
      state.textContent = values[key] ? "CONFIGURED" : "NOT SET";
      state.className = values[key] ? "configured" : "";
      row.append(name, state);
      return row;
    }));
  }
}
