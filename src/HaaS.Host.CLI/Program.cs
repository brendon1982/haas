using HaaS.Host.CLI;
using HaaS.Host.CLI.Infrastructure;
using HaaS.Host.CLI.TicTacToe;

using var layout = new GuiLayoutManager();
var menu = new GuiMenu(new ICliModule[] { new ChatModule(), new TicTacToeModule() }, layout);
layout.SetMainContent(menu);
layout.Run();
