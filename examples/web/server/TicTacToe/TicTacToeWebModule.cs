using HaaS.Domain.Ports;
using HaaS.Infrastructure;

namespace HaaS.Host.Web.TicTacToe;

public static class TicTacToeWebModule
{
    public static void AddTicTacToeWebModule(this HaasBuilder.HaasQueuedPoolBuilder pool)
    {
        pool.AddSignalSource<TicTacToeWebSignalSource, TicTacToeWebSignalPresenter>(config =>
        {
            config.UseProvider("openrouter")
                  .UseModel("cohere/north-mini-code:free")
                  .UseSystemPrompt("You are a TicTacToe player. Use tools to play.")
                  .AddTool("get_board")
                  .AddTool("get_valid_moves")
                  .AddTool("place_marker");
        });
    }

    public static void RegisterTicTacToeTools(this IToolProvider toolProvider)
    {
        toolProvider.Register<WebTicTacToeToolHandlers>("get_board", "Returns the current board", h => h.GetBoard);
        toolProvider.Register<WebTicTacToeToolHandlers>("get_valid_moves", "Returns valid moves", h => h.GetValidMoves);
        toolProvider.Register<WebTicTacToeToolHandlers>("place_marker", "Places a marker", (WebTicTacToeToolHandlers h) => (Func<int, Task<string>>)h.PlaceMarker);
    }
}
