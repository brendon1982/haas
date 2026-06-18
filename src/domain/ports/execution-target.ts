import type { SessionResult } from "../value-objects/session-result.js";

export interface ExecutionTarget {
  deliver(result: SessionResult): Promise<void>;
}
