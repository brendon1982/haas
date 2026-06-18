import {
  AuthStorage,
  createAgentSession,
  ModelRegistry,
  SessionManager,
  SettingsManager,
} from "@earendil-works/pi-coding-agent";
import type { AgentStrategy } from "../../domain/ports/agent-strategy.js";
import type { AgentSessionConfig } from "../../domain/value-objects/agent-session-config.js";
import type { SessionResult } from "../../domain/value-objects/session-result.js";
import type { Signal } from "../../domain/value-objects/signal.js";
import { NoopResourceLoader } from "./noop-resource-loader.js";

export class PiCodingAgentStrategy implements AgentStrategy {
  private readonly authStorage: ReturnType<typeof AuthStorage.inMemory>;
  private readonly modelRegistry: ReturnType<typeof ModelRegistry.create>;

  constructor(modelsJsonPath?: string) {
    this.authStorage = AuthStorage.inMemory();
    this.modelRegistry = ModelRegistry.create(this.authStorage, modelsJsonPath);
  }

  async execute(config: AgentSessionConfig, signal: Signal): Promise<SessionResult> {
    const model = this.modelRegistry.find(config.provider, config.modelId);
    if (!model) {
      throw new Error(
        `Model not found: ${config.provider}/${config.modelId}. Check your models.json configuration.`,
      );
    }

    const settingsManager = SettingsManager.inMemory({
      compaction: { enabled: false },
      retry: { enabled: true, maxRetries: 1 },
    });

    const resourceLoader = new NoopResourceLoader(config.systemPrompt);

    const { session } = await createAgentSession({
      model,
      tools: config.tools.length > 0 ? [...config.tools] : undefined,
      noTools: config.tools.length === 0 ? "all" : undefined,
      sessionManager: SessionManager.inMemory(),
      authStorage: this.authStorage,
      modelRegistry: this.modelRegistry,
      settingsManager,
      resourceLoader,
      thinkingLevel: config.thinkingLevel as never,
    });

    let output = "";
    const unsubscribe = session.subscribe((event) => {
      if (
        event.type === "message_update" &&
        event.assistantMessageEvent.type === "text_delta"
      ) {
        output += event.assistantMessageEvent.delta;
      }
    });

    try {
      await session.prompt(signal.payload);
    } finally {
      unsubscribe();
      session.dispose();
    }

    return { output, sessionId: session.sessionId };
  }
}
