import type { ExecutionTarget } from "../../domain/ports/execution-target.ts";
import type { SessionResult } from "../../domain/value-objects/session-result.ts";

export class ConsoleExecutionTarget implements ExecutionTarget {
  async deliver(result: SessionResult): Promise<void> {
    console.log(`\n[Session ${result.sessionId}]`);
    console.log(result.output);
  }
}
