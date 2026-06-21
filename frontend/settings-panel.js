(function () {
  "use strict";
  const drawer = document.getElementById("settings-drawer");
  const backdrop = document.getElementById("settings-backdrop");
  const openButton = document.getElementById("settings-open");
  const closeButton = document.getElementById("settings-close");
  const form = document.getElementById("settings-form");
  const saveButton = document.getElementById("settings-save");
  const status = document.getElementById("settings-status");
  const segment = document.getElementById("autonomy-segment");
  const color = document.getElementById("matrix-color");
  const colorText = document.getElementById("matrix-color-text");
  const credentials = document.getElementById("credential-grid");
  let available = false;
  let autonomy = 2;
  const fields = {
    spontaneousEnabled: "spontaneous-enabled", spontaneousIntervalMins: "spontaneous-mins",
    neuralGovernorEnabled: "neural-governor", screenAwarenessEnabled: "screen-enabled",
    screenAwarenessSendComments: "screen-comments", screenAwarenessIntervalMins: "screen-interval",
    screenAwarenessCommentCooldownMins: "screen-cooldown", systemOverlordEnabled: "overlord-enabled",
    voiceInputEnabled: "voice-enabled", ttsEnabled: "tts-enabled", wearBridgeEnabled: "wear-enabled",
    wearBridgeIncludePromptContext: "wear-context", minimizeToTray: "tray-enabled"
  };

  function show() {
    drawer.classList.add("open"); backdrop.classList.add("open"); drawer.setAttribute("aria-hidden", "false");
    if (available) load(); else status.textContent = "Host connection required.";
  }
  function hide() {
    drawer.classList.remove("open"); backdrop.classList.remove("open"); drawer.setAttribute("aria-hidden", "true");
  }
  function setAutonomy(value) {
    autonomy = Number(value);
    for (const button of segment.querySelectorAll("button"))
      button.classList.toggle("selected", Number(button.dataset.value) === autonomy);
  }
  function fill(snapshot) {
    const values = snapshot.values || {};
    setAutonomy(values.proactiveAutonomyLevel ?? 2);
    for (const [name, id] of Object.entries(fields)) {
      const input = document.getElementById(id);
      if (input.type === "checkbox") input.checked = Boolean(values[name]); else input.value = String(values[name] ?? "");
    }
    color.value = /^#[0-9a-f]{6}$/i.test(values.matrixColor || "") ? values.matrixColor : "#6366F1";
    colorText.textContent = color.value.toUpperCase();
    renderCredentials(snapshot.credentials || {});
  }
  function renderCredentials(values) {
    const labels = { telegramBot: "Telegram bot", telegramUser: "Telegram user", openAi: "OpenAI", claude: "Claude", ollama: "Ollama" };
    credentials.replaceChildren(...Object.entries(labels).map(([key, label]) => {
      const row = document.createElement("div"); row.className = "credential";
      const name = document.createElement("span"); name.textContent = label;
      const state = document.createElement("output"); state.textContent = values[key] ? "CONFIGURED" : "NOT SET"; state.className = values[key] ? "configured" : "";
      row.append(name, state); return row;
    }));
  }
  async function load() {
    saveButton.disabled = true; status.textContent = "Loading...";
    try { fill(await window.koko.call("settings.get")); status.textContent = "Settings synchronized."; saveButton.disabled = false; }
    catch (error) { status.textContent = error instanceof Error ? error.message : String(error); }
  }
  function read() {
    const values = { proactiveAutonomyLevel: autonomy, matrixColor: color.value.toUpperCase() };
    for (const [name, id] of Object.entries(fields)) {
      const input = document.getElementById(id); values[name] = input.type === "checkbox" ? input.checked : Number(input.value);
    }
    return values;
  }
  segment.addEventListener("click", event => { const button = event.target.closest("button[data-value]"); if (button) setAutonomy(button.dataset.value); });
  color.addEventListener("input", () => { colorText.textContent = color.value.toUpperCase(); });
  openButton.addEventListener("click", show); closeButton.addEventListener("click", hide); backdrop.addEventListener("click", hide);
  form.addEventListener("submit", async event => {
    event.preventDefault(); saveButton.disabled = true; status.textContent = "Saving...";
    try {
      const result = await window.koko.call("settings.update", read()); fill(result.settings);
      status.textContent = result.restartRequired ? "Saved. Restart required for device service changes." : "Saved.";
    } catch (error) { status.textContent = error instanceof Error ? error.message : String(error); }
    finally { saveButton.disabled = false; }
  });
  window.kokoSettingsPanel = { setAvailable: value => { available = Boolean(value); saveButton.disabled = !available; }, open: show, close: hide, fill };
  if (window.location.hash === "#settings") show();
})();
