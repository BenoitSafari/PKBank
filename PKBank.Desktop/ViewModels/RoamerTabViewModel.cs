using PKHeX.Core;

namespace PKBank.Desktop.ViewModels;

/// <summary>
/// One Gen 4 roamer slot (HGSS has four, D/P/Pt two). Values are staged in the
/// view-model and only written to the save structure by <see cref="Apply"/>.
/// </summary>
public sealed class RoamerTabViewModel : ViewModelBase
{
    private readonly Roamer4 _roamer;
    private readonly SaveFile _sav;
    private string _pidText;

    public string Label { get; }
    public ushort ExpectedSpecies { get; }

    public bool IsActive { get; set; }
    public decimal? Level { get; set; }
    public decimal? HpCurrent { get; set; }
    public decimal? Location { get; set; }
    public decimal? Status { get; set; }
    public decimal? IvHp { get; set; }
    public decimal? IvAtk { get; set; }
    public decimal? IvDef { get; set; }
    public decimal? IvSpa { get; set; }
    public decimal? IvSpd { get; set; }
    public decimal? IvSpe { get; set; }

    public RoamerTabViewModel(SaveFile sav, Roamer4 roamer, string label, ushort expectedSpecies)
    {
        _sav = sav;
        _roamer = roamer;
        Label = label;
        ExpectedSpecies = expectedSpecies;

        IsActive = roamer.IsActive;
        Level = roamer.Level;
        HpCurrent = roamer.Stat_HPCurrent;
        Location = roamer.Location;
        Status = roamer.Status;
        IvHp = roamer.IV_HP;
        IvAtk = roamer.IV_ATK;
        IvDef = roamer.IV_DEF;
        IvSpa = roamer.IV_SPA;
        IvSpd = roamer.IV_SPD;
        IvSpe = roamer.IV_SPE;
        _pidText = roamer.PID.ToString("X8");
    }

    public string PidText
    {
        get => _pidText;
        set
        {
            if (SetField(ref _pidText, value))
                OnPropertyChanged(nameof(IsShiny));
        }
    }

    /// <summary>Same shiny xor rule as Gen 3 (xor of trainer ID pair and PID halves &lt; 8).</summary>
    public bool IsShiny => Roamer3.IsShiny(Util.GetHexValue(_pidText), _sav);

    /// <summary>Writes the staged values back into the save's roamer structure.</summary>
    public void Apply()
    {
        _roamer.PID = Util.GetHexValue(_pidText);
        _roamer.SetIVs(
        [
            (int)(IvHp ?? 0),
            (int)(IvAtk ?? 0),
            (int)(IvDef ?? 0),
            (int)(IvSpe ?? 0),
            (int)(IvSpa ?? 0),
            (int)(IvSpd ?? 0),
        ]);
        _roamer.IsActive = IsActive;
        _roamer.Level = (byte)(Level ?? 0);
        _roamer.Stat_HPCurrent = (ushort)(HpCurrent ?? 0);
        _roamer.Location = (int)(Location ?? 0);
        _roamer.Status = (byte)(Status ?? 0);
        // A never-spawned slot (blank save) has no species; give the activated
        // roamer its identity so the game can actually spawn it.
        if (IsActive && _roamer.Species == 0)
            _roamer.Species = ExpectedSpecies;
    }
}
