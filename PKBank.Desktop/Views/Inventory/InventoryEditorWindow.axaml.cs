using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using PKBank.Desktop.ViewModels;
using PKHeX.Core;

namespace PKBank.Desktop.Views.Inventory;

/// <summary>
/// Inventory editor mirroring WinForms' SAV_Inventory: one tab per pouch,
/// item/count rows, sort and give-all helpers, applied to the save on demand.
/// </summary>
public sealed partial class InventoryEditorWindow : Window
{
    private readonly SaveFile? _sav;
    private readonly PlayerBag? _bag;
    private readonly bool _hasNewFlag;
    private readonly string[] _itemNames = [];
    private readonly List<InventoryPouchViewModel> _pouches = [];

    public InventoryEditorWindow() => InitializeComponent(); // designer

    public InventoryEditorWindow(SaveFile sav) : this()
    {
        _sav = sav;
        _bag = sav.Inventory;

        _itemNames = [.. GameInfo.Strings.GetItemStrings(sav.Context, sav.Version)];
        for (int i = 0; i < _itemNames.Length; i++)
        {
            if (string.IsNullOrEmpty(_itemNames[i]))
                _itemNames[i] = $"(Item #{i:000})";
        }

        foreach (var pouch in _bag.Pouches)
            _pouches.Add(new InventoryPouchViewModel(pouch, _itemNames));
        _hasNewFlag = _bag.Pouches.Count != 0 && _bag.Pouches[0].Items is [IItemNewFlag, ..];

        PouchTabs.ItemsSource = _pouches;
        PouchTabs.SelectedIndex = 0;
    }

    private InventoryPouchViewModel? CurrentPouch => PouchTabs.SelectedItem as InventoryPouchViewModel;

    private void OnSortClicked(object? sender, RoutedEventArgs e)
    {
        if (CurrentPouch is not { } pouch)
            return;
        ApplyRowsToPouch(pouch);
        pouch.Pouch.SortByName(_itemNames);
        pouch.Pouch.SortByEmpty();
        pouch.ReloadFromPouch();
    }

    private void OnCancelClicked(object? sender, RoutedEventArgs e) => Close();

    private void OnSaveClicked(object? sender, RoutedEventArgs e)
    {
        if (_sav is not { } sav || _bag is not { } bag)
        {
            Close();
            return;
        }

        foreach (var pouch in _pouches)
            ApplyRowsToPouch(pouch);
        bag.CopyTo(sav);
        sav.State.Edited = true;
        Close();
    }

    /// <summary>Writes the UI rows back into the pouch, WinForms-style: sane counts only, empty slots compressed to the end.</summary>
    private void ApplyRowsToPouch(InventoryPouchViewModel pouchVm)
    {
        if (_bag is not { } bag)
            return;
        var pouch = pouchVm.Pouch;
        int ctr = 0;
        foreach (var row in pouchVm.Rows)
        {
            var itemId = row.ItemId;
            if (itemId <= 0)
                continue;
            int count = row.Count ?? 0;
            if (!bag.IsQuantitySane(pouch.Type, itemId, ref count, _hasNewFlag))
                continue;
            pouch.Items[ctr++] = pouch.GetEmpty(itemId, count);
        }
        for (; ctr < pouch.Items.Length; ctr++)
            pouch.Items[ctr] = pouch.GetEmpty();
        pouchVm.ReloadFromPouch();
    }
}
