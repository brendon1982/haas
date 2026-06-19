using HaaS.Adapters.Agent;
using HaaS.Adapters.Execution;
using HaaS.Adapters.Signal;
using HaaS.Application.UseCases;
using HaaS.Domain.Ports;
using HaaS.Domain.ValueObjects;
using Microsoft.Extensions.AI;

var config = new AgentSessionConfig(
    Provider: "ollama",
    ModelId: "gemma4",
    SystemPrompt: "You are a helpful assistant. Be concise and accurate.",
    Tools: [],
    ThinkingLevel: "off"
);

var chatClient = new OllamaChatClient(
    new Uri("http://localhost:11434"),
    config.ModelId);

IAgentStrategy strategy = new MicrosoftAgentFrameworkStrategy(chatClient);
IExecutionTarget target = new ConsoleExecutionTarget();
var useCase = new RunSessionUseCase(strategy, target);

var source = new CliSignalSource();
await source.ListenAsync(signal => useCase.ExecuteAsync(config, signal));
