using Avalonia.Controls;
using Avalonia.Interactivity;
using PKBank.Desktop.Components;
using PKBank.Desktop.Components.PokemonEditor.ViewModels;
using PKBank.Desktop.Components.Common.QRCodeWindow;
using PKBank.Desktop.Components.Legality;

namespace PKBank.Desktop.Components.PokemonEditor;

public sealed partial class PokemonEditorHeader : UserControl
{
    public PokemonEditorHeader() => InitializeComponent();

    private async void OnQrClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not PokemonEditorViewModel vm || !vm.HasSpecies)
            return;
        if (TopLevel.GetTopLevel(this) is not Window owner)
            return;
        await new QRCodeWindow(vm.GetEntityClone()).ShowDialog(owner);
    }

    private void OnFixLegalityClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not PokemonEditorViewModel vm)
            return;
        vm.TryFixPidIvs(out var message);
        vm.Message = message; // the editor window shows it: there is no status bar behind a modal
    }

    private async void OnLegalityClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not PokemonEditorViewModel vm || !vm.HasSpecies)
            return;
        if (TopLevel.GetTopLevel(this) is not Window owner)
            return;
        await new LegalityReportWindow(vm.GetEntityClone()).ShowDialog(owner);
    }
}
