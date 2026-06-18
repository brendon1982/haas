import { describe, expect, it } from "vitest";
import type { AgentStrategy } from "../../domain/ports/agent-strategy.js";
import type { ExecutionTarget } from "../../domain/ports/execution-target.js";
import type { SignalSource } from "../../domain/ports/signal-source.js";
import type { SessionResult } from "../../domain/value-objects/session-result.js";
import type { Signal } from "../../domain/value-objects/signal.js";
import { SessionConfigTestBuilder } from "../../testharness/session-config-test-builder.js";
import { SessionResultTestBuilder } from "../../testharness/session-result-test-builder.js";
import { SignalTestBuilder } from "../../testharness/signal-test-builder.js";
import { RunSessionUseCase } from "./run-session.js";

describe("RunSessionUseCase", () => {
  it("reads a signal and delivers the session result", async () => {
    // Arrange
    const signal = SignalTestBuilder.create()
      .withPayload("hello")
      .withSource("test")
      .build();
    const expectedResult = SessionResultTestBuilder.create()
      .withOutput("Hi there!")
      .withSessionId("sess-1")
      .build();
    const config = SessionConfigTestBuilder.create().build();
    const source = fakeSignalSource(signal);
    const strategy = fakeStrategy(expectedResult);
    const target = fakeTarget();
    const useCase = new RunSessionUseCase(source, strategy, target);

    // Act
    await useCase.execute(config);

    // Assert
    expect(target.delivered).toEqual(expectedResult);
  });

  it("does nothing when signal source returns null", async () => {
    // Arrange
    const defaultResult = SessionResultTestBuilder.create().build();
    const config = SessionConfigTestBuilder.create().build();
    const source = fakeSignalSource(null);
    const strategy = fakeStrategy(defaultResult);
    const target = fakeTarget();
    const useCase = new RunSessionUseCase(source, strategy, target);

    // Act
    await useCase.execute(config);

    // Assert
    expect(target.delivered).toBeNull();
  });

  it("propagates when the strategy throws", async () => {
    // Arrange
    const signal = SignalTestBuilder.create()
      .withPayload("hello")
      .withSource("test")
      .build();
    const config = SessionConfigTestBuilder.create().build();
    const source = fakeSignalSource(signal);
    const strategy = fakeFailingStrategy(new Error("strategy error"));
    const target = fakeTarget();
    const useCase = new RunSessionUseCase(source, strategy, target);

    // Act & Assert
    await expect(useCase.execute(config)).rejects.toThrow("strategy error");
    expect(target.delivered).toBeNull();
  });

  it("propagates when the target throws", async () => {
    // Arrange
    const signal = SignalTestBuilder.create()
      .withPayload("hello")
      .withSource("test")
      .build();
    const result = SessionResultTestBuilder.create()
      .withOutput("ok")
      .withSessionId("sess-1")
      .build();
    const config = SessionConfigTestBuilder.create().build();
    const source = fakeSignalSource(signal);
    const strategy = fakeStrategy(result);
    const target = fakeFailingTarget(new Error("delivery error"));
    const useCase = new RunSessionUseCase(source, strategy, target);

    // Act & Assert
    await expect(useCase.execute(config)).rejects.toThrow("delivery error");
  });
});

// --- harness (local) ---

function fakeSignalSource(signal: Signal | null): SignalSource {
  return { read: () => Promise.resolve(signal) };
}

function fakeStrategy(expected: SessionResult): AgentStrategy {
  return {
    execute: async (_config, _signal) => expected,
  };
}

function fakeFailingStrategy(error: Error): AgentStrategy {
  return {
    execute: async (_config, _signal) => { throw error; },
  };
}

function fakeFailingTarget(error: Error): ExecutionTarget {
  return {
    deliver: async (_result) => { throw error; },
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
