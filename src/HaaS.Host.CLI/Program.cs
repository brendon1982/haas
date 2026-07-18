using HaaS.Host.CLI;
using HaaS.Host.CLI.Infrastructure;
using HaaS.Host.CLI.TicTacToe;

var logSink = new CliLogSink();
var layout = new CliLayoutManager(logSink);
var menu = new CliMenu(new ICliModule[] { new ChatModule(), new TicTacToeModule() }, layout);
await menu.RunAsync();
