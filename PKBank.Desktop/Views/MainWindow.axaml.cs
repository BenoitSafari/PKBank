using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using PKBank.Desktop.Services;
using PKBank.Desktop.ViewModels;
using PKHeX.Core;

namespace PKBank.Desktop.Views;

public sealed partial class MainWindow : Window
{
    private const double DragThreshold = 6;

    private MainWindowViewModel? ViewModel => DataContext as MainWindowViewModel;

    private SlotViewModel? _pressedSlot;
    private PointerPressedEventArgs? _pressArgs;
    private Point _pressPoint;
    private bool _dragInProgress;
    private KeyModifiers _clickModifiers;

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
        }
        // Accept the drag anywhere in the window so the cursor never turns
        // "forbidden" mid-flight; the drop itself resolves a slot by hit-test.
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragOverEvent, OnWindowDragOver);
        AddHandler(DragDrop.DropEvent, OnWindowDrop);
        AddHandler(DragDrop.DragEnterEvent, OnWindowDragEnter);
        AddHandler(DragDrop.DragLeaveEvent, OnWindowDragLeave);
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
        _clickModifiers = e.KeyModifiers; // consumed by OnSlotClicked (fires after release)
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
            ShowDragGhost(slot);
            var transfer = new DataTransfer();
            var vm = ViewModel;
            if (vm is { IsMultiSelection: true } && vm.SelectedSlots.Contains(slot))
            {
                // Dragging the multi-selection: no internal move semantics, just
                // one export file per occupied selected slot.
                transfer.Add(DataTransferItem.Create(SlotDragFormats.Multi, "selection"));
                foreach (var member in vm.SelectedSlots)
                {
                    if (!member.IsEmpty)
                        await AttachExportFile(transfer, member);
                }
            }
            else
            {
                transfer.Add(DataTransferItem.Create(SlotDragFormats.Slot, slot));
                await AttachExportFile(transfer, slot);
            }
            // Move only: KDE's KIO drops without its Move/Copy/Link menu only when
            // the proposed action is Move, the file is local and on the same device
            // as the destination, and the user opted into DndBehavior=MoveIfSameDevice.
            await DragDrop.DoDragDropAsync(pressArgs, transfer, DragDropEffects.Move);
        }
        finally
        {
            HideDragGhost();
            _dragInProgress = false;
            _pressedSlot = null;
            _pressArgs = null;
        }
    }

    private void ShowDragGhost(SlotViewModel slot)
    {
        DragGhostImage.Source = slot.Sprite;
        DragGhostLayer.IsVisible = true;
        UpdateDragGhost(_pressPoint);
    }

    private void UpdateDragGhost(Point position)
    {
        Canvas.SetLeft(DragGhostImage, position.X + 10);
        Canvas.SetTop(DragGhostImage, position.Y + 6);
    }

    private void HideDragGhost()
    {
        DragGhostLayer.IsVisible = false;
        DragGhostImage.Source = null;
    }

    /// <summary>Writes the Pokémon to a temp .pk* file (WinForms drag-out format) for external drops.</summary>
    private async System.Threading.Tasks.Task AttachExportFile(DataTransfer transfer, SlotViewModel slot)
    {
        try
        {
            var pk = slot.Read();
            // Write on the same filesystem as the user's home so file managers can
            // Move the export directly (/tmp is usually a different device, tmpfs).
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PKBank.Desktop", "export");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, Path.GetFileName(FileUtil.GetPKMTempFileName(pk, encrypt: false)));
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

    private void OnWindowDragEnter(object? sender, DragEventArgs e)
    {
        if (_dragInProgress)
            DragGhostLayer.IsVisible = true;
    }

    private void OnWindowDragLeave(object? sender, DragEventArgs e)
    {
        // The cursor left the window (e.g. dragging out to a file manager).
        DragGhostLayer.IsVisible = false;
    }

    private void OnWindowDragOver(object? sender, DragEventArgs e)
    {
        if (e.DataTransfer.Contains(SlotDragFormats.Slot) || e.DataTransfer.Contains(SlotDragFormats.Multi))
        {
            e.DragEffects = DragDropEffects.Move;
            if (_dragInProgress)
                UpdateDragGhost(e.GetPosition(DragGhostLayer));
            e.Handled = true;
            return;
        }
        if (e.DataTransfer.Contains(DataFormat.File))
        {
            // External Pokémon file heading for a slot.
            e.DragEffects = DragDropEffects.Copy;
            e.Handled = true;
            return;
        }
        e.DragEffects = DragDropEffects.None;
    }

    private async void OnWindowDrop(object? sender, DragEventArgs e)
    {
        if (e.DataTransfer.Contains(SlotDragFormats.Multi))
            return; // multi-selection drags only mean something outside the app

        if (e.DataTransfer.TryGetValue(SlotDragFormats.Slot) is { } source)
        {
            if (HitTestSlot(e.GetPosition(this)) is not { } target)
                return;
            ViewModel?.MoveOrSwapSlots(source, target);
            e.Handled = true;
            return;
        }

        var path = e.DataTransfer.TryGetFiles()?.FirstOrDefault()?.TryGetLocalPath();
        if (path is null)
            return;

        // A dropped save file replaces the loaded one, wherever it lands.
        if (SaveFileDrop.TryGetSaveFile(path, out var dropped))
        {
            e.Handled = true;
            await LoadDroppedSaveAsync(dropped, path);
            return;
        }

        // External Pokémon file dropped onto a slot: import it there.
        if (HitTestSlot(e.GetPosition(this)) is not { } slot)
            return;
        ViewModel?.TryImportFileToSlot(path, slot);
        e.Handled = true;
    }

    private async Task LoadDroppedSaveAsync(SaveFile dropped, string path)
    {
        if (ViewModel is not { } vm)
            return;

        if (vm.SAV is { } current)
        {
            var name = Path.GetFileName(path);
            var message = current.State.Edited
                ? $"The currently loaded save has unsaved changes.\n\nLoading “{name}” will discard them. Continue?"
                : $"Replace the currently loaded save with “{name}”?";
            if (!await ConfirmationWindow.ShowAsync(this, "Load Save File", message, "Load"))
                return;
        }

        vm.LoadSaveFromPath(dropped, path);
    }

    private SlotViewModel? HitTestSlot(Point position)
        => this.InputHitTest(position) is Control control ? FindSlot(control) : null;

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

    private async void OnTrainerClicked(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is { SAV: { } sav })
        {
            await new Trainer.TrainerEditorWindow(sav).ShowDialog(this);
            ViewModel.RefreshTrainerInfo();
        }
    }

    private async void OnMysteryGiftClicked(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is { SAV: IMysteryGiftStorageProvider and SaveFile sav })
            await new Events.MysteryGiftEditorWindow(sav).ShowDialog(this);
    }

    private async void OnWC3Clicked(object? sender, RoutedEventArgs e)
        => await OpenGen3EventAsync(Events.Gen3EventFileKind.WC3);

    private async void OnME3Clicked(object? sender, RoutedEventArgs e)
        => await OpenGen3EventAsync(Events.Gen3EventFileKind.ME3);

    private async void OnECTClicked(object? sender, RoutedEventArgs e)
        => await OpenGen3EventAsync(Events.Gen3EventFileKind.ECT);

    private async void OnECBClicked(object? sender, RoutedEventArgs e)
        => await OpenGen3EventAsync(Events.Gen3EventFileKind.ECB);

    private async void OnWN3Clicked(object? sender, RoutedEventArgs e)
        => await OpenGen3EventAsync(Events.Gen3EventFileKind.WN3);

    private async Task OpenGen3EventAsync(Events.Gen3EventFileKind kind)
    {
        if (ViewModel is { SAV: SAV3 sav })
            await new Events.Gen3EventFileWindow(sav, kind).ShowDialog(this);
    }

    private async void OnRecordMixingClicked(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is { SAV: SAV3 sav } && sav.LargeBlock is ISaveBlock3LargeHoenn)
            await new Events.RecordMixingWindow(sav).ShowDialog(this);
    }

    private async void OnPokedexClicked(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is { SAV: { HasPokeDex: true } sav })
            await new Pokedex.PokedexEditorWindow(sav).ShowDialog(this);
    }

    private async void OnInventoryClicked(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is { SAV: { } sav } && sav.Inventory.Pouches.Count > 0)
            await new Inventory.InventoryEditorWindow(sav).ShowDialog(this);
    }

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
        if ((sender as Button)?.DataContext is not SlotViewModel slot || ViewModel is not { } vm)
            return;
        if (_clickModifiers.HasFlag(KeyModifiers.Control))
            vm.ToggleSelectSlot(slot);
        else if (_clickModifiers.HasFlag(KeyModifiers.Shift))
            vm.RangeSelectSlot(slot);
        else
            vm.SelectSlot(slot); // plain click: back to single selection
    }

    private static SlotViewModel? GetMenuSlot(object? sender) => (sender as MenuItem)?.DataContext as SlotViewModel;

    private void OnSlotMenuOpening(object? sender, CancelEventArgs e)
    {
        if (sender is not ContextMenu { DataContext: SlotViewModel slot } menu || ViewModel is not { } vm)
            return;
        // View/Set/Import are single-slot actions; Export/Delete follow the
        // action targets (whole selection when the clicked slot is part of it).
        var multi = vm.IsMultiSelection;
        var targets = vm.GetActionTargets(slot);
        var anyOccupied = targets.Any(static s => !s.IsEmpty);
        foreach (var item in menu.Items)
        {
            if (item is not MenuItem menuItem)
                continue;
            menuItem.IsEnabled = menuItem.Tag switch
            {
                "view" => !multi && !slot.IsEmpty,
                "set" => !multi && vm.CanSetToSlot,
                "import" => !multi,
                "export" => anyOccupied,
                "delete" => anyOccupied,
                _ => menuItem.IsEnabled,
            };
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
        if (GetMenuSlot(sender) is { } slot && ViewModel is { } vm)
            vm.DeleteSlots(vm.GetActionTargets(slot));
    }

    private async void OnSlotImportClicked(object? sender, RoutedEventArgs e)
    {
        if (GetMenuSlot(sender) is { } slot)
            await ImportIntoSlotAsync(slot);
    }

    private async void OnSlotExportClicked(object? sender, RoutedEventArgs e)
    {
        if (GetMenuSlot(sender) is { } slot && ViewModel is { } vm)
            await ExportSlotsAsync(vm.GetActionTargets(slot));
    }

    private async void OnImportSelectedClicked(object? sender, RoutedEventArgs e)
    {
        if (ViewModel?.SelectedSlot is { } slot)
            await ImportIntoSlotAsync(slot);
    }

    private async void OnExportSelectedClicked(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm)
            await ExportSlotsAsync(vm.SelectedSlots);
    }

    private async System.Threading.Tasks.Task ImportIntoSlotAsync(SlotViewModel slot)
    {
        if (ViewModel is not { SAV: { } sav } vm)
            return;
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import Pokémon File",
            AllowMultiple = false,
            FileTypeFilter = PkmFileService.GetPickerFileTypes(sav),
        });
        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (path is not null)
            vm.TryImportFileToSlot(path, slot);
    }

    private async System.Threading.Tasks.Task ExportSlotsAsync(IReadOnlyList<SlotViewModel> slots)
    {
        if (ViewModel is not { } vm)
            return;
        var occupied = slots.Where(static s => !s.IsEmpty).ToList();
        if (occupied.Count == 0)
            return;

        if (occupied.Count == 1)
        {
            await ExportSingleAsync(vm, occupied[0]);
            return;
        }

        // Multiple Pokémon: pick a folder and write one file per occupied slot.
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = $"Export {occupied.Count} Pokémon To Folder",
            AllowMultiple = false,
        });
        var dir = folders.FirstOrDefault()?.TryGetLocalPath();
        if (dir is null)
            return;

        int exported = 0;
        try
        {
            foreach (var slot in occupied)
            {
                var pk = slot.Read();
                PkmFileService.Export(pk, System.IO.Path.Combine(dir, PathUtil.CleanFileName(pk.FileName)));
                exported++;
            }
            vm.StatusMessage = $"Exported {exported} Pokémon to {System.IO.Path.GetFileName(dir)}.";
        }
        catch (System.Exception ex)
        {
            vm.StatusMessage = $"Export failed after {exported} file(s): {ex.Message}";
        }
    }

    private async System.Threading.Tasks.Task ExportSingleAsync(MainWindowViewModel vm, SlotViewModel slot)
    {
        var pk = slot.Read();
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export Pokémon File",
            SuggestedFileName = PathUtil.CleanFileName(pk.FileName),
            ShowOverwritePrompt = true,
        });
        var path = file?.TryGetLocalPath();
        if (path is null)
            return;
        try
        {
            PkmFileService.Export(pk, path);
            vm.StatusMessage = $"Exported {System.IO.Path.GetFileName(path)}.";
        }
        catch (System.Exception ex)
        {
            vm.StatusMessage = $"Export failed: {ex.Message}";
        }
    }

    private void OnDeleteClicked(object? sender, RoutedEventArgs e) => ViewModel?.DeleteSelected();
    private void OnPrevBoxClicked(object? sender, RoutedEventArgs e) => ViewModel?.PrevBox();
    private void OnNextBoxClicked(object? sender, RoutedEventArgs e) => ViewModel?.NextBox();
    private void OnExitClicked(object? sender, RoutedEventArgs e) => Close();
}
