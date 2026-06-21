(function () {
  "use strict";
  const activity = document.getElementById("agent-activity");
  const summary = document.getElementById("agent-summary");
  const tasks = document.getElementById("agent-tasks");
  const statusClass = value => String(value || "pending").toLowerCase();

  function renderTask(task) {
    const root = document.createElement("article");
    root.className = "agent-task";
    const head = document.createElement("div");
    head.className = "agent-task-head";
    const title = document.createElement("strong");
    title.textContent = task.objective || task.id || "Untitled task";
    const status = document.createElement("span");
    status.className = "agent-status " + statusClass(task.status);
    status.textContent = task.status || "Pending";
    head.append(title, status);
    const steps = document.createElement("div");
    steps.className = "agent-steps";
    for (const step of task.steps || []) {
      const indicator = document.createElement("i");
      indicator.className = "agent-step " + statusClass(step.status);
      indicator.title = (step.title || step.kind || "Step") + ": " + (step.status || "Pending");
      steps.append(indicator);
    }
    root.append(head, steps);
    return root;
  }

  function render(snapshot) {
    if (!snapshot) return;
    const current = snapshot.activity || {};
    const heading = document.createElement("strong");
    heading.textContent = (current.phase || "idle") + " / " + (current.tool || "none");
    const detail = document.createElement("p");
    detail.textContent = current.thought || current.focus || "No active task.";
    activity.replaceChildren(heading, detail);
    const allTasks = snapshot.tasks || [];
    const count = document.createElement("span");
    count.textContent = allTasks.length + " tasks";
    const running = document.createElement("span");
    running.textContent = (snapshot.runningSteps || 0) + " / " + (snapshot.maxParallel || 0) + " running";
    summary.replaceChildren(count, running);
    tasks.replaceChildren(...allTasks.slice(0, 8).map(renderTask));
    if (!allTasks.length) {
      const empty = document.createElement("p");
      empty.className = "agent-empty";
      empty.textContent = "No active tasks.";
      tasks.append(empty);
    }
  }

  window.koko.on("agent.activity", payload => render(payload && payload.snapshot));
  window.koko.on("agent.completed", payload => render(payload && payload.snapshot));
  window.kokoAgentBoard = {
    connect: async function () {
      try {
        render(await window.koko.call("agent.snapshot"));
      } catch (error) {
        activity.querySelector("strong").textContent = "Agent bridge unavailable";
        activity.querySelector("p").textContent = error instanceof Error ? error.message : String(error);
      }
    },
    render
  };
})();
