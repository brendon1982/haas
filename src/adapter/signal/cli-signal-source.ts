import type { Signal } from "../../domain/value-objects/signal.js";
import type { SignalSource } from "../../domain/ports/signal-source.js";
import { createInterface } from "node:readline";
import type { Interface } from "node:readline";

export class CliSignalSource implements SignalSource {
  readonly type = "cli";

  private rl: Interface | null = null;
  private listening = false;

  constructor(
    private readonly input: NodeJS.ReadableStream = process.stdin,
    private readonly output: NodeJS.WritableStream = process.stdout,
  ) {}

  async listen(handler: (signal: Signal) => Promise<void>): Promise<void> {
    if (this.listening) {
      throw new Error("CliSignalSource is already listening");
    }

    this.listening = true;
    this.rl = createInterface({ input: this.input, output: this.output });

    try {
      this.rl.setPrompt("> ");
      this.rl.prompt();

      for await (const line of this.rl) {
        const trimmed = line.trim();
        if (trimmed === "") {
          break;
        }
        await handler({ payload: trimmed, source: "cli" });
        this.rl.prompt();
      }
    } finally {
      this.rl?.close();
      this.rl = null;
      this.listening = false;
    }
  }

  async shutdown(): Promise<void> {
    this.rl?.close();
    this.rl = null;
    this.listening = false;
  }
}
