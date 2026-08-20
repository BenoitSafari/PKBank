using System;
using System.Collections.Generic;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using PKBank.Desktop.Services;
using PKBank.Desktop.Sprites;
using PKHeX.Core;

namespace PKBank.Desktop.ViewModels;

/// <summary>
/// Edits a working copy of the Pokémon in a given slot; <see cref="Apply"/> writes it back to the save.
/// </summary>
public sealed class PokemonEditorViewModel : ViewModelBase
{
    private readonly SaveFile _sav;
    private readonly FilteredGameDataSource _sources;
    private readonly SlotViewModel _origin;
    private PKM _pk;
    private bool _loading;

    public event Action? Applied;

    public PokemonEditorViewModel(SaveFile sav, FilteredGameDataSource sources, SlotViewModel origin)
    {
        _sav = sav;
        _sources = sources;
        _origin = origin;
        _pk = origin.Read();

        StatRows =
        [
            new(this, "HP", 0),
            new(this, "Attack", 1),
            new(this, "Defense", 2),
            new(this, "Sp. Atk", 4),
            new(this, "Sp. Def", 5),
            new(this, "Speed", 3),
        ];

        AbilityList = [];
        FormList = [];
        Load();
    }

    internal PKM Entity => _pk;

    /// <summary>Slot this editor was opened from; <see cref="Apply"/> writes back to it.</summary>
    public SlotViewModel Origin => _origin;

    /// <summary>Snapshot of the entity currently being edited (with pending changes).</summary>
    public PKM GetEntityClone() => _pk.Clone();

    public bool HasSpecies => _pk.Species != 0;

    public void RefreshSprite() => OnPropertyChanged(nameof(Sprite));

    // Static data sources (per save file)
    public IReadOnlyList<ComboItem> SpeciesList => _sources.Species;
    public IReadOnlyList<ComboItem> ItemList => _sources.Items;
    public IReadOnlyList<ComboItem> MoveList => _sources.Moves;
    public IReadOnlyList<ComboItem> NatureList => _sources.Natures;
    public IReadOnlyList<ComboItem> BallList => _sources.Balls;
    public IReadOnlyList<ComboItem> LanguageList => _sources.Languages;

    public IReadOnlyList<ComboItem> AbilityList { get; private set; }
    public IReadOnlyList<string> FormList { get; private set; }

    public IReadOnlyList<StatRowViewModel> StatRows { get; }

    // Capability flags for hiding fields not present in the save's generation
    public bool HasNature => _pk.Format >= 3;
    public bool HasAbility => _pk.Format >= 3;
    public bool HasBall => _pk.Format >= 3;
    public bool HasItem => ItemList.Count > 0;
    public bool HasLanguage => _pk.Format >= 3;
    public bool HasShiny => true;
    public bool HasForms => FormList.Count > 1;
    public bool CanCycleGender => _pk.Format >= 3 && _pk.PersonalInfo.IsDualGender && _pk.Species != 0;

    public Bitmap? Sprite => SpriteService.GetPokemonSprite(_pk);

    public int Species
    {
        get => _pk.Species;
        set
        {
            if (_loading || value < 0 || value == _pk.Species)
                return;

            if (_pk.Species == 0 && value > 0)
            {
                // Slot was empty: seed sane defaults from the save's trainer first.
                EntityTemplates.TemplateFields(_pk, _sav);
            }

            _pk.Species = (ushort)value;
            var pi = _pk.PersonalInfo;
            if (_pk.Form >= pi.FormCount)
                _pk.Form = 0;
            _pk.Gender = _pk.GetSaneGender();
            if (!_pk.IsNicknamed)
                _pk.ClearNickname();

            RebuildSpeciesDependentLists();
            RefreshAll();
        }
    }

