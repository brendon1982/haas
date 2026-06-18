import type { Signal } from "../value-objects/signal.js";

export interface SignalSource {
  read(): Promise<Signal | null>;
}
