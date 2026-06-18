import {
  AuthStorage,
  createAgentSession,
  DefaultResourceLoader,
  getAgentDir,
  ModelRegistry,
  SessionManager,
  SettingsManager,
} from "@earendil-works/pi-coding-agent";
import type { AgentStrategy } from "../../domain/ports/agent-strategy.js";
import type { AgentSessionConfig } from "../../domain/value-objects/agent-session-config.js";
import type { SessionResult } from "../../domain/value-objects/session-result.js";
import type { Signal } from "../../domain/value-objects/signal.js";

export class PiCodingAgentStrategy implements AgentStrategy {
  private readonly authStorage: ReturnType<typeof AuthStorage.create>;
  private readonly modelRegistry: ReturnType<typeof ModelRegistry.create>;

  constructor(modelsJsonPath?: string) {
    this.authStorage = AuthStorage.create();
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

    const resourceLoader = new DefaultResourceLoader({
      cwd: process.cwd(),
      agentDir: getAgentDir(),
      systemPromptOverride: () => config.systemPrompt,
    });
    await resourceLoader.reload();

    const { session } = await createAgentSession({
      model,
      cwd: process.cwd(),
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
