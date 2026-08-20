using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using PKBank.Desktop.ViewModels;

namespace PKBank.Desktop.Views.PokemonEditor;

public sealed partial class PokemonEditorStats : UserControl
{
    // Modifiers captured on pointer-press so the Click handlers can honor
    // Ctrl (max) / Alt (clear), like the WinForms StatEditor labels.
    private KeyModifiers _pressModifiers;

    public PokemonEditorStats()
    {
        InitializeComponent();
        RandomIVsButton.AddHandler(PointerPressedEvent, CaptureModifiers, RoutingStrategies.Tunnel);
        RandomEVsButton.AddHandler(PointerPressedEvent, CaptureModifiers, RoutingStrategies.Tunnel);
    }

    private void CaptureModifiers(object? sender, PointerPressedEventArgs e) => _pressModifiers = e.KeyModifiers;

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

    private void OnStatInputLostFocus(object? sender, RoutedEventArgs e)
    {
        if (e.Source is TextBox && DataContext is PokemonEditorViewModel vm)
            vm.RefreshStatInputTexts(); // emptied fields snap back to their stored value (0)
    }

    // Clamp the text at the control level, like the WinForms masked boxes: binding
    // notifications are deduplicated and cannot rewrite the field mid-edit.
    private void OnStatTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox { DataContext: StatRowViewModel row } tb)
            return;
        var max = tb.Tag as string == "iv" ? row.MaxIV : row.MaxEV;
        if (int.TryParse(tb.Text, out var value) && value > max)
        {
            tb.Text = max.ToString();
            tb.CaretIndex = tb.Text.Length;
        }
    }
}
