using HaaS.Domain.ValueObjects;
using HaaS.Host.Web.TicTacToe;
using Microsoft.AspNetCore.SignalR;

namespace HaaS.Host.Web;

public class HaaSWebHub : Hub
{
    private readonly WebSignalBus _bus;
    private readonly TicTacToeHubHandlers _ticTacToeHandlers;

    public HaaSWebHub(WebSignalBus bus, TicTacToeHubHandlers ticTacToeHandlers)
    {
        _bus = bus;
        _ticTacToeHandlers = ticTacToeHandlers;
    }

    public async Task SendMessage(string source, string message)
    {
        await _bus.PushAsync(source, new IncomingSignal(message, Context.ConnectionId));
    }

    public async Task SendMove(int position)
    {
        await _ticTacToeHandlers.SendMove(this, position);
    }

    public async Task ResetGame()
    {
        await _ticTacToeHandlers.ResetGame(this);
    }
}
