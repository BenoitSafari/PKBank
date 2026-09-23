using System;
using Avalonia.Media;
using PKBank.Desktop.Utils;
using PKHeX.Core;
using PKBank.Desktop.Components;

namespace PKBank.Desktop.Components.PokemonEditor.ViewModels;

/// <summary>
///     One stat line (base stat, IV, EV, computed total) of the Pokémon editor.
///     <see cref="PkIndex" /> uses the internal PKM stat order (0 HP, 1 Atk, 2 Def, 3 Spe, 4 SpA, 5 SpD).
/// </summary>
public sealed class StatRowViewModel(PokemonEditorViewModel owner, string name, int pkIndex) : ViewModelBase
{
    private ushort _stat;

    public string Name { get; } = name;
    public int PkIndex { get; } = pkIndex;

    public string Base => owner.Entity.PersonalInfo.GetBaseStatValue(PkIndex).ToString("000");
    public IBrush BaseBrush => StatColors.BaseStat(owner.Entity.PersonalInfo.GetBaseStatValue(PkIndex));

    public int MaxIV => owner.Entity.MaxIV;
    public int MaxEV => owner.Entity.MaxEV;

    // Text-based inputs so every keystroke updates the computed stats live.
    // Empty, invalid or negative text is treated as 0; values above the cap
    // are clamped and pushed back to the field.
    public string IVText
    {
        get => GetIV().ToString();
        set => ApplyText(value, MaxIV, SetIV);
    }

    public string EVText
    {
        get => GetEV().ToString();
        set
        {
            ApplyText(value, MaxEV, SetEV);
            OnPropertyChanged(nameof(EVBrush));
        }
    }

    public string IVTip => $"Max: {MaxIV}";
    public string EVTip => $"Max: {MaxEV}";

    /// <summary>Highlights the EV field when the stored value exceeds the per-stat cap (hacked data).</summary>
    public IBrush? EVBrush => owner.Entity.Format >= 3 && GetEV() > MaxEV ? StatColors.EVFieldMaxed : null;

    public ushort Stat
    {
        get => _stat;
        private set => SetField(ref _stat, value);
    }

    // Out-of-range display correction happens at the control level (TextChanged);
    // this clamp is defense in depth for the stored value.
    private void ApplyText(string? text, int max, Action<int> apply)
    {
        apply(Math.Clamp(ParseStat(text), 0, max));
        owner.OnStatsEdited();
    }

    private static int ParseStat(string? text) => int.TryParse(text, out var value) && value > 0 ? value : 0;

    /// <summary>Refreshes computed values only; safe to call while the user is typing.</summary>
    public void RefreshComputed(ReadOnlySpan<ushort> stats)
    {
        Stat = stats[PkIndex];
        OnPropertyChanged(nameof(EVBrush));
    }

    /// <summary>Pushes the stored values back into the input fields (load, randomize, focus loss).</summary>
    public void RefreshInputTexts()
    {
        OnPropertyChanged(nameof(IVText));
        OnPropertyChanged(nameof(EVText));
    }

    public void RefreshAll(ReadOnlySpan<ushort> stats)
    {
        RefreshComputed(stats);
        RefreshInputTexts();
        OnPropertyChanged(nameof(Base));
        OnPropertyChanged(nameof(BaseBrush));
        OnPropertyChanged(nameof(IVTip));
        OnPropertyChanged(nameof(EVTip));
    }

    private int GetIV() => PkIndex switch
    {
        0 => owner.Entity.IV_HP,
        1 => owner.Entity.IV_ATK,
        2 => owner.Entity.IV_DEF,
        3 => owner.Entity.IV_SPE,
        4 => owner.Entity.IV_SPA,
        _ => owner.Entity.IV_SPD
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
        _ => owner.Entity.EV_SPD
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
