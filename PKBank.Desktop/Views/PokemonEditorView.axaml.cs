using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using PKBank.Desktop.ViewModels;

namespace PKBank.Desktop.Views;

public sealed partial class PokemonEditorView : UserControl
{
    // Modifiers captured on pointer-press so the Click handlers can honor
    // Ctrl (max) / Alt (clear), like the WinForms StatEditor labels.
    private KeyModifiers _pressModifiers;

    public PokemonEditorView()
    {
        InitializeComponent();
        RandomIVsButton.AddHandler(PointerPressedEvent, CaptureModifiers, RoutingStrategies.Tunnel);
        RandomEVsButton.AddHandler(PointerPressedEvent, CaptureModifiers, RoutingStrategies.Tunnel);
    }

    private void CaptureModifiers(object? sender, PointerPressedEventArgs e) => _pressModifiers = e.KeyModifiers;

    private void OnStatInputLostFocus(object? sender, RoutedEventArgs e)
    {
        if (e.Source is TextBox && DataContext is PokemonEditorViewModel vm)
            vm.RefreshStatInputTexts(); // emptied fields snap back to their stored value (0)
    }

    private void OnRandomIVsClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is PokemonEditorViewModel vm)
            vm.RandomizeIVs(_pressModifiers.HasFlag(KeyModifiers.Control), _pressModifiers.HasFlag(KeyModifiers.Alt));
    }

    private void OnRandomEVsClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is PokemonEditorViewModel vm)
            vm.RandomizeEVs(_pressModifiers.HasFlag(KeyModifiers.Control), _pressModifiers.HasFlag(KeyModifiers.Alt));
    }

    private async void OnQrClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not PokemonEditorViewModel vm || !vm.HasSpecies)
            return;
        if (TopLevel.GetTopLevel(this) is not Window owner)
            return;
        await new QRCodeWindow(vm.GetEntityClone()).ShowDialog(owner);
    }
}
