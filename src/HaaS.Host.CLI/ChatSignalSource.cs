using HaaS.Adapters.Deferred;
using HaaS.Domain.Ports;
using HaaS.Domain.ValueObjects;
using HaaS.Host.CLI.Infrastructure;
using Spectre.Console;

namespace HaaS.Host.CLI;

public class ChatSignalSource : ISignalSource
{
    private readonly CliLayoutManager _layoutManager;
    private readonly CliSignalPresenter _presenter;

    public ChatSignalSource(CliLayoutManager layoutManager, CliSignalPresenter presenter)
    {
        _layoutManager = layoutManager;
        _presenter = presenter;
    }

    public string Type => "chat";

    public async Task ListenAsync(Func<IncomingSignal, Task<ISignalHandle>> handler)
    {
        while (true)
        {
            var line = AnsiConsole.Prompt(
                new TextPrompt<string>("[green]>[/]")
                    .AllowEmpty());

            if (string.IsNullOrWhiteSpace(line))
                break;

            _presenter.AddUserMessage(line);

            var handle = await handler(new IncomingSignal(line.Trim()));

            // Wait for the worker to finish and present the result
            // Use RunLiveAsync to keep the log pane updating while AI is thinking
            await _layoutManager.RunLiveAsync(async () =>
            {
                await handle.WaitForResultAsync();
            });
        }
    }

    public Task ShutdownAsync() => Task.CompletedTask;
}
