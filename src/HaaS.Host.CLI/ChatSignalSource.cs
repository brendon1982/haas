using HaaS.Adapters.Deferred;
using HaaS.Domain.Ports;
using HaaS.Domain.ValueObjects;
using HaaS.Host.CLI.Infrastructure;
using Microsoft.Extensions.Hosting;
using Spectre.Console;

namespace HaaS.Host.CLI;

/// <summary>
/// A CLI-based SignalSource for AI Chat.
/// Acts as an adapter between the user's terminal input and the HaaS engine.
/// Demonstrates how to handle real-time input and session-based interactions in a TUI.
/// </summary>
public class ChatSignalSource : ISignalSource
{
    private readonly CliLayoutManager _layoutManager;
    private readonly CliSignalPresenter _presenter;
    private readonly IHostApplicationLifetime? _lifetime;

    public ChatSignalSource(CliLayoutManager layoutManager, CliSignalPresenter presenter, IHostApplicationLifetime? lifetime = null)
    {
        _layoutManager = layoutManager;
        _presenter = presenter;
        _lifetime = lifetime;
    }

    public string Type => "chat";

    public async Task ListenAsync(Func<IncomingSignal, Task<ISignalHandle>> handler)
    {
        _layoutManager.SetMainContent(null);
        AnsiConsole.Clear();
        await AnsiConsole.Live(_layoutManager.Layout)
            .AutoClear(false)
            .StartAsync(async ctx =>
            {
                Action refresh = () => ctx.Refresh();
                _layoutManager.OnLayoutUpdated += refresh;
                try
                {
                    while (true)
                    {
                        var line = await _layoutManager.ReadInputAsync("> ");

                        if (string.IsNullOrWhiteSpace(line))
                            continue;

                        if (line.Equals("/exit", StringComparison.OrdinalIgnoreCase) || line.Equals("/quit", StringComparison.OrdinalIgnoreCase))
                        {
                            _lifetime?.StopApplication();
                            break;
                        }

                        _presenter.AddUserMessage(line);
                        _layoutManager.SetBusy(true);

                        var handle = await handler(new IncomingSignal(line.Trim()));

                        // Wait for the worker to finish and present the result
                        await handle.WaitForResultAsync();

                        _layoutManager.SetBusy(false);
                    }
                }
                finally
                {
                    _layoutManager.OnLayoutUpdated -= refresh;
                }
            });
    }

    public Task ShutdownAsync() => Task.CompletedTask;
}
