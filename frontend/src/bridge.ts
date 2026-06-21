type KokoRequest = {
  type: "request";
  id: string;
  method: string;
  payload: unknown;
};

type KokoResponse = {
  type: "response";
  id: string;
  result?: unknown;
  error?: string;
};

type KokoEvent = {
  type: "event";
  channel: string;
  payload: unknown;
};

type KokoEnvelope = KokoResponse | KokoEvent;
type EventHandler = (payload: unknown) => void;

interface WebViewTransport {
  postMessage(message: KokoRequest): void;
  addEventListener(type: "message", listener: (event: MessageEvent<KokoEnvelope>) => void): void;
}

declare global {
  interface Window {
    chrome?: { webview?: WebViewTransport };
    koko: KokoBridgeClient;
  }
}

class KokoBridgeClient {
  private readonly pending = new Map<string, {
    resolve: (value: unknown) => void;
    reject: (reason: Error) => void;
    timeout: number;
  }>();
  private readonly listeners = new Map<string, Set<EventHandler>>();

  readonly available: boolean;

  constructor(private readonly transport = window.chrome?.webview) {
    this.available = Boolean(transport);
    transport?.addEventListener("message", event => this.receive(event.data));
  }

  call<T = unknown>(method: string, payload: unknown = null, timeoutMs = 10000): Promise<T> {
    if (!this.transport)
      return Promise.reject(new Error("Kokonoe host bridge is unavailable."));

    const id = globalThis.crypto?.randomUUID?.() ?? `${Date.now()}-${Math.random().toString(16).slice(2)}`;
    return new Promise<T>((resolve, reject) => {
      const timeout = window.setTimeout(() => {
        this.pending.delete(id);
        reject(new Error(`Bridge request timed out: ${method}`));
      }, timeoutMs);
      this.pending.set(id, { resolve: value => resolve(value as T), reject, timeout });
      this.transport!.postMessage({ type: "request", id, method, payload });
    });
  }

  on(channel: string, handler: EventHandler): () => void {
    const bucket = this.listeners.get(channel) ?? new Set<EventHandler>();
    bucket.add(handler);
    this.listeners.set(channel, bucket);
    return () => bucket.delete(handler);
  }

  private receive(envelope: KokoEnvelope): void {
    if (envelope.type === "response") {
      const request = this.pending.get(envelope.id);
      if (!request) return;
      window.clearTimeout(request.timeout);
      this.pending.delete(envelope.id);
      if (envelope.error) request.reject(new Error(envelope.error));
      else request.resolve(envelope.result);
      return;
    }

    if (envelope.type === "event")
      this.listeners.get(envelope.channel)?.forEach(handler => handler(envelope.payload));
  }
}

window.koko = new KokoBridgeClient();

export { KokoBridgeClient };
export type { KokoEnvelope, KokoEvent, KokoRequest, KokoResponse };
