using HaaS.Host.CLI;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddSingleton<CliMenu>();
services.AddSingleton<ICliModule, ChatModule>();
services.AddSingleton<ICliModule, TicTacToeModule>();

var provider = services.BuildServiceProvider();
var menu = provider.GetRequiredService<CliMenu>();
await menu.RunAsync();
