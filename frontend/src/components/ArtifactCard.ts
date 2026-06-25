export interface ArtifactSummary {
  id: string;
  title: string;
  kind: string;
  previewText?: string | null;
  sourceUrl?: string | null;
  sizeLabel: string;
  createdAt: string;
}

const KIND_ICON: Record<string, string> = {
  markdown: "📄", html: "🌐", csv: "📊", json: "{ }",
  patch: "🔧", note: "📝", plaintext: "📃", image: "🖼", pdf: "📕"
};

export function buildArtifactCard(a: ArtifactSummary): HTMLElement {
  const card = document.createElement("div");
  card.className = "artifact-card";
  card.dataset.id = a.id;

  const header = document.createElement("div");
  header.className = "artifact-card-header";

  const icon = document.createElement("span");
  icon.className = "artifact-icon";
  icon.textContent = KIND_ICON[a.kind] ?? "📎";

  const info = document.createElement("div");
  info.className = "artifact-info";
  const title = document.createElement("strong");
  title.className = "artifact-title";
  title.textContent = a.title;
  const meta = document.createElement("span");
  meta.className = "artifact-meta";
  meta.textContent = `${a.kind} · ${a.sizeLabel} · ${a.createdAt}`;
  info.append(title, meta);

  const openBtn = document.createElement("button");
  openBtn.className = "artifact-open-btn";
  openBtn.type = "button";
  openBtn.title = "Відкрити в провіднику";
  openBtn.textContent = "↗";
  openBtn.addEventListener("click", () => void window.koko.call("artifacts.open", { id: a.id }));

  header.append(icon, info, openBtn);
  card.append(header);

  if (a.previewText) {
    const preview = document.createElement("div");
    preview.className = "artifact-preview";
    preview.textContent = a.previewText.slice(0, 200);
    card.append(preview);
  }

  if (a.sourceUrl && /^https?:\/\//i.test(a.sourceUrl)) {
    const link = document.createElement("a");
    link.className = "artifact-source";
    link.href = a.sourceUrl;
    link.target = "_blank";
    link.rel = "noopener noreferrer";
    link.textContent = a.sourceUrl.slice(0, 60);
    card.append(link);
  }

  return card;
}
