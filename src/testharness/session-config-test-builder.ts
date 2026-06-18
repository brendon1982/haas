import type { AgentSessionConfig } from "../domain/value-objects/agent-session-config.js";

export class SessionConfigTestBuilder {
  private provider = "ollama";
  private modelId = "gemma4";
  private systemPrompt = "You are a helpful assistant.";
  private tools: readonly string[] = [];
  private thinkingLevel = "off";

  private constructor() {}

  static create(): SessionConfigTestBuilder {
    return new SessionConfigTestBuilder();
  }

  withProvider(provider: string): SessionConfigTestBuilder {
    this.provider = provider;
    return this;
  }

  withModelId(modelId: string): SessionConfigTestBuilder {
    this.modelId = modelId;
    return this;
  }

  withSystemPrompt(systemPrompt: string): SessionConfigTestBuilder {
    this.systemPrompt = systemPrompt;
    return this;
  }

  withTools(tools: readonly string[]): SessionConfigTestBuilder {
    this.tools = tools;
    return this;
  }

  withThinkingLevel(thinkingLevel: string): SessionConfigTestBuilder {
    this.thinkingLevel = thinkingLevel;
    return this;
  }

  build(): AgentSessionConfig {
    return {
      provider: this.provider,
      modelId: this.modelId,
      systemPrompt: this.systemPrompt,
      tools: this.tools,
      thinkingLevel: this.thinkingLevel,
    };
  }
}
