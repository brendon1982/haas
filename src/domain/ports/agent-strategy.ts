import type { AgentSessionConfig } from "../value-objects/agent-session-config.ts";
import type { SessionResult } from "../value-objects/session-result.ts";
import type { Signal } from "../value-objects/signal.ts";

export interface AgentStrategy {
  execute(config: AgentSessionConfig, signal: Signal): Promise<SessionResult>;
}
