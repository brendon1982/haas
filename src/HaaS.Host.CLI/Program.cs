using HaaS.Host.CLI;
using HaaS.Host.CLI.TicTacToe;

var menu = new CliMenu(new ICliModule[] { new ChatModule(), new TicTacToeModule() });
await menu.RunAsync();
