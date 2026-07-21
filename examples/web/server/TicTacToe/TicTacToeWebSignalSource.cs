namespace HaaS.Host.Web.TicTacToe;

public class TicTacToeWebSignalSource : WebSignalSource
{
    public TicTacToeWebSignalSource(WebSignalBus bus) : base("tictactoe", bus) { }
}
