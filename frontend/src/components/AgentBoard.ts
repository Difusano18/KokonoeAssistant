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
  private readonly tasks = document.getElementById("agent-tasks")!;

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
    this.tasks.replaceChildren(...snapshot.tasks.slice(0, 8).map(task => this.renderTask(task)));
    if (!snapshot.tasks.length)
      this.tasks.append(Object.assign(document.createElement("p"), { className: "agent-empty", textContent: "No active tasks." }));
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
