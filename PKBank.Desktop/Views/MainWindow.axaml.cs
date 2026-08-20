using System.ComponentModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using PKBank.Desktop.ViewModels;

namespace PKBank.Desktop.Views;

public sealed partial class MainWindow : Window
{
    private MainWindowViewModel? ViewModel => DataContext as MainWindowViewModel;

    public MainWindow()
    {
        InitializeComponent();
        BuildNewSaveMenu();
        DataContextChanged += (_, _) =>
        {
            if (ViewModel is { } vm)
            {
                vm.PropertyChanged += OnViewModelPropertyChanged;
                SyncBoxCombo(vm);
                BuildLanguageMenu(vm);
            }
        };
    }

    private void BuildLanguageMenu(MainWindowViewModel vm)
    {
        LanguageMenu.Items.Clear();
        foreach (var (code, name) in Services.AppSettings.Languages)
        {
            var item = new MenuItem
            {
                Header = name,
                ToggleType = MenuItemToggleType.Radio,
                IsChecked = code == vm.Settings.Language,
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
        if (ViewModel is not { } vm)
            return;
        await new SettingsWindow(vm).ShowDialog(this);
        BuildLanguageMenu(vm); // language may have changed from the settings screen
    }

    private async void OnAboutClicked(object? sender, RoutedEventArgs e)
        => await new AboutWindow().ShowDialog(this);

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (ViewModel is not { } vm)
            return;
        if (e.PropertyName == nameof(MainWindowViewModel.BoxNames))
            SyncBoxCombo(vm);
        else if (e.PropertyName == nameof(MainWindowViewModel.CurrentBox) && BoxCombo.SelectedIndex != vm.CurrentBox)
            BoxCombo.SelectedIndex = vm.CurrentBox;
    }

    private void SyncBoxCombo(MainWindowViewModel vm)
    {
        BoxCombo.ItemsSource = vm.BoxNames;
        BoxCombo.SelectedIndex = vm.CurrentBox;
    }

    private void OnBoxComboSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ViewModel is { } vm && BoxCombo.SelectedIndex >= 0)
            vm.CurrentBox = BoxCombo.SelectedIndex;
    }

    private void BuildNewSaveMenu()
    {
        foreach (var (label, version) in MainWindowViewModel.NewSaveOptions)
        {
            var item = new MenuItem { Header = label };
            item.Click += (_, _) => ViewModel?.NewBlank(version);
            NewMenu.Items.Add(item);
        }
    }

    private async void OnOpenClicked(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not { } vm)
            return;

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Save File",
            AllowMultiple = false,
        });

        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (path is not null)
            vm.LoadSaveFromPath(path);
    }

    private async void OnSaveAsClicked(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not { SAV: { } sav } vm)
            return;

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export Save File",
            SuggestedFileName = "main",
            ShowOverwritePrompt = true,
        });

        var path = file?.TryGetLocalPath();
        if (path is not null)
            vm.TrySaveTo(path);
    }

    private void OnSlotClicked(object? sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.DataContext is SlotViewModel slot)
            ViewModel?.SelectSlot(slot);
    }

    private static SlotViewModel? GetMenuSlot(object? sender) => (sender as MenuItem)?.DataContext as SlotViewModel;

    private void OnSlotMenuOpening(object? sender, CancelEventArgs e)
    {
        if (sender is not ContextMenu menu)
            return;
        // Same guard as the action bar's Set button: needs a Pokémon in the editor.
        var canSet = ViewModel?.CanSetToSlot == true;
        foreach (var item in menu.Items)
        {
            if (item is MenuItem { Tag: "set" } setItem)
                setItem.IsEnabled = canSet;
        }
    }

    private void OnViewSelectedClicked(object? sender, RoutedEventArgs e) => ViewModel?.ViewSelected();
    private void OnSetSelectedClicked(object? sender, RoutedEventArgs e) => ViewModel?.SetSelected();

    private void OnSlotViewClicked(object? sender, RoutedEventArgs e)
    {
        if (GetMenuSlot(sender) is { } slot)
            ViewModel?.SelectSlot(slot);
    }

    private void OnSlotSetClicked(object? sender, RoutedEventArgs e)
    {
        if (GetMenuSlot(sender) is { } slot)
            ViewModel?.SetSlotFromEditor(slot);
    }

    private void OnSlotDeleteClicked(object? sender, RoutedEventArgs e)
    {
        if (GetMenuSlot(sender) is { } slot)
            ViewModel?.DeleteSlot(slot);
    }

    private void OnDeleteClicked(object? sender, RoutedEventArgs e) => ViewModel?.DeleteSelected();
    private void OnPrevBoxClicked(object? sender, RoutedEventArgs e) => ViewModel?.PrevBox();
    private void OnNextBoxClicked(object? sender, RoutedEventArgs e) => ViewModel?.NextBox();
    private void OnExitClicked(object? sender, RoutedEventArgs e) => Close();
}
