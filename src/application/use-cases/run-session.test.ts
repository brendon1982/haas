import { describe, expect, it } from "vitest";
import type { AgentStrategy } from "../../domain/ports/agent-strategy.js";
import type { ExecutionTarget } from "../../domain/ports/execution-target.js";
import type { SessionResult } from "../../domain/value-objects/session-result.js";
import type { Signal } from "../../domain/value-objects/signal.js";
import { SessionConfigTestBuilder } from "../../testharness/session-config-test-builder.js";
import { SessionResultTestBuilder } from "../../testharness/session-result-test-builder.js";
import { SignalTestBuilder } from "../../testharness/signal-test-builder.js";
import { RunSessionUseCase } from "./run-session.js";

describe("RunSessionUseCase", () => {
  it("executes the signal through the strategy and delivers the result", async () => {
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
    const strategy = fakeStrategy(expectedResult);
    const target = fakeTarget();
    const useCase = new RunSessionUseCase(strategy, target);

    // Act
    await useCase.execute(config, signal);

    // Assert
    expect(target.delivered).toEqual(expectedResult);
  });

  it("propagates when the strategy throws", async () => {
    // Arrange
    const signal = SignalTestBuilder.create()
      .withPayload("hello")
      .withSource("test")
      .build();
    const config = SessionConfigTestBuilder.create().build();
    const strategy = fakeFailingStrategy(new Error("strategy error"));
    const target = fakeTarget();
    const useCase = new RunSessionUseCase(strategy, target);

    // Act & Assert
    await expect(useCase.execute(config, signal)).rejects.toThrow("strategy error");
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
    const strategy = fakeStrategy(result);
    const target = fakeFailingTarget(new Error("delivery error"));
    const useCase = new RunSessionUseCase(strategy, target);

    // Act & Assert
    await expect(useCase.execute(config, signal)).rejects.toThrow("delivery error");
  });
});

// --- harness (local) ---

function fakeStrategy(expected: SessionResult): AgentStrategy {
  return {
    execute: async (_config, _signal) => expected,
  };
}

function fakeFailingStrategy(error: Error): AgentStrategy {
  return {
    execute: async (_config, _signal) => {
      throw error;
    },
  };
}

function fakeFailingTarget(error: Error): ExecutionTarget {
  return {
    deliver: async (_result) => {
      throw error;
    },
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
