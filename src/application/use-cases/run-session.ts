import type { AgentStrategy } from "../../domain/ports/agent-strategy.js";
import type { ExecutionTarget } from "../../domain/ports/execution-target.js";
import type { AgentSessionConfig } from "../../domain/value-objects/agent-session-config.js";
import type { Signal } from "../../domain/value-objects/signal.js";

export class RunSessionUseCase {
  constructor(
    private readonly agentStrategy: AgentStrategy,
    private readonly executionTarget: ExecutionTarget,
  ) {}

  async execute(config: AgentSessionConfig, signal: Signal): Promise<void> {
    const result = await this.agentStrategy.execute(config, signal);
    await this.executionTarget.deliver(result);
  }
}
