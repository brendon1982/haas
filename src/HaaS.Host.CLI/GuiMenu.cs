using Terminal.Gui;
using Terminal.Gui.Views;
using Terminal.Gui.ViewBase;
using HaaS.Host.CLI.Infrastructure;
using System.Collections.ObjectModel;

namespace HaaS.Host.CLI;

public class GuiMenu : Window
{
    private readonly IEnumerable<ICliModule> _modules;
    private readonly GuiLayoutManager _layoutManager;
    private readonly ListView _listView;

    private readonly List<string> _choices;

    public GuiMenu(IEnumerable<ICliModule> modules, GuiLayoutManager layoutManager)
    {
        Title = "HaaS - Enterprise AI Harness";
        _modules = modules;
        _layoutManager = layoutManager;

        _choices = _modules.Select(m => m.Name).Concat(new[] { "Settings", "Exit" }).ToList();
        
        _listView = new ListView()
        {
            Source = new ListWrapper<string>(new ObservableCollection<string>(_choices)),
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };

        _listView.Accepted += OnSelectedItem;

        Add(_listView);
    }

    private void OnSelectedItem(object? sender, EventArgs args)
    {
        var index = _listView.SelectedItem ?? -1;
        if (index < 0 || index >= _choices.Count) return;
        var choice = _choices[index];
        if (choice == "Exit")
        {
            _layoutManager.Stop();
            return;
        }

        if (choice == "Settings")
        {
            MessageBox.Query(_layoutManager.App, "Settings", "Settings not yet implemented.", "Ok");
            return;
        }

        var module = _modules.FirstOrDefault(m => m.Name == choice);
        if (module != null)
        {
            // In a real TUI, we might want to switch the view
            // For now, let's start the module task
            Task.Run(async () => await module.RunAsync());
        }
    }
}
