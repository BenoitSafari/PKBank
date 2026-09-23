using System.ComponentModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using PKBank.Desktop.Services.Slots;
using PKBank.Desktop.Utils;
using PKBank.Desktop.Components;
using PKBank.Desktop.Components.Box.ViewModels;

namespace PKBank.Desktop.Components.Box;

/// <summary>
///     One storage slot: the sprite, its selection state, and the per-slot actions. Works in any window
///     whose DataContext is the <see cref="MainWindowViewModel" />.
/// </summary>
public sealed partial class SlotView : UserControl
{
    public SlotView() => InitializeComponent();

    private SlotViewModel? Slot => DataContext as SlotViewModel;

    // Resolve from this control, never from the sender: context menu items live in a popup root of
    // their own and would not find the window.
    private TopLevel? Host => TopLevel.GetTopLevel(this);

    private MainWindowViewModel? ViewModel => Host?.DataContext as MainWindowViewModel;

    private void OnClicked(object? sender, RoutedEventArgs e)
    {
        if (Slot is not { } slot || ViewModel is not { } vm)
            return;
        // Modifiers are captured on press; the click only fires after release.
        var modifiers = SlotDragDropHost.LastClickModifiers;
        if (modifiers.HasFlag(KeyModifiers.Control))
            vm.ToggleSelectSlot(slot);
        else if (modifiers.HasFlag(KeyModifiers.Shift))
            vm.RangeSelectSlot(slot);
        else
            vm.SelectSlot(slot); // plain click: back to single selection
    }

    private void OnMenuOpening(object? sender, CancelEventArgs e)
    {
        if (sender is not ContextMenu menu || Slot is not { } slot || ViewModel is not { } vm)
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
                "view" => !multi && !slot.IsEmpty && slot.IsCompatible,
                "set" => !multi && slot.IsCompatible && vm.CanSetToSlot,
                "import" => true,
                "export" => anyOccupied,
                "tobank" => !multi && vm.CanSendToBank(slot),
                "tosave" => !multi && vm.CanSendToSave(slot),
                "delete" => anyOccupied,
                _ => menuItem.IsEnabled
            };
        }
    }

    private void OnViewClicked(object? sender, RoutedEventArgs e)
    {
        if (Slot is { } slot)
            ViewModel?.ViewSlot(slot);
    }

    private void OnSetClicked(object? sender, RoutedEventArgs e)
    {
        if (Slot is { } slot)
            ViewModel?.SetSlotFromEditor(slot);
    }

    private void OnToBankClicked(object? sender, RoutedEventArgs e)
    {
        if (Slot is { } slot)
            ViewModel?.SendToBank(slot);
    }

    private void OnToSaveClicked(object? sender, RoutedEventArgs e)
    {
        if (Slot is { } slot)
            ViewModel?.SendToSave(slot);
    }

    private void OnDeleteClicked(object? sender, RoutedEventArgs e)
    {
        if (Slot is { } slot && ViewModel is { } vm)
            vm.DeleteSlots(vm.GetActionTargets(slot));
    }

    private async void OnImportClicked(object? sender, RoutedEventArgs e)
    {
        if (Slot is not { } slot || ViewModel is not { } vm || Host is not { } host)
            return;
        // Whole selection when the clicked slot is part of it, else just that slot.
        var targets = vm.GetActionTargets(slot).Count > 1 ? vm.GetSelectedSlotsInDisplayOrder() : [slot];
        await SlotFileDialogs.ImportIntoSlotsAsync(host, vm, targets);
    }

    private async void OnExportClicked(object? sender, RoutedEventArgs e)
    {
        if (Slot is { } slot && ViewModel is { } vm && Host is { } host)
            await SlotFileDialogs.ExportSlotsAsync(host, vm, vm.GetActionTargets(slot));
    }
}
