using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using PKBank.Desktop.ViewModels;
using PKBank.Desktop.Views.Components.Common.ConfirmationWindow;
using PKHeX.Core;

namespace PKBank.Desktop.Utils;

/// <summary>
///     Wires slot dragging for one window: pointer handlers on the slot areas, drop handling at the window
///     level, and the ghost sprite following the cursor. Dragging a slot moves/swaps it onto another slot;
///     the data object also carries a temp .pk* file so dropping onto a file manager exports it.
/// </summary>
public sealed class SlotDragDropHost(Window window, Canvas ghostLayer, Image ghostImage)
{
    private const double DragThreshold = 6;

    // A drag started here, kept process-wide so the other window can pick it up: only one slot drag can
    // be in flight at a time, and it is only ever set while we own it.
    private static SlotViewModel? _activeSource;
    private static IReadOnlyList<SlotViewModel>? _activeSources;
    private static Bitmap? _activeSprite;

    private bool _dragInProgress;
    private PointerPressedEventArgs? _pressArgs;
    private Point _pressPoint;
    private SlotViewModel? _pressedSlot;

    /// <summary>Modifiers captured on press; the Click event only fires after release.</summary>
    public static KeyModifiers LastClickModifiers { get; private set; }

    /// <summary>
    ///     Called when the drop carries files rather than a slot. Returns whether it was handled; windows
    ///     that have nothing to do with dropped files can leave it unset.
    /// </summary>
    public Func<IReadOnlyList<string>, Point, Task<bool>>? FileDropHandler { get; set; }

    private MainWindowViewModel? ViewModel => window.DataContext as MainWindowViewModel;

    /// <summary>Starts drags from the slots inside this control.</summary>
    public void AttachArea(Control area)
    {
        area.AddHandler(InputElement.PointerPressedEvent, OnAreaPointerPressed, RoutingStrategies.Tunnel);
        area.AddHandler(InputElement.PointerMovedEvent, OnAreaPointerMoved, RoutingStrategies.Tunnel);
        area.AddHandler(InputElement.PointerReleasedEvent, OnAreaPointerReleased, RoutingStrategies.Tunnel);
    }

    /// <summary>
    ///     Accepts the drag anywhere in the window so the cursor never turns "forbidden" mid-flight; the
    ///     drop itself resolves a slot by hit-test.
    /// </summary>
    public void AttachWindow()
    {
        DragDrop.SetAllowDrop(window, true);
        window.AddHandler(DragDrop.DragOverEvent, OnDragOver);
        window.AddHandler(DragDrop.DropEvent, OnDrop);
        window.AddHandler(DragDrop.DragEnterEvent, OnDragEnter);
        window.AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
    }

    public static SlotViewModel? FindSlot(object? source)
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

    public SlotViewModel? HitTestSlot(Point position)
        => window.InputHitTest(position) is Control control ? FindSlot(control) : null;

    // ----- Drag source ------------------------------------------------------

