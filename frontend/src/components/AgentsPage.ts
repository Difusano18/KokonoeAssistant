interface AgentSummary {
  id: string;
  name: string;
  description: string;
  model: string;
  baseUrl: string;
  enabled: boolean;
  maxTokens: number;
  totalCalls: number;
  lastUsed: string;
  hasKey: boolean;
}

export class AgentsPage {
  private initialized = false;

  async init(): Promise<void> {
    if (this.initialized) {
      void this.loadAgents();
      return;
    }
    this.initialized = true;
    document.getElementById("agent-add-btn")?.addEventListener("click", () => this.openForm(null));
    document.getElementById("agent-form-close")?.addEventListener("click", () => this.closeForm());
    document.getElementById("af-save-btn")?.addEventListener("click", () => void this.saveAgent());
    document.getElementById("af-test-btn")?.addEventListener("click", () => void this.testAgent());
    document.querySelectorAll<HTMLButtonElement>(".chip[data-url]").forEach(chip => {
      chip.addEventListener("click", () => {
        (document.getElementById("af-url") as HTMLInputElement).value = chip.dataset.url ?? "";
        (document.getElementById("af-model") as HTMLInputElement).value = chip.dataset.model ?? "";

        const options = document.getElementById("af-model-options");
        if (options) {
          const models = (chip.dataset.models ?? chip.dataset.model ?? "").split(",").filter(Boolean);
          options.replaceChildren(...models.map(m => {
            const option = document.createElement("option");
            option.value = m;
            return option;
          }));
        }
      });
    });
    await this.loadAgents();
  }

  async loadAgents(): Promise<void> {
    const grid = document.getElementById("agents-grid");
    if (!grid) return;
    try {
      const agents = await window.koko.call<AgentSummary[]>("agents.list");
      grid.replaceChildren();
      if (agents.length === 0) {
        const empty = document.createElement("p");
        empty.className = "agent-empty";
        empty.textContent = 'Немає агентів. Натисни "+ Додати агента" щоб створити першого.';
        grid.append(empty);
        return;
      }
      grid.append(...agents.map(a => this.renderCard(a)));
    } catch (error) {
      grid.replaceChildren();
      const empty = document.createElement("p");
      empty.className = "agent-empty";
      empty.textContent = error instanceof Error ? error.message : String(error);
      grid.append(empty);
    }
  }

  private renderCard(a: AgentSummary): HTMLElement {
    const card = document.createElement("div");
    card.className = "agent-card";

    const header = document.createElement("div");
    header.className = "agent-card-header";

    const left = document.createElement("div");
    left.style.display = "flex";
    left.style.alignItems = "center";
    left.style.gap = "8px";
    const dot = document.createElement("div");
    dot.className = `agent-status-dot${a.enabled ? "" : " disabled"}`;
    const name = document.createElement("span");
    name.className = "agent-name";
    name.textContent = a.name;
    left.append(dot, name);

    const actions = document.createElement("div");
    actions.style.display = "flex";
    actions.style.gap = "6px";
    const editBtn = document.createElement("button");
    editBtn.className = "icon-button";
    editBtn.type = "button";
    editBtn.title = "Редагувати";
    editBtn.textContent = "✎";
    editBtn.addEventListener("click", () => this.openForm(a.id));
    const deleteBtn = document.createElement("button");
    deleteBtn.className = "icon-button";
    deleteBtn.type = "button";
    deleteBtn.title = "Видалити";
    deleteBtn.textContent = "✕";
    deleteBtn.addEventListener("click", () => void this.deleteAgent(a.id));
    actions.append(editBtn, deleteBtn);

    header.append(left, actions);
    card.append(header);

    const model = document.createElement("div");
    model.className = "agent-model";
    model.textContent = a.model || "—";
    card.append(model);

    if (a.description) {
      const desc = document.createElement("div");
      desc.className = "agent-desc";
      desc.textContent = a.description;
      card.append(desc);
    }

    const meta = document.createElement("div");
    meta.className = "agent-meta";
    const keySpan = document.createElement("span");
    keySpan.textContent = a.hasKey ? "🔑 key set" : "⚠ no key";
    const callsSpan = document.createElement("span");
    callsSpan.textContent = `calls: ${a.totalCalls}`;
    const lastSpan = document.createElement("span");
    lastSpan.textContent = `last: ${a.lastUsed}`;
    meta.append(keySpan, callsSpan, lastSpan);
    card.append(meta);

    return card;
  }

