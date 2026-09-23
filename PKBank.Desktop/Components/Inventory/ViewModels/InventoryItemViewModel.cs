using PKBank.Desktop.Components;
namespace PKBank.Desktop.Components.Inventory.ViewModels;

/// <summary>One slot row of an inventory pouch (item + quantity).</summary>
public sealed class InventoryItemViewModel(int itemId, int count) : ViewModelBase
{
    private int _itemId = itemId;
    private int? _count = count;

    public int ItemId
    {
        get => _itemId;
        set
        {
            if (value < 0 || !SetField(ref _itemId, value))
                return;
            // Giving an item to an empty slot implies at least one copy.
            if (value != 0 && (_count ?? 0) == 0)
                Count = 1;
        }
    }

    public int? Count { get => _count; set => SetField(ref _count, value); }
}
