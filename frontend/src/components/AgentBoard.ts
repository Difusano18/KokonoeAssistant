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
  runnerActive?: boolean;
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
  private readonly runnerStart = document.getElementById("agent-runner-start") as HTMLButtonElement | null;
  private readonly runnerStop = document.getElementById("agent-runner-stop") as HTMLButtonElement | null;
  private pollHandle = 0;

  constructor() {
    window.koko.on("agent.activity", payload => this.render((payload as { snapshot: AgentSnapshot }).snapshot));
    window.koko.on("agent.completed", payload => this.render((payload as { snapshot: AgentSnapshot }).snapshot));
    this.sideTasks.addEventListener("click", event => this.handleTaskClick(event));
    this.panelTasks?.addEventListener("click", event => this.handleTaskClick(event));
    this.taskForm?.addEventListener("submit", event => {
      event.preventDefault();
      void this.startTask();
    });
    this.runnerStart?.addEventListener("click", () => void this.setRunner(true));
    this.runnerStop?.addEventListener("click", () => void this.setRunner(false));
  }

  async connect(): Promise<void> {
    try {
      await this.refresh();
      this.setTaskControls(true, "Agent bridge ready.");
      this.setRunnerControls(true);
      this.startPolling();
    } catch (error) {
      this.activity.querySelector("strong")!.textContent = "Agent bridge unavailable";
      this.activity.querySelector("p")!.textContent = error instanceof Error ? error.message : String(error);
      this.setTaskControls(false, error instanceof Error ? error.message : String(error));
      this.setRunnerControls(false);
    }
  }

  private startPolling(): void {
    if (this.pollHandle)
      return;
    this.pollHandle = window.setInterval(() => {
      this.refresh().catch(error => this.setTaskStatus(error instanceof Error ? error.message : String(error)));
    }, 8000);
  }

  private async refresh(): Promise<void> {
    this.render(await window.koko.call("agent.snapshot") as AgentSnapshot);
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

  private setRunnerControls(enabled: boolean, active?: boolean): void {
    if (this.runnerStart)
      this.runnerStart.disabled = !enabled || active === true;
    if (this.runnerStop)
      this.runnerStop.disabled = !enabled || active === false;
  }

  private async setRunner(active: boolean): Promise<void> {
    this.setRunnerControls(false);
    this.setTaskStatus(active ? "Starting runner..." : "Stopping runner...");
    try {
      const result = await window.koko.call<{ active: boolean; snapshot: AgentSnapshot }>(
        active ? "agent.runner.start" : "agent.runner.stop",
        null,
        10000
      );
      this.render(result.snapshot);
      this.setTaskStatus(result.active ? "Runner active." : "Runner stopped.");
    } catch (error) {
      this.setTaskStatus(error instanceof Error ? error.message : String(error));
      this.setRunnerControls(true);
    }
  }

  private handleTaskClick(event: Event): void {
    const button = (event.target as HTMLElement).closest<HTMLButtonElement>("button[data-agent-cancel]");
    if (!button)
      return;
    event.preventDefault();
    const taskId = button.dataset.agentCancel ?? "";
    if (!taskId)
      return;
    button.disabled = true;
    void this.cancelTask(taskId);
  }

  private async cancelTask(taskId: string): Promise<void> {
    this.setTaskStatus(`Canceling ${taskId}...`);
    try {
      const result = await window.koko.call<{ taskId: string; snapshot: AgentSnapshot }>(
        "agent.cancel",
        { taskId },
        10000
      );
      this.render(result.snapshot);
      this.setTaskStatus(`Canceled ${result.taskId}.`);
    } catch (error) {
      this.setTaskStatus(error instanceof Error ? error.message : String(error));
      await this.refresh().catch(() => undefined);
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
    this.setRunnerControls(true, snapshot.runnerActive === true);
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
    if (task.status === "Pending" || task.status === "Running") {
      const cancel = document.createElement("button");
      cancel.className = "agent-task-cancel";
      cancel.type = "button";
      cancel.textContent = "x";
      cancel.title = `Cancel ${task.id}`;
      cancel.ariaLabel = `Cancel task ${task.id}`;
      cancel.dataset.agentCancel = task.id;
      head.append(cancel);
    }
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
