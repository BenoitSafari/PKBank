using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using PKBank.Desktop.Services;
using PKBank.Desktop.ViewModels;

namespace PKBank.Desktop.Views;

public sealed partial class SettingsWindow : Window
{
    private readonly MainWindowViewModel? _vm;
    private readonly bool _loading = true;

    public SettingsWindow() => InitializeComponent(); // designer

    public SettingsWindow(MainWindowViewModel vm) : this()
    {
        _vm = vm;

        LanguageCombo.ItemsSource = AppSettings.Languages.Select(l => l.DisplayName).ToArray();
        LanguageCombo.SelectedIndex = IndexOfLanguage(vm.Settings.Language);

        BlankVersionCombo.ItemsSource = MainWindowViewModel.NewSaveOptions.Select(o => o.Label).ToArray();
        BlankVersionCombo.SelectedIndex = IndexOfVersion(vm);

        ShinyCheck.IsChecked = vm.Settings.ShinySprites;

        _loading = false;
    }

    private static int IndexOfLanguage(string code)
    {
        for (int i = 0; i < AppSettings.Languages.Count; i++)
        {
            if (AppSettings.Languages[i].Code == code)
                return i;
        }
        return 1; // en
    }

    private static int IndexOfVersion(MainWindowViewModel vm)
    {
        for (int i = 0; i < MainWindowViewModel.NewSaveOptions.Count; i++)
        {
            if (MainWindowViewModel.NewSaveOptions[i].Version == vm.Settings.BlankSaveVersion)
                return i;
        }
        return 0;
    }

    private void OnLanguageChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_loading || _vm is not { } vm || LanguageCombo.SelectedIndex < 0)
            return;
        vm.SetLanguage(AppSettings.Languages[LanguageCombo.SelectedIndex].Code);
    }

    private void OnBlankVersionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_loading || _vm is not { } vm || BlankVersionCombo.SelectedIndex < 0)
            return;
        vm.Settings.BlankSaveVersion = MainWindowViewModel.NewSaveOptions[BlankVersionCombo.SelectedIndex].Version;
        vm.Settings.Save();
    }

    private void OnShinyChanged(object? sender, RoutedEventArgs e)
    {
        if (_loading || _vm is not { } vm)
            return;
        vm.SetShinySprites(ShinyCheck.IsChecked == true);
    }

    private void OnCloseClicked(object? sender, RoutedEventArgs e) => Close();
}
