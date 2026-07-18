namespace HaaS.Host.CLI;

using Spectre.Console;

public class CliMenu
{
    private readonly IReadOnlyList<ICliModule> _modules;

    public CliMenu(IEnumerable<ICliModule> modules)
    {
        _modules = modules.ToList();
    }

    public async Task RunAsync(CancellationToken ct = default)
    {
        while (!ct.IsCancellationRequested)
        {
            AnsiConsole.Clear();
            AnsiConsole.Write(
                new FigletText("HaaS")
                    .LeftJustified()
                    .Color(Color.Blue));
            
            AnsiConsole.Write(new Rule("[yellow]Enterprise AI Harness[/]").LeftJustified());
            AnsiConsole.WriteLine();

            var modules = _modules.Select(m => new MenuChoice(m.Name, m.Description)).ToList();
            var settings = new MenuChoice("Settings", "Configuration and keys (coming soon)");
            var exit = new MenuChoice("Exit");

            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<MenuChoice>()
                    .Title("Select a module to run:")
                    .AddChoices(modules)
                    .AddChoices(settings, exit));

            if (choice == exit)
                break;

            if (choice == settings)
            {
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[yellow]Settings not yet implemented.[/]");
                AnsiConsole.MarkupLine("Press any key to continue...");
                Console.ReadKey(true);
                continue;
            }

            var module = _modules.FirstOrDefault(m => m.Name == choice.Name);
            if (module != null)
            {
                await module.RunAsync(ct);
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[grey]Press any key to return to menu...[/]");
                Console.ReadKey(true);
            }
        }
    }

    private sealed record MenuChoice(string Name, string? Description = null)
    {
        public override string ToString() => Description != null ? $"{Name} [grey]- {Description}[/]" : Name;
    }
}
