namespace HaaS.Host.CLI;

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
            Console.Clear();
            Console.WriteLine("HaaS Test Bed");
            Console.WriteLine(new string('=', 40));
            Console.WriteLine();

            for (var i = 0; i < _modules.Count; i++)
            {
                var module = _modules[i];
                Console.WriteLine($"  {i + 1}. {module.Name} — {module.Description}");
            }

            Console.WriteLine($"  {_modules.Count + 1}. Settings (coming soon)");
            Console.WriteLine();
            Console.WriteLine("  0. Exit");
            Console.WriteLine();
            Console.Write("> ");

            var input = Console.ReadLine();

            if (input == "0" || input is null)
                break;

            if (int.TryParse(input, out var index) && index >= 1 && index <= _modules.Count)
            {
                await _modules[index - 1].RunAsync(ct);
                Console.WriteLine();
                Console.WriteLine("Press any key to return to menu...");
                Console.ReadKey(true);
            }
            else if (index == _modules.Count + 1)
            {
                Console.WriteLine();
                Console.WriteLine("Settings not yet implemented.");
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey(true);
            }
        }
    }
}
