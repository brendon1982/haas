export interface AgentSessionConfig {
  readonly provider: string;
  readonly modelId: string;
  readonly systemPrompt: string;
  readonly tools: readonly string[];
  readonly thinkingLevel: string;
}