    public string Nickname
    {
        get => _pk.Nickname;
        set
        {
            if (_loading || value == _pk.Nickname)
                return;
            if (string.IsNullOrWhiteSpace(value))
                _pk.ClearNickname();
            else
                _pk.SetNickname(value);
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsNicknamed));
            RefreshDerived();
        }
    }

    public bool IsNicknamed
    {
        get => _pk.IsNicknamed;
        set
        {
            if (_loading || value == _pk.IsNicknamed)
                return;
            if (value)
                _pk.IsNicknamed = true;
            else
                _pk.ClearNickname();
            OnPropertyChanged();
            OnPropertyChanged(nameof(Nickname));
            RefreshDerived();
        }
    }

    public int Level
    {
        get => _pk.CurrentLevel;
        set
        {
            var level = Math.Clamp(value, 1, 100);
            if (_loading || level == _pk.CurrentLevel)
                return;
            _pk.CurrentLevel = (byte)level;
            OnPropertyChanged();
            RefreshDerived();
        }
    }

    public int Form
    {
        get => _pk.Form;
        set
        {
            if (_loading || value < 0 || value == _pk.Form)
                return;
            _pk.Form = (byte)value;
            RebuildAbilityList();
            RefreshAll();
        }
    }

    public int Nature
    {
        get => (int)_pk.Nature;
        set
        {
            if (_loading || value < 0 || value == (int)_pk.Nature)
                return;
            _pk.SetNature((Nature)value);
            OnPropertyChanged();
            RefreshDerived();
        }
    }

    public int AbilityIndex
    {
        get => _pk.AbilityNumber switch { 2 => 1, 4 => 2, _ => 0 };
        set
        {
            if (_loading || value < 0 || value == AbilityIndex)
                return;
            _pk.SetAbilityIndex(value);
            OnPropertyChanged();
            RefreshDerived();
        }
    }

    public int HeldItem
    {
        get => _pk.HeldItem;
        set
        {
            if (_loading || value < 0 || value == _pk.HeldItem)
                return;
            _pk.HeldItem = value;
            OnPropertyChanged();
            RefreshDerived();
        }
    }

    public int Ball
    {
        get => _pk.Ball;
        set
        {
            if (_loading || value < 0 || value == _pk.Ball)
                return;
            _pk.Ball = (byte)value;
            OnPropertyChanged();
            RefreshDerived();
        }
    }

    public int Language
    {
        get => _pk.Language;
        set
        {
            if (_loading || value < 0 || value == _pk.Language)
                return;
            _pk.Language = value;
            OnPropertyChanged();
            RefreshDerived();
        }
    }

    public bool IsShiny
    {
        get => _pk.IsShiny;
        set
        {
            if (_loading || value == _pk.IsShiny)
                return;
            _pk.SetIsShiny(value);
            OnPropertyChanged();
            RefreshDerived();
        }
    }

    public string GenderSymbol => _pk.Gender switch { 0 => "♂", 1 => "♀", _ => "—" };

    public int Move1 { get => _pk.GetMove(0); set => SetMove(0, value); }
    public int Move2 { get => _pk.GetMove(1); set => SetMove(1, value); }
    public int Move3 { get => _pk.GetMove(2); set => SetMove(2, value); }
    public int Move4 { get => _pk.GetMove(3); set => SetMove(3, value); }

    public bool LegalityValid { get; private set; }
    public string LegalitySummary { get; private set; } = string.Empty;
    public string LegalityReport { get; private set; } = string.Empty;

    // Column totals (mirrors the WinForms StatEditor total row, shown for Format >= 3)
    public bool ShowTotalsRow => _pk.Format >= 3;
    public string BST => _pk.PersonalInfo.GetBaseStatTotal().ToString("000");
    public IBrush BSTBrush => StatColors.BaseStatTotal(_pk.PersonalInfo.GetBaseStatTotal());
    public int IVTotal => _pk.IVTotal;
    public int EVTotal => _pk.EVTotal;
    public IBrush? EVTotalBrush => StatColors.GetEVTotalBrush(_pk.EVTotal);
    public bool EVTotalHasColor => EVTotalBrush is not null;
    public string EVRemainingTip => $"Remaining: {EffortValues.Max510 - _pk.EVTotal}";

    /// <summary>Randomize IVs; Ctrl (max) fills flawless, Alt (clear) zeroes. Mirrors WinForms UpdateRandomIVs.</summary>
    public void RandomizeIVs(bool max, bool clear)
    {
        Span<int> ivs = stackalloc int[6];
        if (max)
        {
            ivs.Fill(_pk.MaxIV);
            _pk.SetIVs(ivs);
        }
        else if (clear)
        {
            _pk.SetIVs(ivs);
        }
        else
        {
            var la = new LegalityAnalysis(_pk);
            var enc = la.EncounterMatch;
            if (enc is IFlawlessIVCount { FlawlessIVCount: not 0 } fc)
                _pk.SetRandomIVs(ivs, fc.FlawlessIVCount);
            else if (enc is IFixedIVSet { IVs: { IsSpecified: true } iv })
                _pk.SetRandomIVs(ivs, iv);
            else if (enc is IFlawlessIVCountConditional c && c.GetFlawlessIVCount(_pk) is { Max: not 0 } x)
                _pk.SetRandomIVs(ivs, Util.Rand.Next(x.Min, x.Max + 1));
            else
                _pk.SetRandomIVs(ivs);
        }
        RefreshDerived();
    }

    /// <summary>Randomize EVs; Ctrl (max) distributes maximum, Alt (clear) zeroes. Mirrors WinForms UpdateRandomEVs.</summary>
    public void RandomizeEVs(bool max, bool clear)
    {
        Span<int> evs = stackalloc int[6];
        if (max)
            EffortValues.SetMax(evs, _pk);
        else if (clear)
            EffortValues.Clear(evs);
        else
            EffortValues.SetRandom(evs, _pk.Format);
        _pk.SetEVs(evs);
        RefreshDerived();
    }

    public void CycleGender()
    {
        if (!CanCycleGender)
            return;
        var gender = (byte)(_pk.Gender == 0 ? 1 : 0);
        if (_pk.Format <= 5)
            _pk.SetPIDGender(gender);
        else
            _pk.Gender = gender;
        OnPropertyChanged(nameof(GenderSymbol));
        RefreshDerived();
    }

    public void Apply()
    {
        if (_origin.IsParty)
            _pk.ResetPartyStats();
        _pk.RefreshChecksum();
        _origin.Write(_pk);
        _pk = _origin.Read(); // reload the save-normalized copy
        _sav.State.Edited = true;
        Load();
        Applied?.Invoke();
    }

    public void Revert()
    {
        _pk = _origin.Read();
        Load();
    }

    internal void OnStatsEdited()
    {
        if (!_loading)
            RefreshDerived(refreshInputTexts: false); // keep the field being typed in untouched
    }

    /// <summary>Normalizes the stat input fields (e.g. shows 0 for an emptied field on focus loss).</summary>
    public void RefreshStatInputTexts()
    {
        foreach (var row in StatRows)
            row.RefreshInputTexts();
    }

    private void SetMove(int index, int value)
    {
        if (_loading || value < 0 || value == _pk.GetMove(index))
            return;
        _pk.SetMove(index, (ushort)value);
        _pk.HealPP();
        OnPropertyChanged($"Move{index + 1}");
        RefreshDerived();
    }

    private void Load()
    {
        _loading = true;
        RebuildSpeciesDependentLists();
        _loading = false;
        RefreshAll();
    }

    private void RebuildSpeciesDependentLists()
    {
        RebuildAbilityList();
        RebuildFormList();
    }

    private void RebuildAbilityList()
    {
        AbilityList = _pk.Format >= 3 ? _sources.GetAbilityList(_pk.PersonalInfo) : [];
        OnPropertyChanged(nameof(AbilityList));
    }

    private void RebuildFormList()
    {
        var strings = GameInfo.Strings;
        FormList = FormConverter.GetFormList(_pk.Species, strings.types, strings.forms, GameInfo.GenderSymbolUnicode, _pk.Context);
        OnPropertyChanged(nameof(FormList));
        OnPropertyChanged(nameof(HasForms));
    }

    private void RefreshAll()
    {
        if (_loading)
            return;
        OnPropertyChanged(nameof(Species));
        OnPropertyChanged(nameof(Nickname));
        OnPropertyChanged(nameof(IsNicknamed));
        OnPropertyChanged(nameof(Level));
        OnPropertyChanged(nameof(Form));
        OnPropertyChanged(nameof(Nature));
        OnPropertyChanged(nameof(AbilityIndex));
        OnPropertyChanged(nameof(HeldItem));
        OnPropertyChanged(nameof(Ball));
        OnPropertyChanged(nameof(Language));
        OnPropertyChanged(nameof(IsShiny));
        OnPropertyChanged(nameof(GenderSymbol));
        OnPropertyChanged(nameof(Move1));
        OnPropertyChanged(nameof(Move2));
        OnPropertyChanged(nameof(Move3));
        OnPropertyChanged(nameof(Move4));
        OnPropertyChanged(nameof(HasNature));
        OnPropertyChanged(nameof(HasAbility));
        OnPropertyChanged(nameof(HasBall));
        OnPropertyChanged(nameof(HasItem));
        OnPropertyChanged(nameof(HasLanguage));
        OnPropertyChanged(nameof(CanCycleGender));
        OnPropertyChanged(nameof(HasSpecies));
        RefreshDerived();
    }

    private void RefreshDerived(bool refreshInputTexts = true)
    {
        OnPropertyChanged(nameof(Sprite));
        RefreshStats(refreshInputTexts);
        RefreshLegality();
    }

    private void RefreshStats(bool refreshInputTexts)
    {
        Span<ushort> stats = stackalloc ushort[6];
        if (_pk.Species != 0)
            _pk.GetStats(_pk.PersonalInfo).AsSpan().CopyTo(stats);
        foreach (var row in StatRows)
        {
            if (refreshInputTexts)
                row.RefreshAll(stats);
            else
                row.RefreshComputed(stats);
        }

        OnPropertyChanged(nameof(ShowTotalsRow));
        OnPropertyChanged(nameof(BST));
        OnPropertyChanged(nameof(BSTBrush));
        OnPropertyChanged(nameof(IVTotal));
        OnPropertyChanged(nameof(EVTotal));
        OnPropertyChanged(nameof(EVTotalBrush));
        OnPropertyChanged(nameof(EVTotalHasColor));
        OnPropertyChanged(nameof(EVRemainingTip));
    }

    private void RefreshLegality()
    {
        if (_pk.Species == 0)
        {
            LegalityValid = true;
            LegalitySummary = string.Empty;
            LegalityReport = string.Empty;
        }
        else
        {
            var la = new LegalityAnalysis(_pk);
            LegalityValid = la.Valid;
            LegalitySummary = la.Valid ? "Legal ✓" : "Illegal ✗";
            LegalityReport = la.Report(verbose: false);
        }
        OnPropertyChanged(nameof(LegalityValid));
        OnPropertyChanged(nameof(LegalitySummary));
        OnPropertyChanged(nameof(LegalityReport));
    }
}