    private void OnAreaPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        LastClickModifiers = e.KeyModifiers;
        if (!e.GetCurrentPoint(window).Properties.IsLeftButtonPressed)
            return;
        var slot = FindSlot(e.Source);
        _pressedSlot = slot is { IsEmpty: false } ? slot : null;
        _pressArgs = _pressedSlot is null ? null : e;
        _pressPoint = e.GetPosition(window);
    }

    private void OnAreaPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _pressedSlot = null;
        _pressArgs = null;
    }

    private async void OnAreaPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_pressedSlot is not { } slot || _pressArgs is not { } pressArgs || _dragInProgress)
            return;
        if (!e.GetCurrentPoint(window).Properties.IsLeftButtonPressed)
        {
            _pressedSlot = null;
            _pressArgs = null;
            return;
        }

        var delta = e.GetPosition(window) - _pressPoint;
        if (Math.Abs(delta.X) < DragThreshold && Math.Abs(delta.Y) < DragThreshold)
            return;

        _dragInProgress = true;
        _activeSprite = slot.Sprite;
        try
        {
            ShowGhost(slot.Sprite);
            var transfer = new DataTransfer();
            var vm = ViewModel;
            if (vm is { IsMultiSelection: true } && vm.SelectedSlots.Contains(slot))
            {
                // Dragging the multi-selection: dropped inside, it fills the free slots from the
                // target on; dropped outside, it exports one file per occupied selected slot.
                var batch = vm.GetSelectedSlotsInDisplayOrder();
                _activeSources = batch;
                transfer.Add(DataTransferItem.Create(SlotDragFormats.Multi, batch));
                foreach (var member in batch)
                    if (!member.IsEmpty)
                        await AttachExportFile(transfer, member);
            }
            else
            {
                _activeSource = slot;
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
            HideGhost();
            _dragInProgress = false;
            _activeSource = null;
            _activeSources = null;
            _activeSprite = null;
            _pressedSlot = null;
            _pressArgs = null;
        }
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
                AppInfo.Name, "export");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, Path.GetFileName(FileUtil.GetPKMTempFileName(pk, false)));
            PkmFileService.Export(pk, path);

            var file = await window.StorageProvider.TryGetFileFromPathAsync(path);
            if (file is not null)
                transfer.Add(DataTransferItem.CreateFile(file));
        }
        catch
        {
            // External export is best-effort; slot-to-slot dragging still works.
        }
    }

    // ----- Ghost ------------------------------------------------------------

    private void ShowGhost(Bitmap? sprite)
    {
        ghostImage.Source = sprite;
        ghostLayer.IsVisible = true;
        UpdateGhost(_pressPoint);
    }

    private void UpdateGhost(Point position)
    {
        Canvas.SetLeft(ghostImage, position.X + 10);
        Canvas.SetTop(ghostImage, position.Y + 6);
    }

    private void HideGhost()
    {
        ghostLayer.IsVisible = false;
        ghostImage.Source = null;
    }

    // ----- Drop target ------------------------------------------------------

    private void OnDragEnter(object? sender, DragEventArgs e)
    {
        // The drag may have started in another window of ours; carry its ghost over.
        if (_activeSprite is { } sprite)
            ShowGhost(sprite);
    }

    private void OnDragLeave(object? sender, DragEventArgs e) =>
        // The cursor left the window (e.g. dragging out to a file manager).
        ghostLayer.IsVisible = false;

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        if (e.DataTransfer.Contains(SlotDragFormats.Slot) || e.DataTransfer.Contains(SlotDragFormats.Multi))
        {
            e.DragEffects = DragDropEffects.Move;
            if (_activeSprite is not null)
                UpdateGhost(e.GetPosition(ghostLayer));
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

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        // The drag may have been started by another window, which only hides its own ghost; this one
        // put a ghost up on DragEnter and has to take it down itself, whatever the drop turns out to be.
        HideGhost();

        // The in-process payload may not survive the round trip between windows; what we recorded when
        // the drag started holds the same objects either way.
        if (e.DataTransfer.Contains(SlotDragFormats.Multi))
        {
            var batch = e.DataTransfer.TryGetValue(SlotDragFormats.Multi) ?? _activeSources;
            if (batch is null || HitTestSlot(e.GetPosition(window)) is not { } into)
                return;
            e.Handled = true;
            await DropBatchAsync(batch, into);
            return;
        }

        if (e.DataTransfer.Contains(SlotDragFormats.Slot))
        {
            var source = e.DataTransfer.TryGetValue(SlotDragFormats.Slot) ?? _activeSource;
            if (source is null || HitTestSlot(e.GetPosition(window)) is not { } target)
                return;
            ViewModel?.MoveOrSwapSlots(source, target);
            e.Handled = true;
            return;
        }

        var paths = e.DataTransfer.TryGetFiles()?.Select(f => f.TryGetLocalPath()).OfType<string>().ToArray() ?? [];
        if (paths.Length == 0 || FileDropHandler is not { } handler)
            return;
        if (await handler(paths, e.GetPosition(window)))
            e.Handled = true;
    }

    /// <summary>
    ///     A batch lands on the free slots from the drop target onwards, skipping occupied ones and
    ///     wrapping to the first container. Nothing is written unless the whole batch fits.
    /// </summary>
    private async Task DropBatchAsync(IReadOnlyList<SlotViewModel> batch, SlotViewModel into)
    {
        if (ViewModel is not { } vm)
            return;
        if (vm.TryPlanMultiMove(batch, into, out var refused) is { } plan)
        {
            vm.ApplyMultiMove(plan);
            return;
        }

        if (refused.Length != 0)
            await ConfirmationWindow.ShowMessageAsync(window, "Move Pokémon", refused);
    }
}
