using System.Collections.Generic;
using System.Linq;
using PKHeX.Core;

namespace PKBank.Desktop.ViewModels;

/// <summary>One pouch tab of the inventory editor.</summary>
public sealed class InventoryPouchViewModel
{
    public InventoryPouchViewModel(InventoryPouch pouch, string[] itemNames)
    {
        Pouch = pouch;
        Name = GetPouchName(pouch.Type);

        var legal = pouch.GetAllItems();
        var choices = new List<ComboItem>(legal.Length + 1) { new("(None)", 0) };
        foreach (var id in legal)
        {
            var name = id < itemNames.Length ? itemNames[id] : $"(Item #{id:000})";
            choices.Add(new ComboItem(name, id));
        }
        choices.Sort(1, choices.Count - 1, ItemNameComparer.Instance);
        ItemChoices = choices;

        Rows = [.. pouch.Items.Select(static item => new InventoryItemViewModel(item.Index, item.Count))];
    }

    public InventoryPouch Pouch { get; }
    public string Name { get; }
    public IReadOnlyList<ComboItem> ItemChoices { get; }
    public IReadOnlyList<InventoryItemViewModel> Rows { get; }
    public int MaxCount => Pouch.MaxCount;

    /// <summary>Re-reads the rows from the pouch (after sort/give-all mutated it).</summary>
    public void ReloadFromPouch()
    {
        for (var i = 0; i < Rows.Count; i++)
        {
            Rows[i].ItemId = Pouch.Items[i].Index;
            Rows[i].Count = Pouch.Items[i].Count;
        }
    }

    private static string GetPouchName(InventoryType type) => type switch
    {
        InventoryType.Items => "Items",
        InventoryType.KeyItems => "Key Items",
        InventoryType.TMHMs => "TMs/HMs",
        InventoryType.Medicine => "Medicine",
        InventoryType.Berries => "Berries",
        InventoryType.Balls => "Balls",
        InventoryType.BattleItems => "Battle Items",
        InventoryType.MailItems => "Mail",
        InventoryType.PCItems => "PC Items",
        InventoryType.FreeSpace => "Free Space",
        InventoryType.ZCrystals => "Z-Crystals",
        InventoryType.Candy => "Candy",
        InventoryType.Treasure => "Treasures",
        InventoryType.Ingredients => "Ingredients",
        _ => type.ToString(),
    };

    private sealed class ItemNameComparer : IComparer<ComboItem>
    {
        public static readonly ItemNameComparer Instance = new();
        public int Compare(ComboItem? x, ComboItem? y) => string.CompareOrdinal(x?.Text, y?.Text);
    }
}
