type AgentStatus = "Pending" | "Running" | "Completed" | "Failed" | "Canceled";

interface AgentStep {
  id?: string;
  order?: number;
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
interface AgentLane {
  slot: number;
  taskId: string;
  stepId: string;
  objective: string;
  stepTitle: string;
  kind: string;
  startedAt?: string;
  elapsedSeconds?: number;
}
interface AgentSnapshot {
  maxParallel: number;
  runningSteps: number;
  pendingTasks?: number;
  completedTasks?: number;
  failedTasks?: number;
  canceledTasks?: number;
  runnerActive?: boolean;
  activity: AgentActivity;
  activeLanes?: AgentLane[];
  taskCount?: number;
  tasks: AgentTask[];
}

type TaskFilter = "all" | "running" | "failed" | "completed";

export class AgentBoardController {
  private readonly panelTasks = document.getElementById("tasks-list");
  private readonly panelCount = document.getElementById("tasks-panel-count");
  private readonly panelRunning = document.getElementById("tasks-panel-running");
  private readonly panelPhase = document.getElementById("tasks-panel-phase");
  private readonly panelThought = document.getElementById("tasks-panel-thought");
  private readonly taskForm = document.getElementById("agent-task-form") as HTMLFormElement | null;
  private readonly objectiveInput = document.getElementById("agent-task-objective") as HTMLTextAreaElement | null;
  private readonly startButton = document.getElementById("agent-task-start") as HTMLButtonElement | null;
  private readonly taskStatus = document.getElementById("agent-task-status");
  private readonly runnerStart = document.getElementById("agent-runner-start") as HTMLButtonElement | null;
  private readonly runnerStop = document.getElementById("agent-runner-stop") as HTMLButtonElement | null;
  private readonly parallelInput = document.getElementById("agent-parallel-input") as HTMLInputElement | null;
  private readonly parallelValue = document.getElementById("agent-parallel-value");
  private readonly lanesPanel = document.getElementById("agent-active-lanes");
  private readonly inspectorPanel = document.getElementById("agent-task-inspector");
  private readonly filterSegment = document.getElementById("task-filter-segment");
  private readonly clearOldButton = document.getElementById("clear-old-tasks") as HTMLButtonElement | null;
  private pollHandle = 0;
  private filter: TaskFilter = "all";
  private lastTasks: AgentTask[] = [];
  private selectedTaskId = "";

  constructor() {
    window.koko.on("agent.activity", payload => this.render((payload as { snapshot: AgentSnapshot }).snapshot));
    window.koko.on("agent.completed", payload => this.render((payload as { snapshot: AgentSnapshot }).snapshot));
    this.panelTasks?.addEventListener("click", event => this.handleTaskClick(event));
    this.panelTasks?.addEventListener("keydown", event => this.handleTaskKeydown(event));
    this.taskForm?.addEventListener("submit", event => {
      event.preventDefault();
      void this.startTask();
    });
    this.objectiveInput?.addEventListener("keydown", event => {
      if ((event.ctrlKey || event.metaKey) && event.key === "Enter") {
        event.preventDefault();
        this.taskForm?.requestSubmit();
      }
    });
    this.runnerStart?.addEventListener("click", () => void this.setRunner(true));
    this.runnerStop?.addEventListener("click", () => void this.setRunner(false));
    this.parallelInput?.addEventListener("change", () => void this.configureParallel());
    this.filterSegment?.addEventListener("click", event => {
      const button = (event.target as HTMLElement).closest<HTMLButtonElement>("button[data-filter]");
      if (button) this.setFilter(button.dataset.filter as TaskFilter);
    });
    this.clearOldButton?.addEventListener("click", () => void this.clearOldTasks());
    this.inspectorPanel?.addEventListener("click", event => this.handleInspectorClick(event));
  }

