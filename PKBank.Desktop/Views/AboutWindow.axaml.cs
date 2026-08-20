using Avalonia.Controls;
using Avalonia.Interactivity;

namespace PKBank.Desktop.Views;

public sealed partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        var version = typeof(AboutWindow).Assembly.GetName().Version;
        VersionText.Text = $"Version {version?.ToString(3) ?? "?"}";
    }

    private void OnCloseClicked(object? sender, RoutedEventArgs e) => Close();
}
