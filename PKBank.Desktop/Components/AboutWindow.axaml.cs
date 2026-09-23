using Avalonia.Controls;
using Avalonia.Interactivity;

namespace PKBank.Desktop.Components;

public sealed partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        Title = $"About {AppInfo.Name}";
        NameText.Text = AppInfo.Name;
        VersionText.Text = $"Version {AppInfo.Version}";
    }

    private void OnCloseClicked(object? sender, RoutedEventArgs e) => Close();
}
