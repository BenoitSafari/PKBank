using System;
using System.Collections.ObjectModel;
using PKHeX.Core;

namespace PKBank.Desktop.ViewModels;

/// <summary>
/// One open box view; several can be shown side by side, each with its own
/// navigation. Two panels may display the same box (writes are mirrored by
/// <see cref="MainWindowViewModel"/>).
/// </summary>
public sealed class BoxPanelViewModel : ViewModelBase
{
    private readonly SaveFile _sav;
    private int _boxIndex;

    public ObservableCollection<SlotViewModel> Slots { get; } = [];

    public BoxPanelViewModel(SaveFile sav, int box)
    {
        _sav = sav;
        _boxIndex = Math.Clamp(box, 0, sav.BoxCount - 1);
        for (var i = 0; i < sav.BoxSlotCount; i++)
            Slots.Add(new SlotViewModel(sav, isParty: false, _boxIndex, i));
    }

    public int BoxIndex
    {
        get => _boxIndex;
        set
        {
            var box = Math.Clamp(value, 0, _sav.BoxCount - 1);
            if (SetField(ref _boxIndex, box))
            {
                foreach (var slot in Slots)
                    slot.ChangeBox(box);
            }
            else if (value != box)
            {
                // The view pushed an out-of-range value (e.g. -1 while the ComboBox
                // ItemsSource resets); notify so it re-reads the clamped value.
                OnPropertyChanged();
            }
        }
    }

    public void NextBox() => BoxIndex = _boxIndex >= _sav.BoxCount - 1 ? 0 : _boxIndex + 1;
    public void PrevBox() => BoxIndex = _boxIndex <= 0 ? _sav.BoxCount - 1 : _boxIndex - 1;

    public void RefreshSlots()
    {
        foreach (var slot in Slots)
            slot.Refresh();
    }
}
