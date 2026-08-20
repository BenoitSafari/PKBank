using Avalonia.Controls;
using Avalonia.Interactivity;
using PKBank.Desktop.ViewModels;

namespace PKBank.Desktop.Views.PokemonEditor;

public sealed partial class PokemonEditorView : UserControl
{
    public PokemonEditorView() => InitializeComponent();

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
