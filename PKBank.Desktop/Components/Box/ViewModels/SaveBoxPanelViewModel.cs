using PKBank.Desktop.Services.Slots;
using PKBank.Desktop.Components;

namespace PKBank.Desktop.Components.Box.ViewModels;

/// <summary>
/// One open box of the loaded save; several can be shown side by side, each with its own navigation. Two
/// panels may display the same box (writes are mirrored by <see cref="MainWindowViewModel"/>).
/// </summary>
public sealed class SaveBoxPanelViewModel(MainWindowViewModel owner, SaveBoxStore store, int box)
    : BoxPanelViewModelBase(store, box)
{
    public override void Add() => owner.AddBoxPanel();

    public override void Close() => owner.CloseBoxPanel(this);
}
