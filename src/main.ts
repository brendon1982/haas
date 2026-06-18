import { PiCodingAgentStrategy } from "./adapter/agent/pi-coding-agent-strategy.ts";
import { ConsoleExecutionTarget } from "./adapter/execution/console-execution-target.ts";
import { CliSignalSource } from "./adapter/signal/cli-signal-source.ts";
import { RunSessionUseCase } from "./application/use-cases/run-session.ts";
import type { AgentSessionConfig } from "./domain/value-objects/agent-session-config.ts";
import { resolve } from "node:path";
import { fileURLToPath } from "node:url";

const dirname = fileURLToPath(new URL(".", import.meta.url));
const modelsJsonPath = resolve(dirname, "../models/ollama.json");

const config: AgentSessionConfig = {
  provider: "ollama",
  modelId: "gemma4",
  systemPrompt: "You are a helpful assistant. Be concise and accurate.",
  tools: [],
  thinkingLevel: "off",
};

const signalSource = new CliSignalSource();
const agentStrategy = new PiCodingAgentStrategy(modelsJsonPath);
const executionTarget = new ConsoleExecutionTarget();
const useCase = new RunSessionUseCase(signalSource, agentStrategy, executionTarget);

await useCase.execute(config);
