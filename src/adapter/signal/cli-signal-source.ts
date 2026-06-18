import type { Signal } from "../../domain/value-objects/signal.js";
import type { SignalSource } from "../../domain/ports/signal-source.js";
import { createInterface } from "node:readline";

export class CliSignalSource implements SignalSource {
  async read(): Promise<Signal | null> {
    const rl = createInterface({ input: process.stdin });
    try {
      const line = await new Promise<string | null>((resolve) => {
        rl.question("> ", resolve);
      });
      if (line === null || line.trim() === "") {
        return null;
      }
      return { payload: line.trim(), source: "cli" };
    } finally {
      rl.close();
    }
  }
}
