using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using PKBank.Core.Configuration;
using PKBank.Desktop.ViewModels;

namespace PKBank.Desktop.Views.Settings;

public sealed partial class SettingsWindow : Window
{
    private readonly MainWindowViewModel? _vm;
    private readonly bool _loading = true;

    public SettingsWindow() => InitializeComponent(); // designer

    public SettingsWindow(MainWindowViewModel vm) : this()
    {
        _vm = vm;

        LanguageCombo.ItemsSource = GameLanguages.All.Select(l => l.DisplayName).ToArray();
        LanguageCombo.SelectedIndex = IndexOfLanguage(vm.Config.Language);

        _loading = false;
    }

    private static int IndexOfLanguage(string code)
    {
        for (var i = 0; i < GameLanguages.All.Count; i++)
        {
            if (GameLanguages.All[i].Code == code)
                return i;
        }
        return 1; // en
    }

    private void OnLanguageChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_loading || _vm is not { } vm || LanguageCombo.SelectedIndex < 0)
            return;
        vm.SetLanguage(GameLanguages.All[LanguageCombo.SelectedIndex].Code);
    }


    private void OnCloseClicked(object? sender, RoutedEventArgs e) => Close();
}
