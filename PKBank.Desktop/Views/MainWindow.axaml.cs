using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using PKBank.Desktop.ViewModels;
using PKHeX.Core;

namespace PKBank.Desktop.Views;

public sealed partial class MainWindow : Window
{
    private static readonly DataFormat<SlotViewModel> SlotDragFormat =
        DataFormat.CreateInProcessFormat<SlotViewModel>("pkhex-avalonia-slot");
    private const double DragThreshold = 6;

    private MainWindowViewModel? ViewModel => DataContext as MainWindowViewModel;

    private SlotViewModel? _pressedSlot;
    private PointerPressedEventArgs? _pressArgs;
    private Point _pressPoint;
    private bool _dragInProgress;

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
        InitializeSlotDragDrop();
    }

    // ----- Slot drag & drop -------------------------------------------------
    // Dragging a slot moves/swaps it onto another slot; the data object also
    // carries a temp .pk* file so dropping onto a file manager exports it.

    private void InitializeSlotDragDrop()
    {
        foreach (var area in new Control[] { BoxItems, PartyItems })
        {
            area.AddHandler(PointerPressedEvent, OnSlotAreaPointerPressed, RoutingStrategies.Tunnel);
            area.AddHandler(PointerMovedEvent, OnSlotAreaPointerMoved, RoutingStrategies.Tunnel);
            area.AddHandler(PointerReleasedEvent, OnSlotAreaPointerReleased, RoutingStrategies.Tunnel);
            DragDrop.SetAllowDrop(area, true);
            area.AddHandler(DragDrop.DragOverEvent, OnSlotDragOver);
            area.AddHandler(DragDrop.DropEvent, OnSlotDrop);
        }
    }

    private static SlotViewModel? FindSlot(object? source)
    {
        for (var control = source as Control; control is not null; control = control.Parent as Control)
        {
            if (control.DataContext is SlotViewModel slot)
                return slot;
            if (control is ItemsControl)
                break;
        }
        return null;
    }

    private void OnSlotAreaPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;
        var slot = FindSlot(e.Source);
        _pressedSlot = slot is { IsEmpty: false } ? slot : null;
        _pressArgs = _pressedSlot is null ? null : e;
        _pressPoint = e.GetPosition(this);
    }

    private void OnSlotAreaPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _pressedSlot = null;
        _pressArgs = null;
    }

    private async void OnSlotAreaPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_pressedSlot is not { } slot || _pressArgs is not { } pressArgs || _dragInProgress)
            return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _pressedSlot = null;
            _pressArgs = null;
            return;
        }
        var delta = e.GetPosition(this) - _pressPoint;
        if (Math.Abs(delta.X) < DragThreshold && Math.Abs(delta.Y) < DragThreshold)
            return;

        _dragInProgress = true;
        try
        {
            var transfer = new DataTransfer();
            transfer.Add(DataTransferItem.Create(SlotDragFormat, slot));
            await AttachExportFile(transfer, slot);
            await DragDrop.DoDragDropAsync(pressArgs, transfer, DragDropEffects.Move | DragDropEffects.Copy);
        }
        finally
        {
            _dragInProgress = false;
            _pressedSlot = null;
            _pressArgs = null;
        }
    }

    /// <summary>Writes the Pokémon to a temp .pk* file (WinForms drag-out format) for external drops.</summary>
    private async System.Threading.Tasks.Task AttachExportFile(DataTransfer transfer, SlotViewModel slot)
    {
        try
        {
            var pk = slot.Read();
            var path = FileUtil.GetPKMTempFileName(pk, encrypt: false);
            pk.ForcePartyData();
            var buffer = new byte[pk.SIZE_PARTY];
            pk.WriteDecryptedDataParty(buffer);
            File.WriteAllBytes(path, buffer);

            var file = await StorageProvider.TryGetFileFromPathAsync(path);
            if (file is not null)
                transfer.Add(DataTransferItem.CreateFile(file));
        }
        catch
        {
            // External export is best-effort; slot-to-slot dragging still works.
        }
    }

    private void OnSlotDragOver(object? sender, DragEventArgs e)
    {
        var valid = e.DataTransfer.Contains(SlotDragFormat) && FindSlot(e.Source) is not null;
        e.DragEffects = valid ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnSlotDrop(object? sender, DragEventArgs e)
    {
        if (e.DataTransfer.TryGetValue(SlotDragFormat) is not { } source)
            return;
        if (FindSlot(e.Source) is not { } target)
            return;
        ViewModel?.MoveOrSwapSlots(source, target);
        e.Handled = true;
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
            ViewModel?.ViewSlot(slot);
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
