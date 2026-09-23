using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using PKBank.Desktop.Components.Roamer.ViewModels;
using PKHeX.Core;

namespace PKBank.Desktop.Components.Roamer;

/// <summary>
///     Gen 4 roamer editor: one tab per roamer slot the save can hold at the same
///     time (HGSS: Raikou/Entei/Latias/Latios, D/P/Pt: Mesprit/Cresselia).
///     WinForms never had this editor; the fields mirror Core's Roamer4 structure.
/// </summary>
public sealed partial class Roamer4EditorWindow : Window
{
    private readonly SAV4? _sav;
    private readonly List<RoamerTabViewModel> _tabs = [];

    public Roamer4EditorWindow() => InitializeComponent(); // designer

    public Roamer4EditorWindow(SAV4 sav) : this()
    {
        _sav = sav;

        var species = GameInfo.Strings.specieslist;
        foreach (var (roamer, id) in GetRoamers(sav))
            _tabs.Add(new RoamerTabViewModel(sav, roamer, species[id], id));

        RoamerTabs.ItemsSource = _tabs;
        RoamerTabs.SelectedIndex = 0;
    }

    private static IEnumerable<(Roamer4 Roamer, ushort Species)> GetRoamers(SAV4 sav) => sav switch
    {
        SAV4HGSS hgss =>
        [
            (hgss.RoamerRaikou, (ushort)Species.Raikou),
            (hgss.RoamerEntei, (ushort)Species.Entei),
            (hgss.RoamerLatias, (ushort)Species.Latias),
            (hgss.RoamerLatios, (ushort)Species.Latios)
        ],
        SAV4Pt pt =>
        [
            (pt.RoamerMesprit, (ushort)Species.Mesprit),
            (pt.RoamerCresselia, (ushort)Species.Cresselia)
        ],
        SAV4DP dp =>
        [
            (dp.RoamerMesprit, (ushort)Species.Mesprit),
            (dp.RoamerCresselia, (ushort)Species.Cresselia)
        ],
        _ => []
    };

    private void OnCancelClicked(object? sender, RoutedEventArgs e) => Close();

    private void OnSaveClicked(object? sender, RoutedEventArgs e)
    {
        if (_sav is not { } sav)
        {
            Close();
            return;
        }

        foreach (var tab in _tabs)
            tab.Apply();
        sav.State.Edited = true;
        Close();
    }
}
