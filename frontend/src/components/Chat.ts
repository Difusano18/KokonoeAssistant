import DOMPurify from "dompurify";
import hljs from "highlight.js/lib/core";
import bash from "highlight.js/lib/languages/bash";
import csharp from "highlight.js/lib/languages/csharp";
import css from "highlight.js/lib/languages/css";
import ini from "highlight.js/lib/languages/ini";
import javascript from "highlight.js/lib/languages/javascript";
import json from "highlight.js/lib/languages/json";
import markdownLang from "highlight.js/lib/languages/markdown";
import powershell from "highlight.js/lib/languages/powershell";
import python from "highlight.js/lib/languages/python";
import sql from "highlight.js/lib/languages/sql";
import typescript from "highlight.js/lib/languages/typescript";
import xml from "highlight.js/lib/languages/xml";
import yaml from "highlight.js/lib/languages/yaml";
import { marked } from "marked";
import { buildArtifactCard, type ArtifactSummary } from "./ArtifactCard";
import "highlight.js/styles/github-dark-dimmed.css";

hljs.registerLanguage("bash", bash);
hljs.registerLanguage("csharp", csharp);
hljs.registerLanguage("css", css);
hljs.registerLanguage("ini", ini);
hljs.registerLanguage("javascript", javascript);
hljs.registerLanguage("json", json);
hljs.registerLanguage("markdown", markdownLang);
hljs.registerLanguage("powershell", powershell);
hljs.registerLanguage("python", python);
hljs.registerLanguage("sql", sql);
hljs.registerLanguage("typescript", typescript);
hljs.registerLanguage("xml", xml);
hljs.registerLanguage("yaml", yaml);

marked.setOptions({ gfm: true, breaks: true });

const MSG_COPY_ICON = `<svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect x="9" y="9" width="13" height="13" rx="2" ry="2"/><path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"/></svg>`;

type ChatEvent = {
  streamId: string;
  sequence?: number;
  chunk?: string;
  reply?: string;
  error?: string;
  errorType?: string;
  role?: string;
  content?: string;
  goal?: string;
};

type ChatSendAck = {
  accepted: boolean;
  streamId: string;
};

