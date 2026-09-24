using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using PKBank.Core.Configuration;
using PKBank.Desktop.Components;
using PKBank.Desktop.Components.Common.ConfirmationWindow;
using PKBank.Desktop.Components.Common.DirectoryPathList;
using PKBank.Desktop.Services.Banks;

namespace PKBank.Desktop.Components.Settings;

public sealed partial class SettingsWindow : Window
{
    private readonly bool _loading = true;
    private bool _applyingBanksPath;

    private readonly ObservableCollection<string> _savPaths = [];
    private readonly MainWindowViewModel? _vm;

    public SettingsWindow() => InitializeComponent(); // designer

    public SettingsWindow(MainWindowViewModel vm) : this()
    {
        _vm = vm;

        LanguageCombo.ItemsSource = GameLanguages.All.Select(l => l.DisplayName).ToArray();
        LanguageCombo.SelectedIndex = IndexOfLanguage(vm.Config.Language);

        SavFolders.Paths = _savPaths;
        Sync(_savPaths, vm.Config.SavPaths);

        BanksPathBox.Text = vm.Bank.Root;
        if (vm.Config.BanksPath.Length != 0 && !BankLibrary.IsSamePath(vm.Config.BanksPath, vm.Bank.Root))
            ShowBanksPathNote(vm.Config.BanksPath);

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

    // ----- Banks folder --------------------------------------------------------

    private void OnBanksPathKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;
        e.Handled = true;
        _ = ApplyBanksPathAsync(BanksPathBox.Text ?? string.Empty);
    }

    private void OnBanksPathLostFocus(object? sender, RoutedEventArgs e) =>
        _ = ApplyBanksPathAsync(BanksPathBox.Text ?? string.Empty);

    private async void OnBanksBrowseClicked(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Banks Folder",
            AllowMultiple = false
        });
        if (folders.Select(static f => f.TryGetLocalPath()).OfType<string>().FirstOrDefault() is { } path)
            await ApplyBanksPathAsync(path);
    }

    private void OnBanksDefaultClicked(object? sender, RoutedEventArgs e) =>
        _ = ApplyBanksPathAsync(string.Empty);

    /// <summary>
    ///     Switches the banks folder, asking whether the existing banks should come along. A path that
    ///     cannot be used falls back to the default folder, and the note under the field says so.
    /// </summary>
    private async Task ApplyBanksPathAsync(string text)
    {
        if (_vm is not { } vm || _applyingBanksPath)
            return;

        var current = vm.Bank.Root;
        if (text.Trim().Length != 0 && BankLibrary.IsSamePath(text, current))
            return; // unchanged, or focus merely left the field

        _applyingBanksPath = true;
        try
        {
            var target = BankLibrary.ResolveRoot(text);
            if (text.Trim().Length != 0 && !BankLibrary.IsSamePath(text, target))
                ShowBanksPathNote(text);
            else
                BanksPathNote.IsVisible = false;

            var move = false;
            if (!BankLibrary.IsSamePath(target, current))
            {
                var message = $"Move the existing banks from “{current}” to “{target}”?";
                if (vm.Bank.IsDirty)
                    message += " Unsaved bank changes are kept only if the banks are moved.";
                var answer = await ConfirmationWindow.ShowAsync(this, "Banks Folder", message, "Move",
                    allowSave: true, saveText: "Don't Move");
                if (answer == ConfirmationResult.Cancel)
                    return;
                move = answer == ConfirmationResult.Confirm;
            }

            if (vm.Bank.ChangeRoot(target, move) is { } error)
                await ConfirmationWindow.ShowMessageAsync(this, "Banks Folder", error);
        }
        finally
        {
            BanksPathBox.Text = vm.Bank.Root;
            _applyingBanksPath = false;
        }
    }

    private void ShowBanksPathNote(string rejected)
    {
        BanksPathNote.Text = $"“{rejected.Trim()}” cannot be used; the default banks folder is used instead.";
        BanksPathNote.IsVisible = true;
    }

    private static void Sync(ObservableCollection<string> target, IReadOnlyList<string> source)
    {
        target.Clear();
        foreach (var path in source)
            target.Add(path);
    }

    private void OnCloseClicked(object? sender, RoutedEventArgs e) => Close();
}
