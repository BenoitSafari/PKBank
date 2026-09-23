using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using PKHeX.Core;

namespace PKBank.Desktop.Components.Roamer;

/// <summary>
/// Gen 3 roamer editor mirroring WinForms' SAV_Roamer3: species, PID (with a
/// live shiny indicator), IVs, level, current HP and active state.
/// Values are only written back to the save on Save.
/// </summary>
public sealed partial class Roamer3EditorWindow : Window
{
    private readonly SAV3? _sav;
    private readonly Roamer3? _reader;

    public Roamer3EditorWindow() => InitializeComponent(); // designer

    public Roamer3EditorWindow(SAV3 sav) : this()
    {
        _sav = sav;
        _reader = new Roamer3(sav.LargeBlock);

        var species = GameInfo.FilteredSources.Species;
        SpeciesCombo.ItemsSource = species;
        SpeciesCombo.SelectedItem = species.FirstOrDefault(z => z.Value == _reader.Species) ?? species.FirstOrDefault();

        PidBox.Text = _reader.PID.ToString("X8");
        RefreshShiny(_reader.PID);

        IvHp.Value = _reader.IV_HP;
        IvAtk.Value = _reader.IV_ATK;
        IvDef.Value = _reader.IV_DEF;
        IvSpa.Value = _reader.IV_SPA;
        IvSpd.Value = _reader.IV_SPD;
        IvSpe.Value = _reader.IV_SPE;

        LevelBox.Value = _reader.CurrentLevel;
        HpBox.Value = _reader.HP_Current;
        ActiveCheck.IsChecked = _reader.IsActive;
    }

    private void RefreshShiny(uint pid)
    {
        if (_sav is { } sav)
            ShinyCheck.IsChecked = Roamer3.IsShiny(pid, sav);
    }

    private void OnPidChanged(object? sender, TextChangedEventArgs e)
        => RefreshShiny(Util.GetHexValue(PidBox.Text ?? string.Empty));

    private void OnCancelClicked(object? sender, RoutedEventArgs e) => Close();

    private void OnSaveClicked(object? sender, RoutedEventArgs e)
    {
        if (_sav is not { } sav || _reader is not { } reader)
        {
            Close();
            return;
        }

        reader.PID = Util.GetHexValue(PidBox.Text ?? string.Empty);
        if (SpeciesCombo.SelectedItem is ComboItem species)
            reader.Species = (ushort)species.Value;
        reader.SetIVs(
        [
            (int)(IvHp.Value ?? 0),
            (int)(IvAtk.Value ?? 0),
            (int)(IvDef.Value ?? 0),
            (int)(IvSpe.Value ?? 0),
            (int)(IvSpa.Value ?? 0),
            (int)(IvSpd.Value ?? 0),
        ]);
        reader.IsActive = ActiveCheck.IsChecked == true;
        reader.CurrentLevel = (byte)(LevelBox.Value ?? 0);
        reader.HP_Current = (ushort)(HpBox.Value ?? 0);

        sav.State.Edited = true;
        Close();
    }
}
