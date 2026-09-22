using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using PKBank.Core.Configuration;
using PKBank.Desktop.ViewModels;
using PKBank.Desktop.Views.Components.Common.DirectoryPathList;

namespace PKBank.Desktop.Views.Settings;

public sealed partial class SettingsWindow : Window
{
    private readonly ObservableCollection<string> _bankPaths = [];
    private readonly bool _loading = true;

    private readonly ObservableCollection<string> _savPaths = [];
    private readonly MainWindowViewModel? _vm;

    public SettingsWindow() => InitializeComponent(); // designer

    public SettingsWindow(MainWindowViewModel vm) : this()
    {
        _vm = vm;

        LanguageCombo.ItemsSource = GameLanguages.All.Select(l => l.DisplayName).ToArray();
        LanguageCombo.SelectedIndex = IndexOfLanguage(vm.Config.Language);

        SavFolders.Paths = _savPaths;
        BankFolders.Paths = _bankPaths;
        Sync(_savPaths, vm.Config.SavPaths);
        Sync(_bankPaths, vm.Config.BankPaths);

        _loading = false;
    }

    private static int IndexOfLanguage(string code)
    {
        for (var i = 0; i < GameLanguages.All.Count; i++)
            if (GameLanguages.All[i].Code == code)
                return i;
        return 1; // en
    }

    private void OnLanguageChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_loading || _vm is null || LanguageCombo.SelectedIndex < 0)
            return;
        _vm.SetLanguage(GameLanguages.All[LanguageCombo.SelectedIndex].Code);
    }

    private void OnSavPathAdded(object? sender, DirectoryPathEventArgs e)
    {
        if (_vm != null && _vm.Config.AddSavPath(e.Path))
            Sync(_savPaths, _vm.Config.SavPaths);
    }

    private void OnSavPathRemoved(object? sender, DirectoryPathEventArgs e)
    {
        if (_vm != null && _vm.Config.RemoveSavPath(e.Path))
            Sync(_savPaths, _vm.Config.SavPaths);
    }

    private void OnBankPathAdded(object? sender, DirectoryPathEventArgs e)
    {
        if (_vm != null && _vm.Config.AddBankPath(e.Path))
            Sync(_bankPaths, _vm.Config.BankPaths);
    }

    private void OnBankPathRemoved(object? sender, DirectoryPathEventArgs e)
    {
        if (_vm != null && _vm.Config.RemoveBankPath(e.Path))
            Sync(_bankPaths, _vm.Config.BankPaths);
    }

    private static void Sync(ObservableCollection<string> target, IReadOnlyList<string> source)
    {
        target.Clear();
        foreach (var path in source)
            target.Add(path);
    }

    private void OnCloseClicked(object? sender, RoutedEventArgs e) => Close();
}
