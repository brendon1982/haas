import type { AgentStrategy } from "../../domain/ports/agent-strategy.ts";
import type { ExecutionTarget } from "../../domain/ports/execution-target.ts";
import type { SignalSource } from "../../domain/ports/signal-source.ts";
import type { AgentSessionConfig } from "../../domain/value-objects/agent-session-config.ts";

export class RunSessionUseCase {
  constructor(
    private readonly signalSource: SignalSource,
    private readonly agentStrategy: AgentStrategy,
    private readonly executionTarget: ExecutionTarget,
  ) {}

  async execute(config: AgentSessionConfig): Promise<void> {
    const signal = await this.signalSource.read();
    if (signal === null) {
      return;
    }
    const result = await this.agentStrategy.execute(config, signal);
    await this.executionTarget.deliver(result);
  }
}
