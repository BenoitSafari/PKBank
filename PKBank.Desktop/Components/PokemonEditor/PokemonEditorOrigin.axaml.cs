using Avalonia.Controls;
using Avalonia.Interactivity;
using PKBank.Desktop.Components.PokemonEditor.ViewModels;

namespace PKBank.Desktop.Components.PokemonEditor;

public sealed partial class PokemonEditorOrigin : UserControl
{
    public PokemonEditorOrigin() => InitializeComponent();

    private void OnCycleOTGenderClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is PokemonEditorViewModel vm)
            vm.CycleOTGender();
    }
}
