using Microsoft.AspNetCore.SignalR;

namespace HaaS.Host.Web.TicTacToe;

public class TicTacToeHub : Hub
{
    private readonly TicTacToeHubHandlers _handlers;

    public TicTacToeHub(TicTacToeHubHandlers handlers)
    {
        _handlers = handlers;
    }

    public async Task SendMove(int position)
    {
        await _handlers.SendMove(this, position);
    }

    public async Task ResetGame()
    {
        await _handlers.ResetGame(this);
    }
}
