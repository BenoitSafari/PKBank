using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using PKBank.Desktop.Services.Slots;
using PKBank.Desktop.Components;

namespace PKBank.Desktop.Components.Box.ViewModels;

/// <summary>
/// One container of a <see cref="ISlotStore"/> shown as a grid: a box of the loaded save, or a box of a
/// bank. Everything the view needs is on the view-model, so the same control serves both sections.
/// </summary>
public abstract class BoxPanelViewModelBase : ViewModelBase
{
    /// <summary>Slot button footprint: the 68x56 sprite plus padding, border and margin.</summary>
    private const double SlotCellWidth = 76;

    private int _containerIndex;
    private IReadOnlyList<string> _containerNames;

    protected BoxPanelViewModelBase(ISlotStore store, int container)
    {
        Store = store;
        _containerNames = store.ContainerNames;
        _containerIndex = Math.Clamp(container, 0, Math.Max(0, store.ContainerCount - 1));
        for (var i = 0; i < store.SlotsPerContainer; i++)
            Slots.Add(new SlotViewModel(store, _containerIndex, i));
    }

    public ISlotStore Store { get; }
    public ObservableCollection<SlotViewModel> Slots { get; } = [];

    public int Columns => Store.Columns;

    /// <summary>Exact width of the slot area, so the wrapping panel breaks after <see cref="Columns" />.</summary>
    public double SlotAreaWidth => Columns * SlotCellWidth;

    public IReadOnlyList<string> ContainerNames
    {
        get => _containerNames;
        protected set => SetField(ref _containerNames, value);
    }

    public int ContainerIndex
    {
        get => _containerIndex;
        set
        {
            var clamped = Math.Clamp(value, 0, Math.Max(0, Store.ContainerCount - 1));
            if (SetField(ref _containerIndex, clamped))
            {
                foreach (var slot in Slots)
                    slot.ChangeContainer(clamped);
                OnContainerChanged(clamped);
            }
            else if (value != clamped)
            {
                // The view pushed an out-of-range value (e.g. -1 while the ComboBox
                // ItemsSource resets); notify so it re-reads the clamped value.
                OnPropertyChanged();
            }
        }
    }

    public virtual void Next() =>
        ContainerIndex = _containerIndex >= Store.ContainerCount - 1 ? 0 : _containerIndex + 1;

    public virtual void Prev() =>
        ContainerIndex = _containerIndex <= 0 ? Store.ContainerCount - 1 : _containerIndex - 1;

    protected virtual void OnContainerChanged(int container)
    {
    }

    public void RefreshSlots()
    {
        foreach (var slot in Slots)
            slot.Refresh();
    }
}
