using PKBank.Desktop.Services.Banks;
using PKBank.Desktop.Components.Box.ViewModels;

namespace PKBank.Desktop.Components.Bank.ViewModels;

/// <summary>One box of a bank; the box count grows as entities land on the spare trailing box.</summary>
public sealed class BankBoxPanelViewModel(BankViewModel owner, BankSlotStore store, int box)
    : BoxPanelViewModelBase(store, box)
{
    protected override void OnContainerChanged(int container) => owner.OnBoxChanged(container);
}
