using PKBank.Desktop.Services.Banks;

namespace PKBank.Desktop.ViewModels;

/// <summary>
/// One box of a bank. The arrows box through the bank instead of switching boxes, and there is nothing
/// to add or close, so those buttons are gone.
/// </summary>
public sealed class BankBoxPanelViewModel(BankViewModel owner, BankSlotStore store, int box)
    : BoxPanelViewModelBase(store, box)
{
    public override bool ShowAdd => false;
    public override bool ShowClose => false;

    /// <summary>Re-reads the box count, which grows when an entity lands on the spare box.</summary>
    public void RefreshBoxes() => ContainerNames = Store.ContainerNames;

    protected override void OnContainerChanged(int container) => owner.OnBoxChanged(container);
}
