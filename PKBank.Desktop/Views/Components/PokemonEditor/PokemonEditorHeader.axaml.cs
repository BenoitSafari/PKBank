using Avalonia.Controls;
using Avalonia.Interactivity;
using PKBank.Desktop.ViewModels;
using PKBank.Desktop.Views.Components.Common;
using PKBank.Desktop.Views.Components.Legality;

namespace PKBank.Desktop.Views.Components.PokemonEditor;

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
        if (TopLevel.GetTopLevel(this)?.DataContext is MainWindowViewModel main)
            main.StatusMessage = message;
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
