using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using PKBank.Desktop.Components.Box.ViewModels;
using PKBank.Desktop.Components.PokemonEditor.ViewModels;
using PKHeX.Core;

namespace PKBank.Desktop.Components.PokemonEditor;

/// <summary>
///     Edits one slot's Pokémon. Nothing is written until Save, which returns the entity to the caller;
///     cancelling — including closing the window — leaves the slot untouched.
/// </summary>
public sealed partial class PokemonEditorWindow : Window
{
    private PKM? _result;

    public PokemonEditorWindow() => InitializeComponent(); // designer

    private PokemonEditorWindow(PokemonEditorViewModel editor) : this() => DataContext = editor;

    /// <summary>Null when cancelled; otherwise the entity to write into <paramref name="slot" />.</summary>
    public static async Task<PKM?> ShowAsync(
        Window owner, SaveFile sav, FilteredGameDataSource sources, SlotViewModel slot)
    {
        // An empty slot reads back as the store's blank entity, which is exactly the "New" case.
        var window = new PokemonEditorWindow(new PokemonEditorViewModel(sav, sources, slot));
        await window.ShowDialog(owner);
        return window._result;
    }

    private void OnCancelClicked(object? sender, RoutedEventArgs e) => Close();

    private void OnSaveClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not PokemonEditorViewModel { HasSpecies: true } vm)
            return;
        _result = vm.GetEntityClone();
        Close();
    }
}
