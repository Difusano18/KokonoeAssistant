(function () {
  "use strict";
  const refresh = document.getElementById("telegram-refresh");
  let timer = 0;

  function renderChannel(id, channel) {
    const root = document.getElementById(id);
    const online = channel.state === "listening" || channel.state === "connected";
    const pending = channel.state === "connecting" || channel.state === "idle";
    root.className = "telegram-channel " + (online ? "online" : channel.state === "error" ? "error" : pending ? "pending" : "");
    root.querySelector("output").textContent = String(channel.state || "unknown").replaceAll("_", " ");
    const parts = [channel.enabled ? "enabled" : "disabled", channel.configured ? "configured" : "not configured"];
    if (channel.account) parts.push(channel.account);
    if (channel.lastActivityAt) {
      const time = new Date(channel.lastActivityAt).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" });
      parts.push((channel.lastActivity || "activity") + " " + time);
    }
    if (channel.lastError) parts.push(channel.lastError);
    const detail = root.querySelector(".telegram-detail");
    detail.textContent = parts.join(" / ");
    detail.classList.toggle("telegram-error", Boolean(channel.lastError));
  }

  function render(status) {
    if (!status) return;
    renderChannel("telegram-bot", status.bot || {});
    renderChannel("telegram-user", status.user || {});
  }

  async function load() {
    refresh.disabled = true;
    try { render(await window.koko.call("telegram.status")); }
    catch (error) {
      renderChannel("telegram-bot", { state: "error", lastError: error instanceof Error ? error.message : String(error) });
      renderChannel("telegram-user", { state: "error", lastError: "Status unavailable" });
    } finally { refresh.disabled = false; }
  }

  refresh.addEventListener("click", load);
  window.koko.on("telegram.status", render);
  window.kokoTelegramPanel = {
    connect: async function () {
      await load();
      if (!timer) timer = window.setInterval(load, 15000);
    },
    render
  };
})();
