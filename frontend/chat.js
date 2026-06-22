(function () {
  "use strict";

  class ChatController {
    constructor(form, input, send, messages, scroll, status) {
      this.form = form;
      this.input = input;
      this.send = send;
      this.messages = messages;
      this.scroll = scroll;
      this.status = status;
      this.activeStreamId = "";
      this.activeBody = null;
      this.busy = false;
      form.addEventListener("submit", event => {
        event.preventDefault();
        this.submit();
      });
      window.koko.on("chat.chunk", payload => this.onChunk(payload));
      window.koko.on("chat.reset", payload => this.onReset(payload));
      window.koko.on("chat.completed", payload => this.onCompleted(payload));
      window.koko.on("chat.error", payload => this.onError(payload));
      window.koko.on("chat.external", payload => this.onExternal(payload));
    }

    setAvailable(available) {
      this.input.disabled = !available;
      this.send.disabled = !available;
      this.status.textContent = available ? "STREAM READY" : "HOST UNAVAILABLE";
      this.status.className = available ? "ok" : "pending";
    }

    async submit() {
      const text = this.input.value.trim();
      if (!text || this.busy) return;
      this.busy = true;
      this.activeStreamId = globalThis.crypto && globalThis.crypto.randomUUID
        ? globalThis.crypto.randomUUID()
        : `${Date.now()}`;
      this.appendMessage("user", text);
      this.activeBody = this.appendMessage("assistant", "", true);
      this.input.value = "";
      this.setBusy(true);
      try {
        const result = await window.koko.call("chat.send", { text, streamId: this.activeStreamId }, 180000);
        if (this.activeBody && !this.activeBody.textContent)
          this.activeBody.textContent = result.reply;
      } catch (error) {
        this.fail(error instanceof Error ? error.message : String(error));
      } finally {
        this.setBusy(false);
        this.input.focus();
      }
    }

    onChunk(event) {
      if (event.streamId !== this.activeStreamId || !this.activeBody) return;
      this.activeBody.append(document.createTextNode(event.chunk || ""));
      this.scrollToEnd();
    }

    onReset(event) {
      if (event.streamId === this.activeStreamId && this.activeBody)
        this.activeBody.textContent = "";
    }

    onCompleted(event) {
      if (event.streamId !== this.activeStreamId || !this.activeBody) return;
      this.activeBody.textContent = event.reply || this.activeBody.textContent || "";
      const message = this.activeBody.closest(".message");
      if (message) message.classList.remove("streaming");
      this.scrollToEnd();
    }

    onError(event) {
      if (event.streamId === this.activeStreamId)
        this.fail(event.error || "Chat failed.");
    }

    onExternal(event) {
      const text = typeof event.content === "string" ? event.content.trim() : "";
      if (!text) return;
      this.appendMessage(event.role === "system" ? "system" : "assistant", text);
    }

    appendMessage(role, text, streaming = false) {
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

    fail(message) {
      if (!this.activeBody) return;
      this.activeBody.textContent = message;
      const element = this.activeBody.closest(".message");
      if (element) {
        element.classList.remove("streaming");
        element.classList.add("error");
      }
    }

    setBusy(value) {
      this.busy = value;
      this.input.disabled = value;
      this.send.disabled = value;
      this.status.textContent = value ? "GENERATING" : "STREAM READY";
      this.status.className = value ? "pending" : "ok";
    }

    scrollToEnd() {
      this.scroll.scrollTop = this.scroll.scrollHeight;
    }
  }

  const controller = new ChatController(
    document.getElementById("chat-form"),
    document.getElementById("chat-input"),
    document.getElementById("chat-send"),
    document.getElementById("messages"),
    document.getElementById("chat-scroll"),
    document.getElementById("chat-status")
  );
  window.kokoChat = controller;
})();
