import type { Signal } from "../value-objects/signal.ts";

export interface SignalSource {
  read(): Promise<Signal | null>;
}
