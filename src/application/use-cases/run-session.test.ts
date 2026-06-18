import { describe, expect, it } from "vitest";
import type { AgentStrategy } from "../../domain/ports/agent-strategy.js";
import type { ExecutionTarget } from "../../domain/ports/execution-target.js";
import type { SignalSource } from "../../domain/ports/signal-source.js";
import type { AgentSessionConfig } from "../../domain/value-objects/agent-session-config.js";
import type { SessionResult } from "../../domain/value-objects/session-result.js";
import type { Signal } from "../../domain/value-objects/signal.js";
import { RunSessionUseCase } from "./run-session.js";

const testConfig: AgentSessionConfig = {
  provider: "ollama",
  modelId: "gemma4",
  systemPrompt: "You are a helpful assistant.",
  tools: [],
  thinkingLevel: "off",
};

function fakeSignalSource(signal: Signal | null): SignalSource {
  return { read: () => Promise.resolve(signal) };
}

function fakeStrategy(expected: SessionResult): AgentStrategy {
  return {
    execute: async (_config, _signal) => expected,
  };
}

function fakeTarget(): ExecutionTarget & { delivered: SessionResult | null } {
  const target: ExecutionTarget & { delivered: SessionResult | null } = {
    delivered: null,
    deliver: async (result) => {
      target.delivered = result;
    },
  };
  return target;
}

describe("RunSessionUseCase", () => {
  it("reads a signal and delivers the session result", async () => {
    const signal: Signal = { payload: "hello", source: "test" };
    const expectedResult: SessionResult = { output: "Hi there!", sessionId: "sess-1" };
    const source = fakeSignalSource(signal);
    const strategy = fakeStrategy(expectedResult);
    const target = fakeTarget();

    const useCase = new RunSessionUseCase(source, strategy, target);
    await useCase.execute(testConfig);

    expect(target.delivered).toEqual(expectedResult);
  });

  it("does nothing when signal source returns null", async () => {
    const source = fakeSignalSource(null);
    const strategy = fakeStrategy({ output: "", sessionId: "" });
    const target = fakeTarget();

    const useCase = new RunSessionUseCase(source, strategy, target);
    await useCase.execute(testConfig);

    expect(target.delivered).toBeNull();
  });
});
