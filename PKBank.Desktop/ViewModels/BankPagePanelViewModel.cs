using PKBank.Desktop.Services.Banks;

namespace PKBank.Desktop.ViewModels;

/// <summary>
/// One page of a bank. The arrows page through the bank instead of switching boxes, and there is nothing
/// to add or close, so those buttons are gone.
/// </summary>
public sealed class BankPagePanelViewModel(BankViewModel owner, BankSlotStore store, int page)
    : BoxPanelViewModelBase(store, page)
{
    public override bool ShowAdd => false;
    public override bool ShowClose => false;

    /// <summary>Re-reads the page count, which grows when an entity lands on the spare page.</summary>
    public void RefreshPages() => ContainerNames = Store.ContainerNames;

    protected override void OnContainerChanged(int container) => owner.OnPageChanged(container);
}