function formatClock(date: Date): string {
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${pad(date.getHours())}:${pad(date.getMinutes())}:${pad(date.getSeconds())}`;
}

export class ChatController {
  private activeStreamId = "";
  private activeBody: HTMLElement | null = null;
  private activeRawText = "";
  private busy = false;
  private responseTimerHandle = 0;
  private responseStartedAt = 0;

  constructor(
    form: HTMLFormElement,
    private readonly input: HTMLInputElement,
    private readonly send: HTMLButtonElement,
    private readonly messages: HTMLElement,
    private readonly scroll: HTMLElement,
    private readonly status: HTMLElement
  ) {
    form.addEventListener("submit", event => {
      event.preventDefault();
      void this.submit();
    });
    window.koko.on("chat.started", payload => this.onStarted(payload as ChatEvent));
    window.koko.on("chat.chunk", payload => this.onChunk(payload as ChatEvent));
    window.koko.on("chat.reset", payload => this.onReset(payload as ChatEvent));
    window.koko.on("chat.completed", payload => this.onCompleted(payload as ChatEvent));
    window.koko.on("chat.error", payload => this.onError(payload as ChatEvent));
    window.koko.on("chat.canceled", payload => this.onCanceled(payload as ChatEvent));
    window.koko.on("chat.external", payload => this.onExternal(payload as ChatEvent));
    window.koko.on("artifact.new", payload => this.onArtifact(payload as ArtifactSummary));
    window.koko.on("mission.started", payload => this.onMissionStarted(payload as ChatEvent));
  }

  setAvailable(available: boolean): void {
    this.input.disabled = !available;
    this.send.disabled = !available;
    this.status.textContent = available ? "STREAM READY" : "HOST UNAVAILABLE";
    this.status.className = available ? "ok" : "pending";
  }

  private async submit(): Promise<void> {
    const text = this.input.value.trim();
    if (!text || this.busy) return;
    this.busy = true;
    this.stopResponseTimer();
    this.activeStreamId = globalThis.crypto?.randomUUID?.() ?? `${Date.now()}`;
    this.appendMessage("user", text);
    this.activeRawText = "";
    this.activeBody = this.appendMessage("assistant", "", true);
    this.input.value = "";
    this.setBusy(true);
    try {
      // chat.send only acknowledges receipt now - the actual reply arrives via
      // chat.started/chat.chunk/chat.completed/chat.error/chat.canceled events.
      // A short timeout here only guards against the RPC itself not getting
      // through; it no longer has to cover however long the model takes to reply,
      // which is what made a slow-but-working response look like a dead bridge.
      await window.koko.call<ChatSendAck>("chat.send", { text, streamId: this.activeStreamId }, 10000);
    } catch (error) {
      // fail() resets busy/focus itself; only call it if chat.error hasn't already
      // (which also resets busy/focus) to avoid stepping on a more specific message.
      if (this.activeBody?.closest(".message")?.classList.contains("error")) return;
      this.fail(error instanceof Error ? error.message : String(error));
    }
  }

  private onStarted(event: ChatEvent): void {
    if (event.streamId !== this.activeStreamId || !this.activeBody) return;
    this.stopResponseTimer();
    this.responseStartedAt = performance.now();
    const timer = document.createElement("span");
    timer.id = "response-timer";
    timer.textContent = "thinking · 0.0s";
    this.activeBody.appendChild(timer);
    this.responseTimerHandle = window.setInterval(() => this.tickResponseTimer(), 100);
  }

  private tickResponseTimer(): void {
    const timer = document.getElementById("response-timer");
    if (!timer) return;
    const elapsed = (performance.now() - this.responseStartedAt) / 1000;
    timer.textContent = `thinking · ${elapsed.toFixed(1)}s`;
  }

  private stopResponseTimer(): void {
    if (this.responseTimerHandle) {
      window.clearInterval(this.responseTimerHandle);
      this.responseTimerHandle = 0;
    }
  }

  private onChunk(event: ChatEvent): void {
    if (event.streamId !== this.activeStreamId || !this.activeBody) return;
    this.stopResponseTimer();
    this.activeRawText += event.chunk ?? "";
    this.renderMarkdown(this.activeBody, this.activeRawText);
    this.scrollToEnd();
  }

  private onReset(event: ChatEvent): void {
    if (event.streamId === this.activeStreamId && this.activeBody) {
      this.activeRawText = "";
      this.activeBody.classList.remove("markdown");
      this.activeBody.textContent = "";
    }
  }

  private onCompleted(event: ChatEvent): void {
    if (event.streamId !== this.activeStreamId || !this.activeBody) return;
    this.stopResponseTimer();
    this.activeRawText = event.reply ?? this.activeRawText;
    this.renderMarkdown(this.activeBody, this.activeRawText);
    this.activeBody.closest(".message")?.classList.remove("streaming");
    if (this.responseStartedAt) {
      const duration = document.createElement("span");
      duration.className = "response-duration";
      duration.textContent = `↩ ${((performance.now() - this.responseStartedAt) / 1000).toFixed(1)}s`;
      this.activeBody.append(duration);
    }
    this.scrollToEnd();
    this.setBusy(false);
    this.input.focus();
  }

  private onCanceled(event: ChatEvent): void {
    if (event.streamId !== this.activeStreamId || !this.activeBody) return;
    this.stopResponseTimer();
    if (!this.activeRawText) {
      this.activeBody.classList.remove("markdown");
      this.activeBody.textContent = "Скасовано.";
    }
    this.activeBody.closest(".message")?.classList.remove("streaming");
    this.setBusy(false);
    this.input.focus();
  }

  private onError(event: ChatEvent): void {
    if (event.streamId === this.activeStreamId)
      this.fail(event.error ?? "Chat failed.", event.errorType === "timeout" || event.errorType === "provider");
  }

  private onExternal(event: ChatEvent): void {
    const text = event.content?.trim() ?? "";
    if (!text) return;
    this.appendMessage(event.role === "system" ? "system" : "assistant", text);
  }

  private onArtifact(artifact: ArtifactSummary): void {
    this.messages.append(buildArtifactCard(artifact));
    this.scrollToEnd();
  }

  private onMissionStarted(event: ChatEvent): void {
    if (event.streamId !== this.activeStreamId || !this.activeBody) return;
    const banner = document.createElement("div");
    banner.className = "mission-banner";
    const dot = document.createElement("div");
    dot.className = "mission-dot";
    const text = document.createElement("div");
    const title = document.createElement("strong");
    title.textContent = "Місія запущена";
    const goal = document.createElement("span");
    goal.textContent = event.goal ?? "";
    text.append(title, goal);
    banner.append(dot, text);
    // The assistant's (empty/typing) bubble already exists by the time this
    // fires — it was created synchronously when the message was sent, while
    // this event only arrives once the server-side stream attempt has
    // already failed over to tool fallback. Insert before that bubble's
    // message row instead of appending, so the banner still reads as
    // preceding the reply rather than trailing whatever's already there.
    this.activeBody.closest(".message")?.before(banner);
    this.scrollToEnd();
  }

  private appendMessage(role: "user" | "assistant" | "system", text: string, streaming = false): HTMLElement {
    const article = document.createElement("article");
    article.className = `message ${role}${streaming ? " streaming" : ""}`;
    const meta = document.createElement("span");
    meta.className = "message-meta";
    meta.textContent = role === "user" ? "You" : "Kokonoe";
    const body = document.createElement("div");
    body.className = "message-body";
    if (role === "user") {
      body.textContent = text;
      this.addMessageCopyButton(body);
    } else if (text) {
      this.renderMarkdown(body, text);
    } else if (streaming) {
      body.innerHTML = `<div class="typing-dots"><span></span><span></span><span></span></div>`;
    }
    article.append(meta, body);
    if (role === "user") {
      const time = document.createElement("span");
      time.className = "msg-time";
      time.textContent = formatClock(new Date());
      article.append(time);
    }
    this.messages.append(article);
    this.scrollToEnd();
    return body;
  }

  private renderMarkdown(body: HTMLElement, raw: string): void {
    body.classList.add("markdown");
    body.innerHTML = DOMPurify.sanitize(marked.parse(raw, { async: false }));
    body.querySelectorAll<HTMLElement>("pre code").forEach(block => hljs.highlightElement(block));
    body.querySelectorAll<HTMLElement>("pre").forEach(pre => this.attachCopyButton(pre));
    this.addMessageCopyButton(body);
  }

  private addMessageCopyButton(body: HTMLElement): void {
    const button = document.createElement("button");
    button.type = "button";
    button.className = "msg-copy-btn";
    button.title = "Копіювати";
    button.innerHTML = MSG_COPY_ICON;
    button.addEventListener("click", () => {
      void navigator.clipboard.writeText(body.textContent ?? "");
      button.innerHTML = "✓";
      window.setTimeout(() => { button.innerHTML = MSG_COPY_ICON; }, 1500);
    });
    body.append(button);
  }

  private attachCopyButton(pre: HTMLElement): void {
    if (pre.querySelector(".code-copy")) return;
    const button = document.createElement("button");
    button.type = "button";
    button.className = "code-copy";
    button.textContent = "copy";
    button.addEventListener("click", () => {
      void navigator.clipboard.writeText(pre.querySelector("code")?.textContent ?? "");
      button.textContent = "✓";
      window.setTimeout(() => { button.textContent = "copy"; }, 1500);
    });
    pre.append(button);
  }

  private fail(message: string, actionable = false): void {
    if (!this.activeBody) return;
    this.stopResponseTimer();
    this.activeBody.classList.remove("markdown");
    this.activeBody.textContent = message;
    if (actionable) {
      const button = document.createElement("button");
      button.type = "button";
      button.className = "error-action";
      button.textContent = "Відкрити Settings";
      button.addEventListener("click", () => {
        document.getElementById("settings-open")?.click();
      });
      this.activeBody.append(document.createElement("br"), button);
    }
    this.addMessageCopyButton(this.activeBody);
    const messageElement = this.activeBody.closest(".message");
    messageElement?.classList.remove("streaming");
    messageElement?.classList.add("error");
    this.setBusy(false);
    this.input.focus();
  }

  private setBusy(value: boolean): void {
    this.busy = value;
    this.input.disabled = value;
    this.send.disabled = value;
    this.status.textContent = value ? "GENERATING" : "STREAM READY";
    this.status.className = value ? "pending" : "ok";
  }

  private scrollToEnd(): void {
    this.scroll.scrollTop = this.scroll.scrollHeight;
  }
}
