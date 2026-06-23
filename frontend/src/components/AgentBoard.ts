type AgentStatus = "Pending" | "Running" | "Completed" | "Failed" | "Canceled";

interface AgentStep {
  status: AgentStatus;
  title?: string;
  kind?: string;
  result?: string;
  error?: string;
  startedAt?: string;
  finishedAt?: string;
}
interface AgentTask {
  id: string;
  objective: string;
  status: AgentStatus;
  priority?: number;
  updatedAt?: string;
  completionNotice?: string;
  nextQuestion?: string;
  steps: AgentStep[];
}
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
  private readonly taskForm = document.getElementById("agent-task-form") as HTMLFormElement | null;
  private readonly objectiveInput = document.getElementById("agent-task-objective") as HTMLInputElement | null;
  private readonly startButton = document.getElementById("agent-task-start") as HTMLButtonElement | null;
  private readonly taskStatus = document.getElementById("agent-task-status");

  constructor() {
    window.koko.on("agent.activity", payload => this.render((payload as { snapshot: AgentSnapshot }).snapshot));
    window.koko.on("agent.completed", payload => this.render((payload as { snapshot: AgentSnapshot }).snapshot));
    this.taskForm?.addEventListener("submit", event => {
      event.preventDefault();
      void this.startTask();
    });
  }

  async connect(): Promise<void> {
    try {
      this.render(await window.koko.call("agent.snapshot") as AgentSnapshot);
      this.setTaskControls(true, "Agent bridge ready.");
    } catch (error) {
      this.activity.querySelector("strong")!.textContent = "Agent bridge unavailable";
      this.activity.querySelector("p")!.textContent = error instanceof Error ? error.message : String(error);
      this.setTaskControls(false, error instanceof Error ? error.message : String(error));
    }
  }

  private async startTask(): Promise<void> {
    const objective = this.objectiveInput?.value.trim() ?? "";
    if (!objective) {
      this.setTaskStatus("Write a real objective first.");
      this.objectiveInput?.focus();
      return;
    }

    this.setTaskControls(false, "Creating task...");
    try {
      const result = await window.koko.call<{ taskId: string; snapshot: AgentSnapshot }>(
        "agent.start",
        { objective, priority: 6, start: true },
        10000
      );
      this.objectiveInput!.value = "";
      this.render(result.snapshot);
      this.setTaskControls(true, `Started ${result.taskId}.`);
    } catch (error) {
      this.setTaskControls(true, error instanceof Error ? error.message : String(error));
    }
  }

  private setTaskControls(enabled: boolean, status: string): void {
    if (this.objectiveInput)
      this.objectiveInput.disabled = !enabled;
    if (this.startButton)
      this.startButton.disabled = !enabled;
    this.setTaskStatus(status);
  }

  private setTaskStatus(status: string): void {
    if (this.taskStatus)
      this.taskStatus.textContent = status;
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
    root.className = `agent-task ${task.status.toLowerCase()}`;
    const head = document.createElement("div");
    head.className = "agent-task-head";
    head.append(
      Object.assign(document.createElement("strong"), { textContent: task.objective }),
      Object.assign(document.createElement("span"), { className: `agent-status ${task.status.toLowerCase()}`, textContent: task.status })
    );
    const steps = document.createElement("div");
    steps.className = "agent-steps";
    for (const step of task.steps)
      steps.append(Object.assign(document.createElement("i"), {
        className: `agent-step ${step.status.toLowerCase()}`,
        title: this.describeStep(step)
      }));
    const detail = Object.assign(document.createElement("p"), {
      className: "agent-task-detail",
      textContent: this.summarizeTask(task)
    });
    root.title = `${task.id} / p${task.priority ?? 5}`;
    root.append(head, steps, detail);
    return root;
  }

  private summarizeTask(task: AgentTask): string {
    const failed = task.steps.find(step => step.status === "Failed");
    if (failed)
      return this.trim(`failed ${failed.kind ?? "step"}: ${failed.error || failed.result || failed.title || "no diagnostic"}`, 220);

    const running = task.steps.find(step => step.status === "Running");
    if (running)
      return this.trim(`running ${running.kind ?? "step"}: ${running.title || running.result || "working"}`, 220);

    if (task.status === "Completed" && task.completionNotice)
      return this.trim(task.completionNotice, 220);

    if (task.nextQuestion)
      return this.trim(`needs input: ${task.nextQuestion}`, 220);

    const completed = [...task.steps].reverse().find(step => step.status === "Completed" && (step.result || step.title));
    if (completed)
      return this.trim(`${completed.kind ?? "step"}: ${completed.result || completed.title}`, 220);

    return `p${task.priority ?? 5} / ${task.id}`;
  }

  private describeStep(step: AgentStep): string {
    const body = step.error || step.result || step.title || step.kind || step.status;
    return this.trim(body, 160);
  }

  private trim(value: string, max: number): string {
    const normalized = value.replace(/\s+/g, " ").trim();
    return normalized.length > max ? `${normalized.slice(0, max - 3)}...` : normalized;
  }
}
