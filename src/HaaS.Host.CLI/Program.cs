using HaaS.Host.CLI;

var menu = new CliMenu(new ICliModule[] { new ChatModule(), new TicTacToeModule() });
await menu.RunAsync();
