type SettingsValues = Record<string, boolean | number | string>;
interface SettingsSnapshot { values: SettingsValues; credentials: Record<string, boolean>; }
interface SettingsUpdateResult { settings: SettingsSnapshot; changed: string[]; restartRequired: boolean; }

export class SettingsPanelController {
  private readonly drawer = document.getElementById("settings-drawer")!;
  private readonly backdrop = document.getElementById("settings-backdrop")!;
  private readonly form = document.getElementById("settings-form") as HTMLFormElement;
  private readonly save = document.getElementById("settings-save") as HTMLButtonElement;
  private readonly status = document.getElementById("settings-status")!;
  private readonly segment = document.getElementById("autonomy-segment")!;
  private readonly providerSegment = document.getElementById("llm-provider-segment")!;
  private readonly ollamaUrlRow = document.getElementById("row-ollama-url")!;
  private readonly ollamaKeyRow = document.getElementById("row-ollama-key")!;
  private readonly ollamaModelRow = document.getElementById("row-ollama-model")!;
  private readonly ollamaUrl = document.getElementById("ollama-url") as HTMLInputElement;
  private readonly ollamaKey = document.getElementById("ollama-key") as HTMLInputElement;
  private readonly ollamaModel = document.getElementById("ollama-model") as HTMLInputElement;
  private readonly color = document.getElementById("matrix-color") as HTMLInputElement;
  private readonly colorText = document.getElementById("matrix-color-text")!;
  private readonly credentials = document.getElementById("credential-grid")!;
  private available = false;
  private autonomy = 2;
  private llmProvider = "ollama-cloud";
  private readonly fields: Record<string, string> = {
    spontaneousEnabled: "spontaneous-enabled", spontaneousIntervalMins: "spontaneous-mins",
    neuralGovernorEnabled: "neural-governor", screenAwarenessEnabled: "screen-enabled",
    screenAwarenessSendComments: "screen-comments", screenAwarenessIntervalMins: "screen-interval",
    screenAwarenessCommentCooldownMins: "screen-cooldown", systemOverlordEnabled: "overlord-enabled",
    voiceInputEnabled: "voice-enabled", ttsEnabled: "tts-enabled", wearBridgeEnabled: "wear-enabled",
    wearBridgeIncludePromptContext: "wear-context", minimizeToTray: "tray-enabled"
  };

  constructor() {
    document.getElementById("settings-open")!.addEventListener("click", () => void this.open());
    document.getElementById("settings-close")!.addEventListener("click", () => this.close());
    this.backdrop.addEventListener("click", () => this.close());
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
    this.form.addEventListener("submit", event => { event.preventDefault(); void this.persist(); });
  }

  setAvailable(value: boolean): void {
    this.available = value;
    this.save.disabled = !value;
    if (value && this.drawer.getAttribute("aria-hidden") === "true")
      void this.load();
  }

  async open(): Promise<void> {
    this.drawer.classList.add("open");
    this.backdrop.classList.add("open");
    this.drawer.setAttribute("aria-hidden", "false");
    if (!this.available) {
      this.status.textContent = "Host connection required.";
      return;
    }
    await this.load();
  }

  close(): void {
    this.drawer.classList.remove("open");
    this.backdrop.classList.remove("open");
    this.drawer.setAttribute("aria-hidden", "true");
  }

  private async load(): Promise<void> {
    this.save.disabled = true;
    this.status.textContent = "Loading...";
    try {
      this.fill(await window.koko.call("settings.get") as SettingsSnapshot);
      this.status.textContent = "Settings synchronized.";
    } catch (error) {
      this.status.textContent = error instanceof Error ? error.message : String(error);
    } finally {
      this.save.disabled = !this.available;
    }
  }

  private async persist(): Promise<void> {
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
    const showCloudFields = value === "ollama-cloud";
    this.ollamaUrlRow.style.display = showCloudFields ? "" : "none";
    this.ollamaKeyRow.style.display = showCloudFields ? "" : "none";
    this.ollamaModelRow.style.display = showCloudFields ? "" : "none";
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
    this.ollamaUrl.value = String(values.ollamaUrl ?? "");
    this.ollamaModel.value = String(values.ollamaModel ?? "");
    this.ollamaKey.value = "";
    this.ollamaKey.placeholder = snapshot.credentials?.ollama ? "•••• configured (leave blank to keep)" : "sk-...";
    this.color.value = /^#[0-9a-f]{6}$/i.test(String(values.matrixColor ?? "")) ? String(values.matrixColor) : "#5fc1b3";
    this.colorText.textContent = this.color.value.toUpperCase();
    document.documentElement.style.setProperty("--accent", this.color.value);
    this.renderCredentials(snapshot.credentials ?? {});
  }

  private read(): SettingsValues {
    const values: SettingsValues = {
      proactiveAutonomyLevel: this.autonomy,
      matrixColor: this.color.value.toUpperCase(),
      llmProvider: this.llmProvider,
      ollamaUrl: this.ollamaUrl.value.trim(),
      ollamaModel: this.ollamaModel.value.trim(),
      ollamaApiKey: this.ollamaKey.value.trim()
    };
    for (const [name, id] of Object.entries(this.fields)) {
      const input = document.getElementById(id) as HTMLInputElement;
      values[name] = input.type === "checkbox" ? input.checked : Number(input.value);
    }
    return values;
  }

  private renderCredentials(values: Record<string, boolean>): void {
    const labels: Record<string, string> = { telegramBot: "Telegram bot", telegramUser: "Telegram user", openAi: "OpenAI", claude: "Claude", ollama: "Ollama" };
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
