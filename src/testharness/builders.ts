import type { AgentSessionConfig } from "../domain/value-objects/agent-session-config.js";
import type { SessionResult } from "../domain/value-objects/session-result.js";
import type { Signal } from "../domain/value-objects/signal.js";

export function aSignal(overrides?: Partial<Signal>): Signal {
  return {
    payload: "default prompt",
    source: "test",
    ...overrides,
  };
}

export function aSessionResult(overrides?: Partial<SessionResult>): SessionResult {
  return {
    output: "default output",
    sessionId: "sess-default",
    ...overrides,
  };
}

export function aSessionConfig(overrides?: Partial<AgentSessionConfig>): AgentSessionConfig {
  return {
    provider: "ollama",
    modelId: "gemma4",
    systemPrompt: "You are a helpful assistant.",
    tools: [],
    thinkingLevel: "off",
    ...overrides,
  };
}
