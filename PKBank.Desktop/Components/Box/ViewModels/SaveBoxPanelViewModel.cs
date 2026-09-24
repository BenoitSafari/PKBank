using PKBank.Desktop.Services.Slots;

namespace PKBank.Desktop.Components.Box.ViewModels;

/// <summary>The box of the loaded save on screen; the arrows and the picker switch boxes.</summary>
public sealed class SaveBoxPanelViewModel(SaveBoxStore store, int box) : BoxPanelViewModelBase(store, box);
