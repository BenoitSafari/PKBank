using Avalonia.Controls;
using Avalonia.Interactivity;
using PKBank.Desktop.Components.Common.ConfirmationWindow;

namespace PKBank.Desktop.Components.Bank;

/// <summary>
///     The bank shown under the save boxes: the bank picker with its create / rename / delete buttons,
///     and the bank box on screen.
/// </summary>
public sealed partial class BankSection : UserControl
{
    public BankSection() => InitializeComponent();

    /// <summary>Where bank slots live, for the window's drag and drop.</summary>
    public Control BoxArea => BoxHost;

    private MainWindowViewModel? ViewModel => DataContext as MainWindowViewModel;
    private Window? Owner => TopLevel.GetTopLevel(this) as Window;

    private async void OnCreateClicked(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not { } vm || Owner is not { } owner)
            return;
        var name = await BankNameWindow.ShowAsync(owner, "Create Bank", "Create", string.Empty,
            text => vm.Bank.ValidateName(text, false));
        if (name is not null)
            vm.Bank.CreateBank(name);
    }

    private async void OnRenameClicked(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not { Bank.SelectedBank: { } bank } vm || Owner is not { } owner)
            return;
        var name = await BankNameWindow.ShowAsync(owner, "Rename Bank", "Rename", bank.Name,
            text => vm.Bank.ValidateName(text, true));
        if (name is not null && name != bank.Name)
            vm.Bank.RenameSelected(name);
    }

    private async void OnDeleteClicked(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not { Bank: { CanDelete: true, SelectedBank: { } bank } } vm || Owner is not { } owner)
            return;
        var message = bank.IsNew
            ? $"Discard bank “{bank.Name}”? It has not been saved yet, so nothing on disk changes."
            : $"Delete bank “{bank.Name}” and all the Pokémon files in it? It is deleted when the save is " +
              "written; after that it cannot be undone.";
        if (await ConfirmationWindow.ShowAsync(owner, "Delete Bank", message, "Delete") == ConfirmationResult.Confirm)
            vm.Bank.DeleteSelected();
    }
}
