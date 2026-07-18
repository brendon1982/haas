using HaaS.Adapters.Deferred;
using HaaS.Domain.Ports;
using HaaS.Domain.ValueObjects;
using HaaS.Host.CLI.Infrastructure;
using Microsoft.Extensions.Hosting;
using Spectre.Console;

namespace HaaS.Host.CLI;

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
        await AnsiConsole.Live(_layoutManager.Layout)
            .AutoClear(false)
            .StartAsync(async ctx =>
            {
                while (true)
                {
                    UpdateLayout(ctx);

                    var line = await ReadLineAsync(ctx);

                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    if (line.Equals("/exit", StringComparison.OrdinalIgnoreCase) || line.Equals("/quit", StringComparison.OrdinalIgnoreCase))
                    {
                        _lifetime?.StopApplication();
                        break;
                    }

                    _presenter.AddUserMessage(line);
                    _layoutManager.SetBusy(true);
                    UpdateLayout(ctx);

                    var handle = await handler(new IncomingSignal(line.Trim()));

                    // Wait for the worker to finish and present the result
                    await handle.WaitForResultAsync();
                    
                    _layoutManager.SetBusy(false);
                    UpdateLayout(ctx);
                }
            });
    }

    private void UpdateLayout(LiveDisplayContext ctx)
    {
        ctx.Refresh();
    }

    private async Task<string> ReadLineAsync(LiveDisplayContext ctx)
    {
        var input = string.Empty;
        while (true)
        {
            _layoutManager.SetInput($"> {input}_");
            ctx.Refresh();

            if (!Console.KeyAvailable)
            {
                await Task.Delay(50);
                continue;
            }

            var key = Console.ReadKey(true);
            
            // Handle scrolling
            if (key.Key == ConsoleKey.PageUp)
            {
                _layoutManager.Scroll(5);
                ctx.Refresh();
                continue;
            }
            if (key.Key == ConsoleKey.PageDown)
            {
                _layoutManager.Scroll(-5);
                ctx.Refresh();
                continue;
            }
            if (key.Key == ConsoleKey.UpArrow && string.IsNullOrEmpty(input))
            {
                _layoutManager.Scroll(1);
                ctx.Refresh();
                continue;
            }
            if (key.Key == ConsoleKey.DownArrow && string.IsNullOrEmpty(input))
            {
                _layoutManager.Scroll(-1);
                ctx.Refresh();
                continue;
            }

            if (key.Key == ConsoleKey.Enter)
            {
                _layoutManager.SetInput(string.Empty);
                ctx.Refresh();
                return input;
            }
            
            if (key.Key == ConsoleKey.Backspace)
            {
                if (input.Length > 0)
                {
                    input = input[..^1];
                }
            }
            else if (!char.IsControl(key.KeyChar))
            {
                input += key.KeyChar;
            }
        }
    }

    public Task ShutdownAsync() => Task.CompletedTask;
}
