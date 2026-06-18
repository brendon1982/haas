import {
  createExtensionRuntime,
  type ResourceLoader,
} from "@earendil-works/pi-coding-agent";

export class NoopResourceLoader implements ResourceLoader {
  private readonly runtime = createExtensionRuntime();
  private readonly systemPrompt: string | undefined;

  constructor(systemPrompt?: string) {
    this.systemPrompt = systemPrompt;
  }

  getExtensions() {
    return { extensions: [], errors: [], runtime: this.runtime };
  }

  getSkills() {
    return { skills: [], diagnostics: [] };
  }

  getPrompts() {
    return { prompts: [], diagnostics: [] };
  }

  getThemes() {
    return { themes: [], diagnostics: [] };
  }

  getAgentsFiles() {
    return { agentsFiles: [] };
  }

  getSystemPrompt(): string | undefined {
    return this.systemPrompt;
  }

  getAppendSystemPrompt(): string[] {
    return [];
  }

  extendResources(_paths: never): void {}

  loadProjectTrustExtensions() {
    return Promise.resolve({ extensions: [], errors: [], runtime: this.runtime });
  }

  reload(_options?: never): Promise<void> {
    return Promise.resolve();
  }
}
