import type { SessionResult } from "../value-objects/session-result.ts";

export interface ExecutionTarget {
  deliver(result: SessionResult): Promise<void>;
}
