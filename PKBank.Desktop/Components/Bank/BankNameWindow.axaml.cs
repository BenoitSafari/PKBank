using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace PKBank.Desktop.Components.Bank;

/// <summary>Asks for a bank name, for a new bank or a rename. Confirming is only possible with a valid name.</summary>
public sealed partial class BankNameWindow : Window
{
    private string? _result;
    private Func<string, string?> _validate = static _ => null;

    public BankNameWindow() => InitializeComponent(); // designer

    /// <param name="validate">Returns why a name cannot be used, or null when it can.</param>
    /// <returns>The chosen name, or null when cancelled.</returns>
    public static async Task<string?> ShowAsync(Window owner, string title, string confirmText, string initial,
        Func<string, string?> validate)
    {
        var window = new BankNameWindow
        {
            Title = title,
            ConfirmButton = { Content = confirmText },
            _validate = validate
        };
        window.NameBox.Text = initial;
        window.Opened += (_, _) =>
        {
            window.NameBox.Focus();
            window.NameBox.SelectAll();
        };
        await window.ShowDialog(owner);
        return window._result;
    }

    private void OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        var text = NameBox.Text ?? string.Empty;
        var error = _validate(text);
        ConfirmButton.IsEnabled = error is null;
        // An empty box is not worth an error line: the disabled button says enough.
        ErrorText.Text = error;
        ErrorText.IsVisible = error is not null && text.Length > 0;
    }

    private void OnNameKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter when ConfirmButton.IsEnabled:
                Confirm();
                e.Handled = true;
                break;
            case Key.Escape:
                Close();
                e.Handled = true;
                break;
        }
    }

    private void OnConfirmClicked(object? sender, RoutedEventArgs e) => Confirm();

    private void OnCancelClicked(object? sender, RoutedEventArgs e) => Close();

    private void Confirm()
    {
        _result = NameBox.Text ?? string.Empty;
        Close();
    }
}
