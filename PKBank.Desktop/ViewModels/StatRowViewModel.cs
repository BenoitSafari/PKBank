using System;
using PKHeX.Core;

namespace PKBank.Desktop.ViewModels;

/// <summary>
/// One stat line (IV/EV/computed total) of the Pokémon editor.
/// <see cref="PkIndex"/> uses the internal PKM stat order (0 HP, 1 Atk, 2 Def, 3 Spe, 4 SpA, 5 SpD).
/// </summary>
public sealed class StatRowViewModel(PokemonEditorViewModel owner, string name, int pkIndex) : ViewModelBase
{
    private ushort _stat;

    public string Name { get; } = name;
    public int PkIndex { get; } = pkIndex;

    public int IV
    {
        get => GetIV();
        set
        {
            SetIV(Math.Clamp(value, 0, owner.Entity.MaxIV));
            OnPropertyChanged();
            owner.OnStatsEdited();
        }
    }

    public int EV
    {
        get => GetEV();
        set
        {
            SetEV(Math.Clamp(value, 0, owner.Entity.MaxEV));
            OnPropertyChanged();
            owner.OnStatsEdited();
        }
    }

    public ushort Stat { get => _stat; private set => SetField(ref _stat, value); }

    public void RefreshAll(ReadOnlySpan<ushort> stats)
    {
        Stat = stats[PkIndex];
        OnPropertyChanged(nameof(IV));
        OnPropertyChanged(nameof(EV));
    }

    private int GetIV() => PkIndex switch
    {
        0 => owner.Entity.IV_HP,
        1 => owner.Entity.IV_ATK,
        2 => owner.Entity.IV_DEF,
        3 => owner.Entity.IV_SPE,
        4 => owner.Entity.IV_SPA,
        _ => owner.Entity.IV_SPD,
    };

    private void SetIV(int value)
    {
        var pk = owner.Entity;
        switch (PkIndex)
        {
            case 0: pk.IV_HP = value; break;
            case 1: pk.IV_ATK = value; break;
            case 2: pk.IV_DEF = value; break;
            case 3: pk.IV_SPE = value; break;
            case 4: pk.IV_SPA = value; break;
            default: pk.IV_SPD = value; break;
        }
    }

    private int GetEV() => PkIndex switch
    {
        0 => owner.Entity.EV_HP,
        1 => owner.Entity.EV_ATK,
        2 => owner.Entity.EV_DEF,
        3 => owner.Entity.EV_SPE,
        4 => owner.Entity.EV_SPA,
        _ => owner.Entity.EV_SPD,
    };

    private void SetEV(int value)
    {
        var pk = owner.Entity;
        switch (PkIndex)
        {
            case 0: pk.EV_HP = value; break;
            case 1: pk.EV_ATK = value; break;
            case 2: pk.EV_DEF = value; break;
            case 3: pk.EV_SPE = value; break;
            case 4: pk.EV_SPA = value; break;
            default: pk.EV_SPD = value; break;
        }
    }
}
