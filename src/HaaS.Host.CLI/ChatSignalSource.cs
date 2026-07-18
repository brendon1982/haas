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
                Action refresh = () => ctx.Refresh();
                _layoutManager.OnLayoutUpdated += refresh;
                try
                {
                    while (true)
                    {
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

    private void UpdateLayout(LiveDisplayContext ctx)
    {
        ctx.Refresh();
    }

    private async Task<string> ReadLineAsync(LiveDisplayContext ctx)
    {
        var input = string.Empty;
        _layoutManager.SetInput($"> {input}_");

        while (true)
        {
            // Block until a key is available (no polling)
            var key = await Task.Run(() => Console.ReadKey(true));
            
            bool finished = ProcessKey(key, ref input);
            
            // Drain any other keys that were pressed simultaneously
            while (!finished && Console.KeyAvailable)
            {
                key = Console.ReadKey(true);
                finished = ProcessKey(key, ref input);
            }

            if (finished)
            {
                _layoutManager.SetInput(string.Empty);
                return input;
            }

            _layoutManager.SetInput($"> {input}_");
        }
    }

    private bool ProcessKey(ConsoleKeyInfo key, ref string input)
    {
        // Handle scrolling
        if (key.Key == ConsoleKey.PageUp)
        {
            _layoutManager.Scroll(5);
        }
        else if (key.Key == ConsoleKey.PageDown)
        {
            _layoutManager.Scroll(-5);
        }
        else if (key.Key == ConsoleKey.UpArrow && string.IsNullOrEmpty(input))
        {
            _layoutManager.Scroll(1);
        }
        else if (key.Key == ConsoleKey.DownArrow && string.IsNullOrEmpty(input))
        {
            _layoutManager.Scroll(-1);
        }
        else if (key.Key == ConsoleKey.Enter)
        {
            return true;
        }
        else if (key.Key == ConsoleKey.Backspace)
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
        
        return false;
    }

    public Task ShutdownAsync() => Task.CompletedTask;
}