  private openForm(id: string | null): void {
    const overlay = document.getElementById("agent-form-overlay")!;
    if (id) {
      window.koko.call<AgentSummary[]>("agents.list").then(agents => {
        const a = agents.find(x => x.id === id);
        if (!a) return;
        (document.getElementById("af-id") as HTMLInputElement).value = a.id;
        (document.getElementById("af-name") as HTMLInputElement).value = a.name;
        (document.getElementById("af-desc") as HTMLInputElement).value = a.description ?? "";
        (document.getElementById("af-url") as HTMLInputElement).value = a.baseUrl ?? "";
        (document.getElementById("af-key") as HTMLInputElement).value = "";
        (document.getElementById("af-model") as HTMLInputElement).value = a.model ?? "";
        (document.getElementById("af-tokens") as HTMLInputElement).value = String(a.maxTokens ?? 4096);
        (document.getElementById("af-enabled") as HTMLInputElement).checked = a.enabled;
        document.getElementById("agent-form-title")!.textContent = `Редагувати: ${a.name}`;
      }).catch(() => undefined);
    } else {
      for (const fieldId of ["af-id", "af-name", "af-desc", "af-url", "af-key", "af-model"])
        (document.getElementById(fieldId) as HTMLInputElement).value = "";
      (document.getElementById("af-tokens") as HTMLInputElement).value = "4096";
      (document.getElementById("af-enabled") as HTMLInputElement).checked = true;
      document.getElementById("agent-form-title")!.textContent = "Новий агент";
    }
    document.getElementById("af-test-result")!.textContent = "";
    document.getElementById("af-model-options")?.replaceChildren();
    overlay.style.display = "flex";
  }

  private closeForm(): void {
    document.getElementById("agent-form-overlay")!.style.display = "none";
  }

  private async saveAgent(): Promise<void> {
    const id = (document.getElementById("af-id") as HTMLInputElement).value;
    const payload = {
      id: id || undefined,
      name: (document.getElementById("af-name") as HTMLInputElement).value,
      description: (document.getElementById("af-desc") as HTMLInputElement).value,
      baseUrl: (document.getElementById("af-url") as HTMLInputElement).value,
      apiKey: (document.getElementById("af-key") as HTMLInputElement).value,
      model: (document.getElementById("af-model") as HTMLInputElement).value,
      maxTokens: Number((document.getElementById("af-tokens") as HTMLInputElement).value),
      enabled: (document.getElementById("af-enabled") as HTMLInputElement).checked
    };
    try {
      await window.koko.call("agents.save", payload);
      this.closeForm();
      await this.loadAgents();
    } catch (error) {
      document.getElementById("af-test-result")!.textContent = error instanceof Error ? error.message : String(error);
    }
  }

  private async deleteAgent(id: string): Promise<void> {
    if (!window.confirm("Видалити агента?")) return;
    await window.koko.call("agents.delete", { id });
    await this.loadAgents();
  }

  private async testAgent(): Promise<void> {
    const id = (document.getElementById("af-id") as HTMLInputElement).value;
    const resultEl = document.getElementById("af-test-result")!;
    if (!id) {
      resultEl.textContent = "Спочатку збережи агента";
      return;
    }
    resultEl.textContent = "тестую...";
    resultEl.style.color = "";
    try {
      const r = await window.koko.call<{ ok: boolean; response: string }>("agents.test", { id }, 30000);
      resultEl.textContent = r.ok ? "✓ OK — " + r.response : "✗ " + r.response;
      resultEl.style.color = r.ok ? "var(--accent)" : "var(--error)";
    } catch (error) {
      resultEl.textContent = "✗ " + (error instanceof Error ? error.message : String(error));
      resultEl.style.color = "var(--error)";
    }
  }
}
