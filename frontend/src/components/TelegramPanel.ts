interface TelegramChannelStatus {
  enabled: boolean;
  configured: boolean;
  state: string;
  account: string;
  lastActivity: string;
  lastActivityAt?: string;
  lastError: string;
}
interface TelegramStatus { updatedAt: string; bot: TelegramChannelStatus; user: TelegramChannelStatus; }

export class TelegramPanelController {
  private readonly refresh = document.getElementById("telegram-refresh") as HTMLButtonElement;
  private timer?: number;

  constructor() {
    this.refresh.addEventListener("click", () => void this.load());
    window.koko.on("telegram.status", payload => this.render(payload as TelegramStatus));
  }

  async connect(): Promise<void> {
    this.refresh.disabled = false;
    await this.load();
    this.timer ??= window.setInterval(() => void this.load(), 15_000);
  }

  private async load(): Promise<void> {
    this.refresh.disabled = true;
    try { this.render(await window.koko.call("telegram.status") as TelegramStatus); }
    finally { this.refresh.disabled = false; }
  }

  private render(status: TelegramStatus): void {
    this.renderChannel("telegram-bot", status.bot);
    this.renderChannel("telegram-user", status.user);
  }

  private renderChannel(id: string, channel: TelegramChannelStatus): void {
    const root = document.getElementById(id)!;
    const online = channel.state === "listening" || channel.state === "connected";
    const pending = channel.state === "connecting" || channel.state === "idle";
    root.className = `telegram-channel ${online ? "online" : channel.state === "error" ? "error" : pending ? "pending" : ""}`;
    root.querySelector("output")!.textContent = channel.state.replaceAll("_", " ");
    const detail = root.querySelector(".telegram-detail")!;
    const parts = [channel.enabled ? "enabled" : "disabled", channel.configured ? "configured" : "not configured"];
    if (channel.account) parts.push(channel.account);
    if (channel.lastActivityAt) parts.push(`${channel.lastActivity} ${new Date(channel.lastActivityAt).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" })}`);
    if (channel.lastError) parts.push(channel.lastError);
    detail.textContent = parts.join(" / ");
    detail.classList.toggle("telegram-error", Boolean(channel.lastError));
  }
}