  async connect(): Promise<void> {
    try {
      await this.refresh();
      this.setTaskControls(true, "Agent bridge ready.");
      this.setRunnerControls(true);
      if (this.parallelInput)
        this.parallelInput.disabled = false;
      this.startPolling();
    } catch (error) {
      this.setTaskControls(false, error instanceof Error ? error.message : String(error));
      this.setRunnerControls(false);
      if (this.parallelInput)
        this.parallelInput.disabled = true;
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
      const result = await window.koko.call<{ taskId?: string; taskIds?: string[]; count?: number; snapshot: AgentSnapshot }>(
        "agent.start_many",
        { objective, priority: 6, start: true },
        10000
      );
      this.objectiveInput!.value = "";
      this.render(result.snapshot);
      const count = result.count ?? result.taskIds?.length ?? 1;
      this.setTaskControls(true, count > 1 ? `Queued ${count} tasks.` : `Queued ${result.taskId ?? result.taskIds?.[0] ?? "task"}.`);
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
    if (button) {
      event.preventDefault();
      const taskId = button.dataset.agentCancel ?? "";
      if (!taskId)
        return;
      button.disabled = true;
      void this.cancelTask(taskId);
      return;
    }

    const taskCard = (event.target as HTMLElement).closest<HTMLElement>("[data-agent-task]");
    if (!taskCard)
      return;
    this.selectedTaskId = taskCard.dataset.agentTask ?? "";
    this.renderTaskList();
    this.renderInspector();
  }

  private handleTaskKeydown(event: KeyboardEvent): void {
    if (event.key !== "Enter" && event.key !== " ")
      return;
    const taskCard = (event.target as HTMLElement).closest<HTMLElement>("[data-agent-task]");
    if (!taskCard)
      return;
    event.preventDefault();
    this.selectedTaskId = taskCard.dataset.agentTask ?? "";
    this.renderTaskList();
    this.renderInspector();
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
    this.lastTasks = snapshot.tasks;
    if (this.selectedTaskId && !this.lastTasks.some(task => task.id === this.selectedTaskId))
      this.selectedTaskId = "";
    if (!this.selectedTaskId)
      this.selectedTaskId = snapshot.tasks.find(task => task.status === "Running")?.id
        ?? snapshot.tasks.find(task => task.status === "Pending")?.id
        ?? snapshot.tasks[0]?.id
        ?? "";
    this.renderTaskList();
    this.renderInspector();

    if (this.panelCount)
      this.panelCount.textContent = String(snapshot.taskCount ?? snapshot.tasks.length);
    if (this.panelRunning)
      this.panelRunning.textContent = `${snapshot.runningSteps} / ${snapshot.maxParallel}`;
    this.renderLanes(snapshot);
    this.renderQueueSummary(snapshot);
    if (this.parallelInput && this.parallelInput.value !== String(snapshot.maxParallel))
      this.parallelInput.value = String(snapshot.maxParallel);
    if (this.parallelValue)
      this.parallelValue.textContent = `${snapshot.maxParallel} lanes`;
    if (this.parallelInput)
      this.parallelInput.disabled = !window.koko.available;
    if (this.panelPhase)
      this.panelPhase.textContent = current.phase || "idle";
    if (this.panelThought)
      this.panelThought.textContent = current.thought || current.focus || "No active task.";
    this.setRunnerControls(true, snapshot.runnerActive === true);
    if (this.clearOldButton)
      this.clearOldButton.disabled = false;
  }

  private renderQueueSummary(snapshot: AgentSnapshot): void {
    const parts = [
      `${snapshot.pendingTasks ?? 0} queued`,
      `${snapshot.completedTasks ?? 0} done`,
      `${snapshot.failedTasks ?? 0} failed`
    ];
    if (snapshot.canceledTasks)
      parts.push(`${snapshot.canceledTasks} canceled`);
    const status = snapshot.runnerActive ? `Runner active; ${parts.join(" / ")}.` : `Runner stopped; ${parts.join(" / ")}.`;
    if (this.taskStatus && !this.objectiveInput?.disabled)
      this.taskStatus.textContent = status;
  }

  private renderLanes(snapshot: AgentSnapshot): void {
    if (!this.lanesPanel)
      return;

    const lanes = snapshot.activeLanes ?? [];
    const nodes: HTMLElement[] = [];
    const slotCount = Math.max(snapshot.maxParallel, ...lanes.map(item => item.slot));
    for (let slot = 1; slot <= slotCount; slot++) {
      const lane = lanes.find(item => item.slot === slot);
      const row = document.createElement("article");
      row.className = `agent-lane ${lane ? "active" : "idle"}`;
      const head = document.createElement("div");
      head.className = "agent-lane-head";
      head.append(
        Object.assign(document.createElement("span"), { textContent: `lane ${slot}` }),
        Object.assign(document.createElement("b"), { textContent: lane?.kind ?? "idle" })
      );
      const body = Object.assign(document.createElement("p"), {
        textContent: lane
          ? this.trim(`${lane.stepTitle || lane.kind}: ${lane.objective}`, 180)
          : "No task in this slot."
      });
      const meta = Object.assign(document.createElement("small"), {
        textContent: lane ? `${lane.taskId} / ${this.formatElapsed(lane.elapsedSeconds)}` : "available"
      });
      row.append(head, body, meta);
      nodes.push(row);
    }
    this.lanesPanel.replaceChildren(...nodes);
  }

  private async configureParallel(): Promise<void> {
    if (!this.parallelInput)
      return;
    const maxParallel = Number(this.parallelInput.value);
    this.parallelInput.disabled = true;
    this.setTaskStatus(`Setting ${maxParallel} lane(s)...`);
    try {
      const result = await window.koko.call<{ maxParallel: number; snapshot: AgentSnapshot }>(
        "agent.runner.configure",
        { maxParallel },
        10000
      );
      this.render(result.snapshot);
      this.setTaskStatus(`Parallel lanes set to ${result.maxParallel}.`);
    } catch (error) {
      this.setTaskStatus(error instanceof Error ? error.message : String(error));
      await this.refresh().catch(() => undefined);
    } finally {
      this.parallelInput.disabled = !window.koko.available;
    }
  }

  private renderTaskList(): void {
    const filtered = this.lastTasks.filter(task => {
      switch (this.filter) {
        case "running": return task.status === "Running" || task.status === "Pending";
        case "failed": return task.status === "Failed";
        case "completed": return task.status === "Completed";
        default: return true;
      }
    });
    this.panelTasks?.replaceChildren(...filtered.map(task => this.renderTask(task)));
    if (!filtered.length) {
      const empty = Object.assign(document.createElement("p"), {
        className: "agent-empty",
        textContent: this.filter === "all" ? "No active tasks." : "No tasks match this filter."
      });
      this.panelTasks?.append(empty);
    }
  }

  private setFilter(filter: TaskFilter): void {
    this.filter = filter;
    for (const button of this.filterSegment?.querySelectorAll<HTMLButtonElement>("button") ?? [])
      button.classList.toggle("selected", button.dataset.filter === filter);
    this.renderTaskList();
  }

  private async clearOldTasks(): Promise<void> {
    if (this.clearOldButton)
      this.clearOldButton.disabled = true;
    try {
      const result = await window.koko.call<{ removed: number; snapshot: AgentSnapshot }>("agent.clear_completed", null, 10000);
      this.render(result.snapshot);
      this.setTaskStatus(`Cleared ${result.removed} old task(s).`);
    } catch (error) {
      this.setTaskStatus(error instanceof Error ? error.message : String(error));
    } finally {
      if (this.clearOldButton)
        this.clearOldButton.disabled = !window.koko.available;
    }
  }

  private renderTask(task: AgentTask): HTMLElement {
    const root = document.createElement("article");
    root.className = `agent-task ${task.status.toLowerCase()}${task.id === this.selectedTaskId ? " selected" : ""}`;
    root.dataset.agentTask = task.id;
    root.tabIndex = 0;
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

  private renderInspector(): void {
    if (!this.inspectorPanel)
      return;

    const task = this.lastTasks.find(item => item.id === this.selectedTaskId);
    if (!task) {
      this.inspectorPanel.replaceChildren(Object.assign(document.createElement("p"), {
        className: "agent-empty",
        textContent: "Select a task to inspect evidence."
      }));
      return;
    }

    const completed = task.steps.filter(step => step.status === "Completed").length;
    const failed = task.steps.filter(step => step.status === "Failed").length;
    const total = Math.max(1, task.steps.length);
    const progress = Math.round((completed / total) * 100);
    const running = task.steps.find(step => step.status === "Running");
    const latestEvidence = [...task.steps].reverse().find(step => step.result || step.error);

    const head = document.createElement("div");
    head.className = "task-inspector-head";
    const title = document.createElement("div");
    title.append(
      Object.assign(document.createElement("span"), { textContent: `${task.id} / p${task.priority ?? 5}` }),
      Object.assign(document.createElement("strong"), { textContent: task.objective })
    );
    const copy = document.createElement("button");
    copy.type = "button";
    copy.className = "agent-inspector-copy";
    copy.dataset.copyReport = task.id;
    copy.textContent = "Copy report";
    head.append(title, copy);

    const metrics = document.createElement("div");
    metrics.className = "task-inspector-metrics";
    metrics.append(
      this.metric("Status", task.status),
      this.metric("Progress", `${completed}/${task.steps.length} (${progress}%)`),
      this.metric("Failed", String(failed)),
      this.metric("Updated", this.formatDate(task.updatedAt))
    );

    const progressBar = document.createElement("div");
    progressBar.className = "task-progress";
    const progressFill = document.createElement("i");
    progressFill.style.width = `${progress}%`;
    progressBar.append(progressFill);

    const focus = Object.assign(document.createElement("p"), {
      className: "task-inspector-focus",
      textContent: running
        ? `Active: ${running.kind ?? "step"} - ${running.title ?? "working"}`
        : latestEvidence
          ? `Latest: ${latestEvidence.kind ?? "step"} - ${this.trim(latestEvidence.error || latestEvidence.result || latestEvidence.title || "", 240)}`
          : "No execution evidence yet."
    });

    const steps = document.createElement("div");
    steps.className = "task-inspector-steps";
    for (const step of task.steps)
      steps.append(this.renderInspectorStep(step));

    this.inspectorPanel.replaceChildren(head, metrics, progressBar, focus, steps);
  }

  private renderInspectorStep(step: AgentStep): HTMLElement {
    const row = document.createElement("article");
    row.className = `task-step-row ${step.status.toLowerCase()}`;
    const marker = Object.assign(document.createElement("span"), {
      textContent: String(step.order ?? "")
    });
    const body = document.createElement("div");
    body.append(
      Object.assign(document.createElement("strong"), { textContent: step.title || step.kind || step.status }),
      Object.assign(document.createElement("small"), { textContent: `${step.kind ?? "step"} / ${step.status}` })
    );
    const evidence = step.error || step.result;
    if (evidence) {
      body.append(Object.assign(document.createElement("p"), {
        textContent: this.trim(evidence, 360)
      }));
    }
    row.append(marker, body);
    return row;
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

  private handleInspectorClick(event: Event): void {
    const button = (event.target as HTMLElement).closest<HTMLButtonElement>("button[data-copy-report]");
    if (!button)
      return;
    const task = this.lastTasks.find(item => item.id === button.dataset.copyReport);
    if (!task)
      return;
    void navigator.clipboard.writeText(this.buildTaskReport(task));
    button.textContent = "Copied";
    window.setTimeout(() => { button.textContent = "Copy report"; }, 1400);
  }

  private metric(label: string, value: string): HTMLElement {
    const node = document.createElement("div");
    node.append(
      Object.assign(document.createElement("label"), { textContent: label }),
      Object.assign(document.createElement("output"), { textContent: value })
    );
    return node;
  }

  private buildTaskReport(task: AgentTask): string {
    const lines = [
      `Task ${task.id} [${task.status}] p${task.priority ?? 5}`,
      task.objective,
      "",
      `Updated: ${this.formatDate(task.updatedAt)}`,
      task.completionNotice ? `Completion: ${task.completionNotice}` : "",
      task.nextQuestion ? `Next: ${task.nextQuestion}` : "",
      "",
      "Steps:"
    ].filter(Boolean);
    for (const step of task.steps) {
      lines.push(`- ${step.order ?? "?"}. [${step.status}] ${step.kind ?? "step"}: ${step.title ?? ""}`);
      if (step.error)
        lines.push(`  error: ${this.trim(step.error, 500)}`);
      else if (step.result)
        lines.push(`  result: ${this.trim(step.result, 500)}`);
    }
    return lines.join("\n");
  }

  private trim(value: string, max: number): string {
    const normalized = value.replace(/\s+/g, " ").trim();
    return normalized.length > max ? `${normalized.slice(0, max - 3)}...` : normalized;
  }

  private formatElapsed(value?: number): string {
    const seconds = Math.max(0, Math.round(value ?? 0));
    if (seconds < 60)
      return `${seconds}s`;
    const minutes = Math.floor(seconds / 60);
    return `${minutes}m ${String(seconds % 60).padStart(2, "0")}s`;
  }

  private formatDate(value?: string): string {
    if (!value)
      return "--";
    const date = new Date(value);
    if (Number.isNaN(date.getTime()))
      return "--";
    return date.toLocaleString(undefined, {
      month: "2-digit",
      day: "2-digit",
      hour: "2-digit",
      minute: "2-digit"
    });
  }
}
