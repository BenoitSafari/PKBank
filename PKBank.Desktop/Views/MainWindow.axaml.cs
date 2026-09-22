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
using PKBank.Desktop.Utils;
using PKBank.Desktop.ViewModels;
using PKBank.Desktop.Views.Components.Common.ConfirmationWindow;
using PKHeX.Core;

namespace PKBank.Desktop.Views;

public sealed partial class MainWindow : Window
{
    private const double DragThreshold = 6;
    private KeyModifiers _clickModifiers;
    private bool _dragInProgress;
    private PointerPressedEventArgs? _pressArgs;
    private Point _pressPoint;

    private SlotViewModel? _pressedSlot;

    public MainWindow()
    {
        InitializeComponent();
        InitializeSlotDragDrop();
    }

    private MainWindowViewModel? ViewModel => DataContext as MainWindowViewModel;

    // ----- Slot drag & drop -------------------------------------------------
    // Dragging a slot moves/swaps it onto another slot; the data object also
    // carries a temp .pk* file so dropping onto a file manager exports it.

    private void InitializeSlotDragDrop()
    {
        foreach (var area in new Control[] { BoxPanelsItems, PartyItems })
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
                    if (!member.IsEmpty)
                        await AttachExportFile(transfer, member);
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
    private async Task AttachExportFile(DataTransfer transfer, SlotViewModel slot)
    {
        try
        {
            var pk = slot.Read();
            // Write on the same filesystem as the user's home so file managers can
            // Move the export directly (/tmp is usually a different device, tmpfs).
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PKBank.Desktop", "export");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, Path.GetFileName(FileUtil.GetPKMTempFileName(pk, false)));
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

    private void OnWindowDragLeave(object? sender, DragEventArgs e) =>
        // The cursor left the window (e.g. dragging out to a file manager).
        DragGhostLayer.IsVisible = false;

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

        var paths = e.DataTransfer.TryGetFiles()?.Select(f => f.TryGetLocalPath()).OfType<string>().ToArray() ?? [];
        if (paths.Length == 0)
            return;

        // Several files dropped: import them into the selected slots in display order.
        if (paths.Length > 1)
        {
            e.Handled = true;
            ViewModel?.ImportFilesToSelection(paths);
            return;
        }

        var path = paths[0];

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

        if (vm.SAV is { State.Edited: true })
        {
            var message =
                $"The currently loaded save has unsaved changes.\n\nLoading “{Path.GetFileName(path)}” will discard them. Continue?";
            if (!await ConfirmationWindow.ShowAsync(this, "Load Save File", message, "Load"))
                return;
        }

        vm.LoadSaveFromPath(dropped, path);
    }

    private SlotViewModel? HitTestSlot(Point position)
        => this.InputHitTest(position) is Control control ? FindSlot(control) : null;

    // ----- Box panels -------------------------------------------------------

    private static BoxPanelViewModel? FindPanel(object? sender)
        => (sender as Control)?.DataContext as BoxPanelViewModel;

    private void OnPanelPrevBoxClicked(object? sender, RoutedEventArgs e) => FindPanel(sender)?.PrevBox();
    private void OnPanelNextBoxClicked(object? sender, RoutedEventArgs e) => FindPanel(sender)?.NextBox();
    private void OnAddBoxClicked(object? sender, RoutedEventArgs e) => ViewModel?.AddBoxPanel();

    private void OnCloseBoxClicked(object? sender, RoutedEventArgs e)
    {
        if (FindPanel(sender) is { } panel)
            ViewModel?.CloseBoxPanel(panel);
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
        // View/Set are single-slot actions; Import/Export/Delete follow the
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
                "import" => true,
                "export" => anyOccupied,
                "delete" => anyOccupied,
                _ => menuItem.IsEnabled
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
        if (GetMenuSlot(sender) is not { } slot || ViewModel is not { } vm)
            return;
        // Whole selection when the clicked slot is part of it, else just that slot.
        var targets = vm.GetActionTargets(slot).Count > 1 ? vm.GetSelectedSlotsInDisplayOrder() : [slot];
        await ImportIntoSlotsAsync(targets);
    }

    private async void OnSlotExportClicked(object? sender, RoutedEventArgs e)
    {
        if (GetMenuSlot(sender) is { } slot && ViewModel is { } vm)
            await ExportSlotsAsync(vm.GetActionTargets(slot));
    }

    private async void OnImportSelectedClicked(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is { SelectedSlot: not null } vm)
            await ImportIntoSlotsAsync(vm.GetSelectedSlotsInDisplayOrder());
    }

    private async void OnExportSelectedClicked(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm)
            await ExportSlotsAsync(vm.SelectedSlots);
    }

    private async Task ImportIntoSlotsAsync(IReadOnlyList<SlotViewModel> targets)
    {
        if (ViewModel is not { SAV: { } sav } vm || targets.Count == 0)
            return;
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title =
                targets.Count > 1 ? $"Import Pokémon Files ({targets.Count} slots selected)" : "Import Pokémon File",
            AllowMultiple = targets.Count > 1,
            FileTypeFilter = PkmFileService.GetPickerFileTypes(sav)
        });
        var paths = files.Select(f => f.TryGetLocalPath()).OfType<string>().ToArray();
        if (paths.Length == 0)
            return;
        if (targets.Count == 1)
            vm.TryImportFileToSlot(paths[0], targets[0]);
        else
            vm.ImportFilesToSlots(paths, targets);
    }

    private async Task ExportSlotsAsync(IReadOnlyList<SlotViewModel> slots)
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
            AllowMultiple = false
        });
        var dir = folders.FirstOrDefault()?.TryGetLocalPath();
        if (dir is null)
            return;

        var exported = 0;
        try
        {
            foreach (var slot in occupied)
            {
                var pk = slot.Read();
                PkmFileService.Export(pk, Path.Combine(dir, PathUtil.CleanFileName(pk.FileName)));
                exported++;
            }

            vm.StatusMessage = $"Exported {exported} Pokémon to {Path.GetFileName(dir)}.";
        }
        catch (Exception ex)
        {
            vm.StatusMessage = $"Export failed after {exported} file(s): {ex.Message}";
        }
    }

    private async Task ExportSingleAsync(MainWindowViewModel vm, SlotViewModel slot)
    {
        var pk = slot.Read();
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export Pokémon File",
            SuggestedFileName = PathUtil.CleanFileName(pk.FileName),
            ShowOverwritePrompt = true
        });
        var path = file?.TryGetLocalPath();
        if (path is null)
            return;
        try
        {
            PkmFileService.Export(pk, path);
            vm.StatusMessage = $"Exported {Path.GetFileName(path)}.";
        }
        catch (Exception ex)
        {
            vm.StatusMessage = $"Export failed: {ex.Message}";
        }
    }

    private void OnDeleteClicked(object? sender, RoutedEventArgs e) => ViewModel?.DeleteSelected();
}
