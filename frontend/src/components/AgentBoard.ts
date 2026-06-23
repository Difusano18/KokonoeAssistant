type AgentStatus = "Pending" | "Running" | "Completed" | "Failed" | "Canceled";

interface AgentStep { status: AgentStatus; title?: string; kind?: string; }
interface AgentTask { id: string; objective: string; status: AgentStatus; steps: AgentStep[]; }
interface AgentActivity { phase: string; tool: string; focus: string; thought: string; }
interface AgentSnapshot {
  maxParallel: number;
  runningSteps: number;
  activity: AgentActivity;
  tasks: AgentTask[];
}

export class AgentBoardController {
  private readonly activity = document.getElementById("agent-activity")!;
  private readonly summary = document.getElementById("agent-summary")!;
  private readonly sideTasks = document.getElementById("agent-tasks")!;
  private readonly panelTasks = document.getElementById("tasks-list");
  private readonly panelCount = document.getElementById("tasks-panel-count");
  private readonly panelRunning = document.getElementById("tasks-panel-running");
  private readonly panelPhase = document.getElementById("tasks-panel-phase");
  private readonly panelThought = document.getElementById("tasks-panel-thought");

  constructor() {
    window.koko.on("agent.activity", payload => this.render((payload as { snapshot: AgentSnapshot }).snapshot));
    window.koko.on("agent.completed", payload => this.render((payload as { snapshot: AgentSnapshot }).snapshot));
  }

  async connect(): Promise<void> {
    try {
      this.render(await window.koko.call("agent.snapshot") as AgentSnapshot);
    } catch (error) {
      this.activity.querySelector("strong")!.textContent = "Agent bridge unavailable";
      this.activity.querySelector("p")!.textContent = error instanceof Error ? error.message : String(error);
    }
  }

  private render(snapshot: AgentSnapshot): void {
    const current = snapshot.activity;
    this.activity.replaceChildren(
      Object.assign(document.createElement("strong"), { textContent: `${current.phase} / ${current.tool}` }),
      Object.assign(document.createElement("p"), { textContent: current.thought || current.focus })
    );
    this.summary.replaceChildren(
      Object.assign(document.createElement("span"), { textContent: `${snapshot.tasks.length} tasks` }),
      Object.assign(document.createElement("span"), { textContent: `${snapshot.runningSteps} / ${snapshot.maxParallel} running` })
    );
    const rendered = snapshot.tasks.slice(0, 8).map(task => this.renderTask(task));
    this.sideTasks.replaceChildren(...rendered.map(task => task.cloneNode(true) as HTMLElement));
    this.panelTasks?.replaceChildren(...rendered);
    if (!snapshot.tasks.length) {
      const empty = Object.assign(document.createElement("p"), { className: "agent-empty", textContent: "No active tasks." });
      this.sideTasks.append(empty.cloneNode(true));
      this.panelTasks?.append(empty);
    }

    if (this.panelCount)
      this.panelCount.textContent = String(snapshot.tasks.length);
    if (this.panelRunning)
      this.panelRunning.textContent = `${snapshot.runningSteps} / ${snapshot.maxParallel}`;
    if (this.panelPhase)
      this.panelPhase.textContent = current.phase || "idle";
    if (this.panelThought)
      this.panelThought.textContent = current.thought || current.focus || "No active task.";
  }

  private renderTask(task: AgentTask): HTMLElement {
    const root = document.createElement("article");
    root.className = "agent-task";
    const head = document.createElement("div");
    head.className = "agent-task-head";
    head.append(
      Object.assign(document.createElement("strong"), { textContent: task.objective }),
      Object.assign(document.createElement("span"), { className: `agent-status ${task.status.toLowerCase()}`, textContent: task.status })
    );
    const steps = document.createElement("div");
    steps.className = "agent-steps";
    for (const step of task.steps)
      steps.append(Object.assign(document.createElement("i"), { className: `agent-step ${step.status.toLowerCase()}` }));
    root.append(head, steps);
    return root;
  }
}
