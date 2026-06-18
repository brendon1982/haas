import type { AgentSessionConfig } from "../domain/value-objects/agent-session-config.js";
import type { SessionResult } from "../domain/value-objects/session-result.js";
import type { Signal } from "../domain/value-objects/signal.js";

export class SignalBuilder {
  private payload = "default prompt";
  private source = "test";
  private sessionId: string | undefined;

  private constructor() {}

  static create(): SignalBuilder {
    return new SignalBuilder();
  }

  withPayload(payload: string): SignalBuilder {
    this.payload = payload;
    return this;
  }

  withSource(source: string): SignalBuilder {
    this.source = source;
    return this;
  }

  withSessionId(sessionId: string): SignalBuilder {
    this.sessionId = sessionId;
    return this;
  }

  build(): Signal {
    return {
      payload: this.payload,
      source: this.source,
      ...(this.sessionId !== undefined ? { sessionId: this.sessionId } : {}),
    };
  }
}

export class SessionResultBuilder {
  private output = "default output";
  private sessionId = "sess-default";

  private constructor() {}

  static create(): SessionResultBuilder {
    return new SessionResultBuilder();
  }

  withOutput(output: string): SessionResultBuilder {
    this.output = output;
    return this;
  }

  withSessionId(sessionId: string): SessionResultBuilder {
    this.sessionId = sessionId;
    return this;
  }

  build(): SessionResult {
    return { output: this.output, sessionId: this.sessionId };
  }
}

export class SessionConfigBuilder {
  private provider = "ollama";
  private modelId = "gemma4";
  private systemPrompt = "You are a helpful assistant.";
  private tools: readonly string[] = [];
  private thinkingLevel = "off";

  private constructor() {}

  static create(): SessionConfigBuilder {
    return new SessionConfigBuilder();
  }

  withProvider(provider: string): SessionConfigBuilder {
    this.provider = provider;
    return this;
  }

  withModelId(modelId: string): SessionConfigBuilder {
    this.modelId = modelId;
    return this;
  }

  withSystemPrompt(systemPrompt: string): SessionConfigBuilder {
    this.systemPrompt = systemPrompt;
    return this;
  }

  withTools(tools: readonly string[]): SessionConfigBuilder {
    this.tools = tools;
    return this;
  }

  withThinkingLevel(thinkingLevel: string): SessionConfigBuilder {
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
