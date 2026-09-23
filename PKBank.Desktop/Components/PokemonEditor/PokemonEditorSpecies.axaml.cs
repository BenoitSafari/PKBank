using Avalonia.Controls;
using Avalonia.Interactivity;
using PKBank.Desktop.Components.PokemonEditor.ViewModels;

namespace PKBank.Desktop.Components.PokemonEditor;

/// <summary>
///     First tab of the editor window: identity and species-level fields, PID through Pokérus.
/// </summary>
public sealed partial class PokemonEditorSpecies : UserControl
{
    public PokemonEditorSpecies() => InitializeComponent();

    private void OnRerollPidClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is PokemonEditorViewModel vm)
            vm.RerollPid();
    }

    private void OnPidLostFocus(object? sender, RoutedEventArgs e)
    {
        if (DataContext is PokemonEditorViewModel vm)
            vm.NormalizePidText(); // snap partial input back to the stored 8-digit value
    }
}
