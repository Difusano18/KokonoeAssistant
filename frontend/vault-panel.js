(function () {
  "use strict";
  const state = document.getElementById("vault-state");
  const notes = document.getElementById("vault-notes");
  const folders = document.getElementById("vault-folders");
  const path = document.getElementById("vault-path");
  const recent = document.getElementById("vault-recent");
  const meta = document.getElementById("vault-meta");
  const refresh = document.getElementById("vault-refresh");

  function noteRow(note) {
    const row = document.createElement("div");
    row.className = "vault-note";
    const title = document.createElement("span");
    title.textContent = note.path;
    title.title = note.path;
    const time = document.createElement("time");
    time.textContent = new Date(note.modifiedAt).toLocaleDateString([], { month: "short", day: "2-digit" });
    row.append(title, time);
    return row;
  }

  function render(status) {
    if (!status) return;
    state.className = "vault-state " + (status.available ? "online" : "offline");
    state.querySelector("span").textContent = status.available ? "ONLINE" : "UNAVAILABLE";
    notes.textContent = String(status.noteCount || 0);
    folders.textContent = String(status.folderCount || 0);
    path.textContent = status.path || "Vault path unavailable.";
    const recentNotes = status.recentNotes || [];
    recent.replaceChildren(...recentNotes.map(noteRow));
    if (!recentNotes.length) {
      const empty = document.createElement("p");
      empty.className = "agent-empty";
      empty.textContent = "No recent notes.";
      recent.append(empty);
    }
    const scanned = new Date(status.scannedAt).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" });
    meta.textContent = "scan " + status.scanMs + " ms / " + scanned;
  }

  function fail(error) {
    state.className = "vault-state offline";
    state.querySelector("span").textContent = "ERROR";
    meta.textContent = error instanceof Error ? error.message : String(error);
  }

  async function load(method) {
    refresh.disabled = true;
    try { render(await window.koko.call(method)); }
    catch (error) { fail(error); }
    finally { refresh.disabled = false; }
  }

  refresh.addEventListener("click", () => load("vault.refresh"));
  window.koko.on("vault.status", render);
  window.kokoVaultPanel = {
    connect: function () { return load("vault.status"); },
    render
  };
})();
