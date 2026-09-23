using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using PKHeX.Core;

namespace PKBank.Desktop.Components.Roamer;

public sealed partial class Roamer3EditorWindow : Window
{
    private readonly bool _defined;
    private readonly Roamer3? _reader;
    private readonly RoamerLegality? _rules;
    private readonly SAV3? _sav;
    private bool _syncing;

    public Roamer3EditorWindow() => InitializeComponent(); // designer

    public Roamer3EditorWindow(SAV3 sav) : this()
    {
        _sav = sav;
        _reader = new Roamer3(sav.LargeBlock);
        _rules = RoamerLegality.For(sav);
        _defined = _reader.Species != 0;

        var names = GameInfo.Strings.specieslist;
        SpeciesText.Text = _defined && _reader.Species < names.Length
            ? names[_reader.Species]
            : "-";
        LevelText.Text = _defined ? _reader.CurrentLevel.ToString() : "-";

        _syncing = true;
        PidBox.Text = _reader.PID.ToString("X8");
        ShinyCheck.IsChecked = Roamer3.IsShiny(_reader.PID, sav);
        _syncing = false;

        IvHp.Value = _reader.IV_HP;
        IvAtk.Value = _reader.IV_ATK;
        IvDef.Value = _reader.IV_DEF;
        IvSpa.Value = _reader.IV_SPA;
        IvSpd.Value = _reader.IV_SPD;
        IvSpe.Value = _reader.IV_SPE;

        HpBox.Value = _reader.HP_Current;
        ActiveCheck.IsChecked = _reader.IsActive;

        SetEditable(_defined);

        foreach (var box in new[] { IvHp, IvAtk, IvDef, IvSpa, IvSpd, IvSpe, HpBox })
            box.ValueChanged += (_, _) => RefreshLegality();
        ActiveCheck.IsCheckedChanged += (_, _) => RefreshLegality();
        ShinyCheck.IsCheckedChanged += OnShinyToggled;

        RefreshLegality();
    }

    private void SetEditable(bool editable)
    {
        PidBox.IsEnabled = editable;
        ShinyCheck.IsEnabled = editable;
        foreach (var box in new[] { IvHp, IvAtk, IvDef, IvSpa, IvSpd, IvSpe, HpBox })
            box.IsEnabled = editable;
        ActiveCheck.IsEnabled = editable;
        SaveButton.IsEnabled = editable;
    }

    private RoamerState GetState() => new()
    {
        Species = _reader?.Species ?? 0,
        PID = Util.GetHexValue(PidBox.Text ?? string.Empty),
        IV32 = RoamerIVs.Pack(
            (int)(IvHp.Value ?? 0), (int)(IvAtk.Value ?? 0), (int)(IvDef.Value ?? 0),
            (int)(IvSpa.Value ?? 0), (int)(IvSpd.Value ?? 0), (int)(IvSpe.Value ?? 0)),
        Level = _reader?.CurrentLevel ?? 0,
        HpCurrent = (ushort)(HpBox.Value ?? 0),
        IsActive = ActiveCheck.IsChecked == true
    };

    private void Load(RoamerState state)
    {
        _syncing = true;
        PidBox.Text = state.PID.ToString("X8");
        ShinyCheck.IsChecked = _sav is { } sav && Roamer3.IsShiny(state.PID, sav);
        _syncing = false;

        IvHp.Value = RoamerIVs.HP(state.IV32);
        IvAtk.Value = RoamerIVs.ATK(state.IV32);
        IvDef.Value = RoamerIVs.DEF(state.IV32);
        IvSpa.Value = RoamerIVs.SPA(state.IV32);
        IvSpd.Value = RoamerIVs.SPD(state.IV32);
        IvSpe.Value = RoamerIVs.SPE(state.IV32);
        HpBox.Value = state.HpCurrent;
        LevelText.Text = state.Level.ToString();
    }

    private void RefreshLegality(string? message = null)
    {
        if (_rules is null)
            return;

        var verdict = _rules.Check(GetState());
        VerdictText.Text = verdict.Summary;
        VerdictText.Foreground = verdict.Valid ? Brushes.MediumSeaGreen : Brushes.IndianRed;
        FixButton.IsVisible = _defined && !verdict.Valid;

        var detail = message ?? (_defined ? verdict.Detail : "The game has not decided this roamer yet.");
        DetailText.Text = detail;
        DetailText.IsVisible = detail.Length != 0;
    }

    private void OnPidChanged(object? sender, TextChangedEventArgs e)
    {
        if (_syncing || _sav is null)
            return;

        _syncing = true;
        ShinyCheck.IsChecked = Roamer3.IsShiny(Util.GetHexValue(PidBox.Text ?? string.Empty), _sav);
        _syncing = false;
        RefreshLegality();
    }

    private void OnShinyToggled(object? sender, RoutedEventArgs e)
    {
        if (_syncing || _rules is null)
            return;

        var state = GetState();
        var message = _rules.SetShiny(state, ShinyCheck.IsChecked == true);
        Load(state);
        RefreshLegality(message);
    }

    private void OnFixClicked(object? sender, RoutedEventArgs e)
    {
        if (_rules is null)
            return;

        var state = GetState();
        if (!_rules.TryFix(state, out var message))
        {
            RefreshLegality(message);
            return;
        }

        Load(state);
        RefreshLegality(message);
    }

    private void OnCancelClicked(object? sender, RoutedEventArgs e) => Close();

    private void OnSaveClicked(object? sender, RoutedEventArgs e)
    {
        if (_sav is null || _reader is null || !_defined)
        {
            Close();
            return;
        }

        var state = GetState();
        _reader.PID = state.PID;
        _reader.IV32 = state.IV32;
        _reader.IsActive = state.IsActive;
        _reader.CurrentLevel = state.Level;
        _reader.HP_Current = state.HpCurrent;

        _sav.State.Edited = true;
        Close();
    }
}
