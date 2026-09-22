using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace PKBank.Desktop.Views.Components.Common.ConfirmationWindow;

/// <summary>How the user dismissed a <see cref="ConfirmationWindow" />.</summary>
public enum ConfirmationResult
{
    Cancel,
    Confirm,

    /// <summary>Write the pending changes out first, then go ahead.</summary>
    Save
}

public sealed partial class ConfirmationWindow : Window
{
    private ConfirmationResult _result = ConfirmationResult.Cancel;

    public ConfirmationWindow() => InitializeComponent(); // designer

    public static async Task<ConfirmationResult> ShowAsync(
        Window owner, string title, string message, string confirmText, bool allowSave = false)
    {
        var window = new ConfirmationWindow
        {
            Title = title,
            MessageText = { Text = message },
            ConfirmButton = { Content = confirmText },
            SaveButton = { IsVisible = allowSave }
        };
        await window.ShowDialog(owner);
        return window._result;
    }

    /// <summary>Plain notification: only the dismiss button is shown.</summary>
    public static async Task ShowMessageAsync(Window owner, string title, string message)
    {
        var window = new ConfirmationWindow
        {
            Title = title,
            MessageText = { Text = message },
            CancelButton = { IsVisible = false }
        };
        await window.ShowDialog(owner);
    }

    private void OnConfirmClicked(object? sender, RoutedEventArgs e) => Dismiss(ConfirmationResult.Confirm);

    private void OnSaveClicked(object? sender, RoutedEventArgs e) => Dismiss(ConfirmationResult.Save);

    private void OnCancelClicked(object? sender, RoutedEventArgs e) => Dismiss(ConfirmationResult.Cancel);

    private void Dismiss(ConfirmationResult result)
    {
        _result = result;
        Close();
    }
}
