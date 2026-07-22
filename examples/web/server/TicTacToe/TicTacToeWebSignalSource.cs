using HaaS.Host.Web.Infrastructure;

namespace HaaS.Host.Web.TicTacToe;

public class TicTacToeWebSignalSource : WebSignalSource
{
    public TicTacToeWebSignalSource(WebSignalBus bus) : base("tictactoe", bus) { }
}
