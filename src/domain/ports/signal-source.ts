import type { Signal } from "../value-objects/signal.js";

export interface SignalSource {
  readonly type: string;
  listen(handler: (signal: Signal) => Promise<void>): Promise<void>;
  shutdown(): Promise<void>;
}
