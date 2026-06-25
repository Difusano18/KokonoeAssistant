import { buildArtifactCard, type ArtifactSummary } from "./ArtifactCard";

export class ArtifactsPanelController {
  private initialized = false;

  async init(): Promise<void> {
    if (this.initialized) {
      void this.loadArtifacts();
      return;
    }
    this.initialized = true;
    window.koko.on("artifact.new", payload => this.prependArtifact(payload as ArtifactSummary));
    await this.loadArtifacts();
  }

  private async loadArtifacts(): Promise<void> {
    const list = document.getElementById("artifacts-list");
    if (!list) return;
    try {
      const artifacts = await window.koko.call<ArtifactSummary[]>("artifacts.list");
      if (artifacts.length === 0) return;
      list.replaceChildren(...artifacts.map(buildArtifactCard));
    } catch (error) {
      const empty = document.createElement("p");
      empty.className = "agent-empty";
      empty.textContent = error instanceof Error ? error.message : String(error);
      list.replaceChildren(empty);
    }
  }

  private prependArtifact(artifact: ArtifactSummary): void {
    const list = document.getElementById("artifacts-list");
    if (!list) return;
    list.querySelector(".agent-empty")?.remove();
    list.prepend(buildArtifactCard(artifact));
  }
}
