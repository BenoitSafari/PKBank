using System.Collections;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using PKBank.Core.Configuration;
using PKBank.Core.Events.Files;
using PKBank.Desktop.Services.SaveFiles;
using PKBank.Desktop.ViewModels;
using PKBank.Desktop.Views.Components.Common.ConfirmationWindow;
using PKBank.Desktop.Views.Components.Events;
using PKBank.Desktop.Views.Components.Inventory;
using PKBank.Desktop.Views.Components.Pokedex;
using PKBank.Desktop.Views.Components.Roamer;
using PKBank.Desktop.Views.Components.Trainer;
using PKBank.Desktop.Views.Settings;
using PKHeX.Core;

namespace PKBank.Desktop.Views;

public sealed partial class MainWindowMenu : UserControl
{
    private TopLevel? _gestureHost;

    public MainWindowMenu()
    {
        InitializeComponent();
        AboutMenuItem.Header = $"_About {AppInfo.Name}…";
        DataContextChanged += (_, _) =>
        {
            if (ViewModel is { } vm)
                BuildLanguageMenu(vm);
        };
    }

    private MainWindowViewModel? ViewModel => DataContext as MainWindowViewModel;

    private Window? Host => TopLevel.GetTopLevel(this) as Window;

    private IStorageProvider? Storage => TopLevel.GetTopLevel(this)?.StorageProvider;

    #region Keyboard shortcuts

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _gestureHost = TopLevel.GetTopLevel(this);
        _gestureHost?.AddHandler(KeyDownEvent, OnHostKeyDown, RoutingStrategies.Tunnel);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _gestureHost?.RemoveHandler(KeyDownEvent, OnHostKeyDown);
        _gestureHost = null;
        base.OnDetachedFromVisualTree(e);
    }

    private void OnHostKeyDown(object? sender, KeyEventArgs e)
    {
        var gesture = new KeyGesture(e.Key, e.KeyModifiers);
        if (FindGestureItem(RootMenu.Items, gesture) is not { IsEnabled: true } item)
            return;
        item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        e.Handled = true;
    }

    private static MenuItem? FindGestureItem(IEnumerable items, KeyGesture gesture)
    {
        foreach (var entry in items)
        {
            if (entry is not MenuItem item)
                continue;
            if (gesture.Equals(item.InputGesture))
                return item;
            if (FindGestureItem(item.Items, gesture) is { } nested)
                return nested;
        }

        return null;
    }

    #endregion

    #region File

    private async void OnOpenClicked(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm && TopLevel.GetTopLevel(this) is { } top)
            await SaveFileDialogs.OpenAsync(top, vm);
    }

    private async void OnSaveAsClicked(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not { SAV: not null } vm || Storage is not { } storage)
            return;

        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export Save File",
            SuggestedFileName = "main",
            ShowOverwritePrompt = true
        });

        var path = file?.TryGetLocalPath();
        if (path is not null)
            vm.TrySaveTo(path);
    }

    private async void OnCloseClicked(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not { SAV: { } sav } vm || Host is not { } host)
            return;

        if (sav.State.Edited)
        {
            const string message =
                "The currently loaded save has unsaved changes.\n\nClosing it will discard them. Continue?";
            if (!await ConfirmationWindow.ShowAsync(host, "Close Save File", message, "Close"))
                return;
        }

        vm.CloseSave();
    }

    private void OnExitClicked(object? sender, RoutedEventArgs e) => Host?.Close();

    #endregion

    #region Edit

    private async void OnTrainerClicked(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is { SAV: { } sav } vm && Host is { } host)
        {
            await new TrainerEditorWindow(sav).ShowDialog(host);
            vm.RefreshTrainerInfo();
        }
    }

    private async void OnPokedexClicked(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is { SAV: { HasPokeDex: true } sav } && Host is { } host)
            await new PokedexEditorWindow(sav).ShowDialog(host);
    }

    private async void OnInventoryClicked(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is { SAV: { } sav } && sav.Inventory.Pouches.Count > 0 && Host is { } host)
            await new InventoryEditorWindow(sav).ShowDialog(host);
    }

    private async void OnRoamerClicked(object? sender, RoutedEventArgs e)
    {
        if (Host is not { } host)
            return;
        if (ViewModel?.SAV is SAV3 s3)
            await new Roamer3EditorWindow(s3).ShowDialog(host);
        else if (ViewModel?.SAV is SAV4 s4)
            await new Roamer4EditorWindow(s4).ShowDialog(host);
        else if (ViewModel?.SAV is SAV6XY xy)
            await new Roamer6EditorWindow(xy).ShowDialog(host);
    }

    private async void OnMysteryGiftClicked(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is { SAV: IMysteryGiftStorageProvider and SaveFile sav } && Host is { } host)
            await new MysteryGiftEditorWindow(sav).ShowDialog(host);
    }

    #endregion

    #region Gen 3 events

    private async void OnWC3Clicked(object? sender, RoutedEventArgs e) =>
        await OpenGen3EventAsync(Gen3EventFileKind.WC3);

    private async void OnME3Clicked(object? sender, RoutedEventArgs e) =>
        await OpenGen3EventAsync(Gen3EventFileKind.ME3);

    private async void OnECTClicked(object? sender, RoutedEventArgs e) =>
        await OpenGen3EventAsync(Gen3EventFileKind.ECT);

    private async void OnECBClicked(object? sender, RoutedEventArgs e) =>
        await OpenGen3EventAsync(Gen3EventFileKind.ECB);

    private async void OnWN3Clicked(object? sender, RoutedEventArgs e) =>
        await OpenGen3EventAsync(Gen3EventFileKind.WN3);

    private async Task OpenGen3EventAsync(Gen3EventFileKind kind)
    {
        if (ViewModel is { SAV: SAV3 sav } && Host is { } host)
            await new Gen3EventFileWindow(sav, kind).ShowDialog(host);
    }

    private async void OnRecordMixingClicked(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is { SAV: SAV3 sav } && sav.LargeBlock is ISaveBlock3LargeHoenn && Host is { } host)
            await new RecordMixingWindow(sav).ShowDialog(host);
    }

    #endregion

    #region Options

    private void BuildLanguageMenu(MainWindowViewModel vm)
    {
        LanguageMenu.Items.Clear();
        foreach (var (code, name) in GameLanguages.All)
        {
            var item = new MenuItem
            {
                Header = name,
                ToggleType = MenuItemToggleType.Radio,
                IsChecked = code == vm.Config.Language
            };
            item.Click += (_, _) =>
            {
                vm.SetLanguage(code);
                BuildLanguageMenu(vm); // refresh check marks
            };
            LanguageMenu.Items.Add(item);
        }
    }

    private async void OnSettingsClicked(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not { } vm || Host is not { } host)
            return;

        await new SettingsWindow(vm).ShowDialog(host);
        BuildLanguageMenu(vm); // language may have changed from the settings screen
    }

    private async void OnAboutClicked(object? sender, RoutedEventArgs e)
    {
        if (Host is { } host)
            await new AboutWindow().ShowDialog(host);
    }

    #endregion
}
