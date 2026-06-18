import type { ExecutionTarget } from "../../domain/ports/execution-target.js";
import type { SessionResult } from "../../domain/value-objects/session-result.js";

export class ConsoleExecutionTarget implements ExecutionTarget {
  async deliver(result: SessionResult): Promise<void> {
    console.log(`\n[Session ${result.sessionId}]`);
    console.log(result.output);
  }
}
