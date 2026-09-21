using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace PKBank.Desktop.Views.Components.Common;

/// <summary>Modal yes/no prompt, used before actions that discard the current state.</summary>
public sealed partial class ConfirmationWindow : Window
{
    private bool _confirmed;

    public ConfirmationWindow() => InitializeComponent(); // designer

    public static async Task<bool> ShowAsync(Window owner, string title, string message, string confirmText)
    {
        var window = new ConfirmationWindow { Title = title };
        window.MessageText.Text = message;
        window.ConfirmButton.Content = confirmText;
        await window.ShowDialog(owner);
        return window._confirmed;
    }

    private void OnConfirmClicked(object? sender, RoutedEventArgs e)
    {
        _confirmed = true;
        Close();
    }

    private void OnCancelClicked(object? sender, RoutedEventArgs e) => Close();
}
