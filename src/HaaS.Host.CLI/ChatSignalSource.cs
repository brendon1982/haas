using HaaS.Adapters.Deferred;
using HaaS.Domain.Ports;
using HaaS.Domain.ValueObjects;
using HaaS.Host.CLI.Infrastructure;
using Microsoft.Extensions.Hosting;
using Terminal.Gui;
using Terminal.Gui.Views;

namespace HaaS.Host.CLI;

public class ChatSignalSource : ISignalSource
{
    private readonly GuiLayoutManager _layoutManager;
    private readonly GuiSignalPresenter _presenter;
    private readonly IHostApplicationLifetime? _lifetime;
    private TaskCompletionSource? _tcs;

    public ChatSignalSource(GuiLayoutManager layoutManager, GuiSignalPresenter presenter, IHostApplicationLifetime? lifetime = null)
    {
        _layoutManager = layoutManager;
        _presenter = presenter;
        _lifetime = lifetime;
    }

    public string Type => "chat";

    public async Task ListenAsync(Func<IncomingSignal, Task<ISignalHandle>> handler)
    {
        _tcs = new TaskCompletionSource();
        
        var chatView = new ChatView(_presenter, _layoutManager);
        
        chatView.OnMessageSent += async message =>
        {
            if (message.Equals("/exit", StringComparison.OrdinalIgnoreCase) || message.Equals("/quit", StringComparison.OrdinalIgnoreCase))
            {
                _tcs.SetResult();
                return;
            }

            _presenter.AddUserMessage(message);
            var handle = await handler(new IncomingSignal(message.Trim()));
            await handle.WaitForResultAsync();
        };

        _layoutManager.SetMainContent(chatView);
        chatView.FocusInput();

        await _tcs.Task;
        
        _layoutManager.SetMainContent(new Label() { Text = "Returning to menu..." });
        _lifetime?.StopApplication();
    }

    public Task ShutdownAsync()
    {
        _tcs?.TrySetResult();
        return Task.CompletedTask;
    }
}
