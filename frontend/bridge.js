(function () {
  "use strict";

  class KokoBridgeClient {
    constructor(transport = window.chrome && window.chrome.webview) {
      this.transport = transport;
      this.pending = new Map();
      this.listeners = new Map();
      this.available = Boolean(transport);
      if (transport)
        transport.addEventListener("message", event => this.receive(event.data));
    }

    call(method, payload = null, timeoutMs = 10000) {
      if (!this.transport)
        return Promise.reject(new Error("Kokonoe host bridge is unavailable."));

      const id = globalThis.crypto && globalThis.crypto.randomUUID
        ? globalThis.crypto.randomUUID()
        : `${Date.now()}-${Math.random().toString(16).slice(2)}`;
      return new Promise((resolve, reject) => {
        const timeout = window.setTimeout(() => {
          this.pending.delete(id);
          reject(new Error(`Bridge request timed out: ${method}`));
        }, timeoutMs);
        this.pending.set(id, { resolve, reject, timeout });
        this.transport.postMessage({ type: "request", id, method, payload });
      });
    }

    on(channel, handler) {
      const bucket = this.listeners.get(channel) || new Set();
      bucket.add(handler);
      this.listeners.set(channel, bucket);
      return () => bucket.delete(handler);
    }

    receive(envelope) {
      if (!envelope || !envelope.type) return;
      if (envelope.type === "response") {
        const request = this.pending.get(envelope.id);
        if (!request) return;
        window.clearTimeout(request.timeout);
        this.pending.delete(envelope.id);
        if (envelope.error) request.reject(new Error(envelope.error));
        else request.resolve(envelope.result);
        return;
      }

      if (envelope.type === "event") {
        const handlers = this.listeners.get(envelope.channel);
        if (handlers) handlers.forEach(handler => handler(envelope.payload));
      }
    }
  }

  window.koko = new KokoBridgeClient();
})();
