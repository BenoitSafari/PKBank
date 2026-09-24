using System.Threading.Tasks;
using Avalonia.Controls;
using PKBank.Desktop.Components;
using PKBank.Desktop.Components.Box.ViewModels;
using PKBank.Desktop.Components.Common.ConfirmationWindow;
using PKBank.Desktop.Components.PokemonEditor;

namespace PKBank.Desktop.Services.Slots;

/// <summary>
///     Modal slot actions behind Edit/New and Paste, shared by the slot context menu and the
///     selected-slot action bar.
/// </summary>
public static class SlotEditorDialogs
{
    /// <summary>
    ///     Opens the editor on the slot; an empty slot starts from the store's blank entity, which is the
    ///     "New" case. Nothing is written unless the user saves.
    /// </summary>
    public static async Task EditSlotAsync(TopLevel top, MainWindowViewModel vm, SlotViewModel slot)
    {
        if (top is not Window owner || vm.SAV is not { } sav || vm.Sources is not { } sources)
            return;

        vm.SelectSlot(slot);
        var pk = await PokemonEditorWindow.ShowAsync(owner, sav, sources, slot);
        if (pk is not null)
            vm.WriteEntityToSlot(slot, pk);
    }

    /// <summary>
    ///     Pastes the clipboard at the slot: one Pokémon replaces what is there (after confirming), several
    ///     fill the free slots from there on.
    /// </summary>
    public static async Task PasteToSlotAsync(TopLevel top, MainWindowViewModel vm, SlotViewModel slot)
    {
        if (top is not Window owner)
            return;

        // Several Pokémon take the free slots from here on, like a dropped selection: nothing is overwritten.
        if (vm.ClipboardCount > 1)
        {
            if (vm.TryPlanPaste(slot, out var error) is { } plan)
                vm.ApplyPaste(plan);
            else if (error.Length != 0)
                await ConfirmationWindow.ShowMessageAsync(owner, "Paste", error);
            return;
        }

        if (!slot.IsEmpty)
        {
            var result = await ConfirmationWindow.ShowAsync(
                owner, "Paste", "This slot already holds a Pokémon. Overwrite it?", "Overwrite");
            if (result != ConfirmationResult.Confirm)
                return;
        }

        vm.PasteToSlot(slot);
    }
}
