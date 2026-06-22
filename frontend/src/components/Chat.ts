type ChatEvent = {
  streamId: string;
  sequence?: number;
  chunk?: string;
  reply?: string;
  error?: string;
  role?: string;
  content?: string;
};

type ChatSendResult = {
  streamId: string;
  reply: string;
  streamed: boolean;
};

export class ChatController {
  private activeStreamId = "";
  private activeBody: HTMLElement | null = null;
  private busy = false;

  constructor(
    private readonly form: HTMLFormElement,
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
    window.koko.on("chat.chunk", payload => this.onChunk(payload as ChatEvent));
    window.koko.on("chat.reset", payload => this.onReset(payload as ChatEvent));
    window.koko.on("chat.completed", payload => this.onCompleted(payload as ChatEvent));
    window.koko.on("chat.error", payload => this.onError(payload as ChatEvent));
    window.koko.on("chat.external", payload => this.onExternal(payload as ChatEvent));
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
    this.activeStreamId = globalThis.crypto?.randomUUID?.() ?? `${Date.now()}`;
    this.appendMessage("user", text);
    this.activeBody = this.appendMessage("assistant", "", true);
    this.input.value = "";
    this.setBusy(true);
    try {
      const result = await window.koko.call<ChatSendResult>("chat.send", { text, streamId: this.activeStreamId }, 180000);
      if (this.activeBody && !this.activeBody.textContent)
        this.activeBody.textContent = result.reply;
    } catch (error) {
      this.fail(error instanceof Error ? error.message : String(error));
    } finally {
      this.setBusy(false);
      this.input.focus();
    }
  }

  private onChunk(event: ChatEvent): void {
    if (event.streamId !== this.activeStreamId || !this.activeBody) return;
    this.activeBody.append(document.createTextNode(event.chunk ?? ""));
    this.scrollToEnd();
  }

  private onReset(event: ChatEvent): void {
    if (event.streamId === this.activeStreamId && this.activeBody)
      this.activeBody.textContent = "";
  }

  private onCompleted(event: ChatEvent): void {
    if (event.streamId !== this.activeStreamId || !this.activeBody) return;
    this.activeBody.textContent = event.reply ?? this.activeBody.textContent ?? "";
    this.activeBody.closest(".message")?.classList.remove("streaming");
    this.scrollToEnd();
  }

  private onError(event: ChatEvent): void {
    if (event.streamId === this.activeStreamId)
      this.fail(event.error ?? "Chat failed.");
  }

  private onExternal(event: ChatEvent): void {
    const text = event.content?.trim() ?? "";
    if (!text) return;
    this.appendMessage(event.role === "system" ? "system" : "assistant", text);
  }

  private appendMessage(role: "user" | "assistant" | "system", text: string, streaming = false): HTMLElement {
    const article = document.createElement("article");
    article.className = `message ${role}${streaming ? " streaming" : ""}`;
    const meta = document.createElement("span");
    meta.className = "message-meta";
    meta.textContent = role === "user" ? "You" : "Kokonoe";
    const body = document.createElement("p");
    body.className = "message-body";
    body.textContent = text;
    article.append(meta, body);
    this.messages.append(article);
    this.scrollToEnd();
    return body;
  }

  private fail(message: string): void {
    if (!this.activeBody) return;
    this.activeBody.textContent = message;
    const messageElement = this.activeBody.closest(".message");
    messageElement?.classList.remove("streaming");
    messageElement?.classList.add("error");
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
