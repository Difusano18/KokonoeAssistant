export class MotionController {
  private readonly shell = document.querySelector(".shell")!;
  private readonly runtime = document.getElementById("runtime")!;
  private readonly dot = document.getElementById("runtime-dot")!;
  private readonly footer = document.getElementById("footer-state")!;

  setHostState(state: "ready" | "busy" | "error" | "preview"): void {
    const labels = {
      ready: ["Runtime linked", "● HOST READY"], busy: ["Kokonoe is responding", "● STREAMING"],
      error: ["Host error", "● HOST ERROR"], preview: ["Static preview", "● HOST ONLY"]
    } as const;
    this.runtime.textContent = labels[state][0];
    this.footer.textContent = labels[state][1];
    this.dot.className = `runtime-dot ${state === "preview" ? "" : state}`;
    this.shell.classList.toggle("is-busy", state === "busy");
  }
}
