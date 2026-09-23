using System.Runtime.CompilerServices;
using PKHeX.Core;

namespace PKBank.Desktop.Components.Roamer.ViewModels;

public sealed class RoamerTabViewModel : ViewModelBase
{
    private readonly Roamer4 _roamer;
    private readonly RoamerLegality _rules;
    private readonly SaveFile _sav;
    private decimal? _hpCurrent;
    private bool _isActive;
    private decimal? _ivAtk;
    private decimal? _ivDef;
    private decimal? _ivHp;
    private decimal? _ivSpa;
    private decimal? _ivSpd;
    private decimal? _ivSpe;
    private byte _level;
    private decimal? _location;
    private string? _pendingMessage;

    private string _pidText;
    private decimal? _status;

    public RoamerTabViewModel(SAV4 sav, Roamer4 roamer, string label, ushort expectedSpecies, byte expectedLevel)
    {
        _sav = sav;
        _roamer = roamer;
        _rules = RoamerLegality.For(sav, expectedSpecies, expectedLevel);
        Label = label;
        ExpectedSpecies = expectedSpecies;
        IsDefined = roamer.Species != 0;

        _isActive = roamer.IsActive;
        _level = roamer.Level;
        _hpCurrent = roamer.Stat_HPCurrent;
        _location = roamer.Location;
        _status = roamer.Status;
        _ivHp = roamer.IV_HP;
        _ivAtk = roamer.IV_ATK;
        _ivDef = roamer.IV_DEF;
        _ivSpa = roamer.IV_SPA;
        _ivSpd = roamer.IV_SPD;
        _ivSpe = roamer.IV_SPE;
        _pidText = roamer.PID.ToString("X8");

        RefreshLegality();
    }

    public string Label { get; }
    public ushort ExpectedSpecies { get; }

    public bool IsDefined { get; }

    public bool IsActive
    {
        get => _isActive;
        set => SetStaged(ref _isActive, value);
    }

    public decimal? HpCurrent
    {
        get => _hpCurrent;
        set => SetStaged(ref _hpCurrent, value);
    }

    public decimal? Location
    {
        get => _location;
        set => SetStaged(ref _location, value);
    }

    public decimal? Status
    {
        get => _status;
        set => SetStaged(ref _status, value);
    }

    public decimal? IvHp
    {
        get => _ivHp;
        set => SetStaged(ref _ivHp, value);
    }

    public decimal? IvAtk
    {
        get => _ivAtk;
        set => SetStaged(ref _ivAtk, value);
    }

    public decimal? IvDef
    {
        get => _ivDef;
        set => SetStaged(ref _ivDef, value);
    }

    public decimal? IvSpa
    {
        get => _ivSpa;
        set => SetStaged(ref _ivSpa, value);
    }

    public decimal? IvSpd
    {
        get => _ivSpd;
        set => SetStaged(ref _ivSpd, value);
    }

    public decimal? IvSpe
    {
        get => _ivSpe;
        set => SetStaged(ref _ivSpe, value);
    }

    public string LevelDisplay => IsDefined ? _level.ToString() : "—";

    public string SpeciesDisplay => IsDefined ? Label : "Not decided yet";

    public string PidText
    {
        get => _pidText;
        set
        {
            if (!SetField(ref _pidText, value))
                return;
            OnPropertyChanged(nameof(IsShiny));
            RefreshLegality();
        }
    }

    public bool IsShiny
    {
        get => Roamer3.IsShiny(Util.GetHexValue(_pidText), _sav);
        set
        {
            if (value == IsShiny)
                return;
            var state = GetState();
            _pendingMessage = _rules.SetShiny(state, value);
            LoadFrom(state);
        }
    }

    public bool LegalityValid { get; private set; }
    public string LegalitySummary { get; private set; } = string.Empty;
    public string LegalityDetail { get; private set; } = string.Empty;
    public bool HasLegalityDetail => LegalityDetail.Length != 0;
    public bool CanFix => IsDefined && !LegalityValid;

    public void Fix()
    {
        var state = GetState();
        _rules.TryFix(state, out var message);
        _pendingMessage = message;
        LoadFrom(state);
    }

    public void Apply()
    {
        if (!IsDefined)
            return;

        var state = GetState();
        _roamer.PID = state.PID;
        _roamer.IV32 = state.IV32;
        _roamer.IsActive = state.IsActive;
        _roamer.Level = state.Level;
        _roamer.Stat_HPCurrent = state.HpCurrent;
        _roamer.Species = state.Species;
        _roamer.Location = (int)(Location ?? 0);
        _roamer.Status = (byte)(Status ?? 0);
    }

    private RoamerState GetState() => new()
    {
        Species = _roamer.Species,
        PID = Util.GetHexValue(_pidText),
        IV32 = RoamerIVs.Pack(
            (int)(IvHp ?? 0), (int)(IvAtk ?? 0), (int)(IvDef ?? 0),
            (int)(IvSpa ?? 0), (int)(IvSpd ?? 0), (int)(IvSpe ?? 0)),
        Level = _level,
        HpCurrent = (ushort)(HpCurrent ?? 0),
        IsActive = IsActive
    };

    private void LoadFrom(RoamerState state)
    {
        _pidText = state.PID.ToString("X8");
        _level = state.Level;
        _hpCurrent = state.HpCurrent;
        _ivHp = RoamerIVs.HP(state.IV32);
        _ivAtk = RoamerIVs.ATK(state.IV32);
        _ivDef = RoamerIVs.DEF(state.IV32);
        _ivSpa = RoamerIVs.SPA(state.IV32);
        _ivSpd = RoamerIVs.SPD(state.IV32);
        _ivSpe = RoamerIVs.SPE(state.IV32);

        OnPropertyChanged(nameof(PidText));
        OnPropertyChanged(nameof(IsShiny));
        OnPropertyChanged(nameof(LevelDisplay));
        OnPropertyChanged(nameof(HpCurrent));
        OnPropertyChanged(nameof(IvHp));
        OnPropertyChanged(nameof(IvAtk));
        OnPropertyChanged(nameof(IvDef));
        OnPropertyChanged(nameof(IvSpa));
        OnPropertyChanged(nameof(IvSpd));
        OnPropertyChanged(nameof(IvSpe));

        RefreshLegality();
    }

    private void SetStaged<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (SetField(ref field, value, name))
            RefreshLegality();
    }

    private void RefreshLegality()
    {
        var verdict = _rules.Check(GetState());
        LegalityValid = verdict.Valid;
        LegalitySummary = verdict.Summary;
        LegalityDetail = _pendingMessage ?? (IsDefined ? verdict.Detail : "The game has not decided this roamer yet.");
        _pendingMessage = null;

        OnPropertyChanged(nameof(LegalityValid));
        OnPropertyChanged(nameof(LegalitySummary));
        OnPropertyChanged(nameof(LegalityDetail));
        OnPropertyChanged(nameof(HasLegalityDetail));
        OnPropertyChanged(nameof(CanFix));
    }
}
