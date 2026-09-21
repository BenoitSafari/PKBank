using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using PKHeX.Core;
using PKBank.Core.Events.Files;

namespace PKBank.Desktop.Views.Events;

/// <summary>
/// Record Mixing gift editor for RS/E, mirroring the WC3 Plugin's RM3 form:
/// pick an item the game can hold in the PC (plus the Eon Ticket) and how many
/// times it may still be sent to other players.
/// </summary>
public sealed partial class RecordMixingWindow : Window
{
    private readonly SAV3? _sav;

    public RecordMixingWindow() => InitializeComponent(); // designer

    public RecordMixingWindow(SAV3 sav) : this()
    {
        _sav = sav;

        var names = GameInfo.Strings.GetItemStrings(sav.Context, sav.Version);
        var choices = new List<ComboItem> { new("(None)", 0) };
        choices.AddRange(sav.GetRecordMixingItems()
            .Distinct()
            .Select(id => new ComboItem(GetItemName(names, id), id))
            .OrderBy(z => z.Text));

        ItemCombo.ItemsSource = choices;
        ItemCombo.DisplayMemberBinding = new global::Avalonia.Data.Binding(nameof(ComboItem.Text));

        var current = sav.GetRecordMixing();
        var item = current is not null && sav.IsValidForRecordMixing(current.Item) ? current.Item : (ushort)0;
        ItemCombo.SelectedItem = choices.FirstOrDefault(z => z.Value == item) ?? choices[0];
        CountBox.Value = current?.Count ?? 0;
    }

    private static string GetItemName(IReadOnlyList<string> names, ushort id)
        => id < names.Count && !string.IsNullOrEmpty(names[id]) ? names[id] : $"(Item #{id:000})";

    private void OnItemChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ItemCombo.SelectedItem is not ComboItem selected)
            return;
        if (selected.Value == 0)
            CountBox.Value = 0;
        else if (CountBox.Value is null or 0)
            CountBox.Value = 1;
    }

    private void OnCancelClicked(object? sender, RoutedEventArgs e) => Close();

    private void OnSaveClicked(object? sender, RoutedEventArgs e)
    {
        if (_sav is not { } sav)
        {
            Close();
            return;
        }

        var item = ItemCombo.SelectedItem is ComboItem selected ? (ushort)selected.Value : (ushort)0;
        sav.SetRecordMixing(item, (byte)(CountBox.Value ?? 0));
        sav.State.Edited = true;
        Close();
    }
}
