using Avalonia.Controls;
using Avalonia.Interactivity;
using PKBank.Desktop.ViewModels;

namespace PKBank.Desktop.Views.Components.PokemonEditor;

public sealed partial class PokemonEditorOrigin : UserControl
{
    public PokemonEditorOrigin() => InitializeComponent();

    private void OnCycleOTGenderClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is PokemonEditorViewModel vm)
            vm.CycleOTGender();
    }
}
