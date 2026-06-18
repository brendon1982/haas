import type { AgentSessionConfig } from "../value-objects/agent-session-config.js";
import type { SessionResult } from "../value-objects/session-result.js";
import type { Signal } from "../value-objects/signal.js";

export interface AgentStrategy {
  execute(config: AgentSessionConfig, signal: Signal): Promise<SessionResult>;
}
