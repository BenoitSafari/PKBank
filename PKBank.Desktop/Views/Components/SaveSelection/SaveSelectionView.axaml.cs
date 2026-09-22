using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using PKBank.Desktop.Services.SaveFiles;
using PKBank.Desktop.ViewModels;
using PKBank.Desktop.Views.Settings;

namespace PKBank.Desktop.Views.Components.SaveSelection;

/// <summary>
///     Startup screen: pick one of the saves found under the configured folders, or
///     open any save file from disk.
/// </summary>
public sealed partial class SaveSelectionView : UserControl
{
    /// <summary>Width below which a row can no longer fit identity and game side by side.</summary>
    private const double NarrowWidth = 480;

    public static readonly StyledProperty<bool> IsNarrowProperty =
        AvaloniaProperty.Register<SaveSelectionView, bool>(nameof(IsNarrow));

    public SaveSelectionView()
    {
        InitializeComponent();
        SizeChanged += (_, e) => IsNarrow = e.NewSize.Width < NarrowWidth;
    }

    /// <summary>Rows stack the game block under the trainer line instead of trimming both.</summary>
    public bool IsNarrow
    {
        get => GetValue(IsNarrowProperty);
        private set => SetValue(IsNarrowProperty, value);
    }

    private MainWindowViewModel? ViewModel => DataContext as MainWindowViewModel;

    private Window? Host => TopLevel.GetTopLevel(this) as Window;

    private void OnLoadClicked(object? sender, RoutedEventArgs e) => LoadSelected();

    private void OnRowDoubleTapped(object? sender, TappedEventArgs e) => LoadSelected();

    private void LoadSelected()
    {
        if (ViewModel is { SaveSelection.SelectedSave: { } entry } vm)
            vm.LoadSaveFromPath(entry.Path);
    }

    private async void OnOpenClicked(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm && TopLevel.GetTopLevel(this) is { } top)
            await SaveFileDialogs.OpenAsync(top, vm);
    }

    private async void OnEditPathsClicked(object? sender, RoutedEventArgs e)
    {
        // Adding a folder commits to the config, which triggers a rescan.
        if (ViewModel is { } vm && Host is { } host)
            await new SettingsWindow(vm).ShowDialog(host);
    }
}
