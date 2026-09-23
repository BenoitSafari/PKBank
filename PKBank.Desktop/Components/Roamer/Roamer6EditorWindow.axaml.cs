using Avalonia.Controls;
using Avalonia.Interactivity;
using PKHeX.Core;

namespace PKBank.Desktop.Components.Roamer;

/// <summary>
/// X/Y roamer editor mirroring WinForms' SAV_Roamer6: which legendary bird
/// roams, how many times it was encountered, and its roam state.
/// Values are only written back to the save on Save.
/// </summary>
public sealed partial class Roamer6EditorWindow : Window
{
    private const int SpeciesOffset = 144; // Articuno
    private const int StarterChoiceIndex = 48;

    private readonly SAV6XY? _sav;
    private readonly Roamer6? _roamer;

    public Roamer6EditorWindow() => InitializeComponent(); // designer

    public Roamer6EditorWindow(SAV6XY sav) : this()
    {
        _sav = sav;
        _roamer = sav.Encount.Roamer;

        var species = GameInfo.Strings.specieslist;
        SpeciesCombo.ItemsSource = new[]
        {
            species[(int)Species.Articuno],
            species[(int)Species.Zapdos],
            species[(int)Species.Moltres],
        };
        SpeciesCombo.SelectedIndex = GetInitialIndex(sav, _roamer);

        EncounterBox.Value = _roamer.TimesEncountered;

        StateCombo.ItemsSource = new[] { "Inactive", "Roaming", "Stationary", "Defeated", "Captured" };
        StateCombo.SelectedIndex = (int)_roamer.RoamStatus;
    }

    private static int GetInitialIndex(SAV6XY sav, Roamer6 roamer)
    {
        if (roamer.Species != 0)
            return roamer.Species - SpeciesOffset;
        // Roamer Species is not set if the player hasn't beaten the league so derive the species from the starter choice
        return sav.EventWork.GetWork(StarterChoiceIndex);
    }

    private void OnCancelClicked(object? sender, RoutedEventArgs e) => Close();

    private void OnSaveClicked(object? sender, RoutedEventArgs e)
    {
        if (_sav is not { } sav || _roamer is not { } roamer)
        {
            Close();
            return;
        }

        if (SpeciesCombo.SelectedIndex >= 0)
            roamer.Species = (ushort)(SpeciesOffset + SpeciesCombo.SelectedIndex);
        roamer.TimesEncountered = (uint)(EncounterBox.Value ?? 0);
        if (StateCombo.SelectedIndex >= 0)
            roamer.RoamStatus = (Roamer6State)StateCombo.SelectedIndex;

        sav.State.Edited = true;
        Close();
    }
}
