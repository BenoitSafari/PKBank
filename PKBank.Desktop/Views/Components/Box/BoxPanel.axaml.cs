using Avalonia.Controls;
using Avalonia.Interactivity;
using PKBank.Desktop.ViewModels;

namespace PKBank.Desktop.Views.Components.Box;

/// <summary>
///     One container of slots with its navigation bar: a box of the loaded save, or a box of a bank.
///     Everything configurable lives on <see cref="BoxPanelViewModelBase" />.
/// </summary>
public sealed partial class BoxPanel : UserControl
{
    public BoxPanel() => InitializeComponent();

    private BoxPanelViewModelBase? ViewModel => DataContext as BoxPanelViewModelBase;

    private void OnPrevClicked(object? sender, RoutedEventArgs e) => ViewModel?.Prev();
    private void OnNextClicked(object? sender, RoutedEventArgs e) => ViewModel?.Next();
    private void OnAddClicked(object? sender, RoutedEventArgs e) => ViewModel?.Add();
    private void OnCloseClicked(object? sender, RoutedEventArgs e) => ViewModel?.Close();
}
