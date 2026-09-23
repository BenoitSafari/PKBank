using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using PKBank.Core.Moves;
using PKBank.Desktop.Sprites;
using PKBank.Desktop.Utils;
using PKHeX.Core;
using PKBank.Desktop.Components;
using PKBank.Desktop.Components.Box.ViewModels;

namespace PKBank.Desktop.Components.PokemonEditor.ViewModels;

public sealed class PokemonEditorViewModel : ViewModelBase
{
    public static readonly IReadOnlyList<string> PokerusOptions = ["None", "Infected", "Cured"];
    private readonly LegalMoveSource<ComboItem> _legalMoves = new(new LegalMoveComboSource());
    private readonly SaveFile _sav;
    private readonly FilteredGameDataSource _sources;

    // ----- Extra bytes (raw unused offsets, as in WinForms) ----------------

    private int _extraByteIndex;
    private bool _loading;

    public PokemonEditorViewModel(SaveFile sav, FilteredGameDataSource sources, SlotViewModel origin)
    {
        _sav = sav;
        _sources = sources;
        Origin = origin;
        Entity = origin.Read();
        _legalMoves.ChangeMoveSource(sources.Moves);

        StatRows =
        [
            new StatRowViewModel(this, "HP", 0),
            new StatRowViewModel(this, "Attack", 1),
            new StatRowViewModel(this, "Defense", 2),
            new StatRowViewModel(this, "Sp. Atk", 4),
            new StatRowViewModel(this, "Sp. Def", 5),
            new StatRowViewModel(this, "Speed", 3)
        ];

        MoveRows = [new(this, 0), new(this, 1), new(this, 2), new(this, 3)];

        AbilityList = [];
        FormList = [];
        Load();
    }

    internal PKM Entity { get; private set; }

    /// <summary>
    ///     Slot this editor was opened from.
    /// </summary>
    public SlotViewModel Origin { get; }

    public bool HasSpecies => Entity.Species != 0;

    public IReadOnlyList<ComboItem> SpeciesList => _sources.Species;
    public IReadOnlyList<ComboItem> ItemList => _sources.Items;
    public IReadOnlyList<MoveChoice> MoveList { get; private set; } = [];
    public IReadOnlyList<ComboItem> NatureList => _sources.Natures;
    public IReadOnlyList<ComboItem> BallList => _sources.Balls;
    public IReadOnlyList<ComboItem> LanguageList => _sources.Languages;
    public IReadOnlyList<ComboItem> AbilityList { get; private set; }
    public IReadOnlyList<string> FormList { get; private set; }
    public IReadOnlyList<StatRowViewModel> StatRows { get; }
    public IReadOnlyList<MoveRowViewModel> MoveRows { get; }

    // Capability flags for hiding fields not present in the save's generation
    public bool HasNature => Entity.Format >= 3;
    public bool HasAbility => Entity.Format >= 3;
    public bool HasBall => Entity.Format >= 3;
    public bool HasItem => ItemList.Count > 0;
    public bool HasLanguage => Entity.Format >= 3;
    public bool HasShiny => true;
    public bool HasForms => FormList.Count > 1;
    public bool CanCycleGender => Entity.Format >= 3 && Entity.PersonalInfo.IsDualGender && Entity.Species != 0;

    public Bitmap? Sprite => SpriteService.GetPokemonSprite(Entity);

    public int Species
    {
        get => Entity.Species;
        set
        {
            if (_loading || value < 0 || value == Entity.Species)
                return;
            if (Entity.Species == 0 && value > 0)
                EntityTemplates.TemplateFields(Entity, _sav);

            Entity.Species = (ushort)value;
            var pi = Entity.PersonalInfo;
            if (Entity.Form >= pi.FormCount)
                Entity.Form = 0;

            Entity.Gender = Entity.GetSaneGender();
            if (!Entity.IsNicknamed)
                Entity.ClearNickname();

            RebuildSpeciesDependentLists();
            RefreshAll();
        }
    }

    public string Nickname
    {
        get => DisplayText.Sanitize(Entity.Nickname);
        set
        {
            if (_loading || value == Entity.Nickname)
                return;
            if (string.IsNullOrWhiteSpace(value))
                Entity.ClearNickname();
            else
                Entity.SetNickname(value);
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsNicknamed));
            RefreshDerived();
        }
    }

    public bool IsNicknamed
    {
        get => Entity.IsNicknamed;
        set
        {
            if (_loading || value == Entity.IsNicknamed)
                return;
            if (value)
                Entity.IsNicknamed = true;
            else
                Entity.ClearNickname();
            OnPropertyChanged();
            OnPropertyChanged(nameof(Nickname));
            RefreshDerived();
        }
    }

    public int Level
    {
        get => Entity.CurrentLevel;
        set
        {
            var level = Math.Clamp(value, 1, 100);
            if (_loading || level == Entity.CurrentLevel)
                return;
            Entity.CurrentLevel = (byte)level;
            OnPropertyChanged();
            RefreshDerived();
        }
    }

    public int Form
    {
        get => Entity.Form;
        set
        {
            if (_loading || value < 0 || value == Entity.Form)
                return;
            Entity.Form = (byte)value;
            OnPropertyChanged(nameof(SelectedForm));
            RebuildAbilityList();
            RefreshAll();
        }
    }

    public int Nature
    {
        get => (int)Entity.Nature;
        set
        {
            if (_loading || value < 0 || value == (int)Entity.Nature)
                return;
            Entity.SetNature((Nature)value); // Gen 3/4: rerolls a PID matching the nature
            OnPropertyChanged();
            OnPropertyChanged(nameof(PIDText));
            OnPropertyChanged(nameof(GenderSymbol));
            OnPropertyChanged(nameof(AbilityIndex));
            OnPropertyChanged(nameof(SelectedAbility));
            RefreshDerived();
        }
    }

    public int AbilityIndex
    {
        get => Entity.AbilityNumber switch { 2 => 1, 4 => 2, _ => 0 };
        set
        {
            if (_loading || value < 0 || value == AbilityIndex)
                return;
            Entity.SetAbilityIndex(value);
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedAbility));
            RefreshDerived();
        }
    }

    // Ability and Form combos are bound by item, not by index: a ComboBox resets its
    // SelectedIndex to -1 while swapping ItemsSource and swallows the write-back, so an
    // index binding loses the selection whenever the list is rebuilt (e.g. on slot change).
    // Binding the item lets the control re-resolve the selection against the new list.
    // Ability entries cannot be bound by value either, since two entries can share the
    // same ability id (e.g. "Volt Absorb (1)" and "Volt Absorb (2)").
    public ComboItem? SelectedAbility
    {
        get
        {
            var list = AbilityList;
            var index = AbilityIndex;
            return (uint)index < (uint)list.Count ? list[index] : null;
        }
        set
        {
            if (_loading || value is null)
                return;
            var list = AbilityList;
            for (var i = 0; i < list.Count; i++)
            {
                if (!ReferenceEquals(list[i], value) && list[i] != value)
                    continue;
                AbilityIndex = i;
                return;
            }
        }
    }

    public string? SelectedForm
    {
        get
        {
            var list = FormList;
            var index = Form;
            return (uint)index < (uint)list.Count ? list[index] : null;
        }
        set
        {
            if (_loading || value is null)
                return;
            var list = FormList;
            for (var i = 0; i < list.Count; i++)
            {
                if (list[i] != value)
                    continue;
                Form = i;
                return;
            }
        }
    }

    public int HeldItem
    {
        get => Entity.HeldItem;
        set
        {
            if (_loading || value < 0 || value == Entity.HeldItem)
                return;
            Entity.HeldItem = value;
            OnPropertyChanged();
            RefreshDerived();
        }
    }

    public int Ball
    {
        get => Entity.Ball;
        set
        {
            if (_loading || value < 0 || value == Entity.Ball)
                return;
            Entity.Ball = (byte)value;
            OnPropertyChanged();
            RefreshDerived();
        }
    }

    public int Language
    {
        get => Entity.Language;
        set
        {
            if (_loading || value < 0 || value == Entity.Language)
                return;
            Entity.Language = value;
            OnPropertyChanged();
            RefreshDerived();
        }
    }

    public bool IsShiny
    {
        get => Entity.IsShiny;
        set
        {
            if (_loading || value == Entity.IsShiny)
                return;
            // CommonEdits.SetIsShiny keeps the PID valid: SetShiny rerolls the
            // PID/shiny relation, SetUnshiny rerolls via SetPIDGender.
            Entity.SetIsShiny(value);
            OnPropertyChanged();
            OnPropertyChanged(nameof(PIDText));
            OnPropertyChanged(nameof(GenderSymbol));
            RefreshDerived();
        }
    }

    public string GenderSymbol => Entity.Gender switch { 0 => "♂", 1 => "♀", _ => "—" };

    // ----- PID -------------------------------------------------------------

    public bool HasPID => Entity.Format >= 3;

    /// <summary>
    ///     PID as 8 hex digits. Manual edits parse leniently while typing; derived
    ///     attributes (nature/gender/ability/shiny on old formats) refresh live.
    /// </summary>
    public string PIDText
    {
        get => Entity.PID.ToString("X8");
        set
        {
            if (_loading)
                return;
            var text = value?.Trim() ?? string.Empty;
            if (!uint.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var pid) ||
                pid == Entity.PID)
                return;
            Entity.PID = pid;
            NotifyPidDerived();
            RefreshDerived(false);
        }
    }

    // ----- Egg & Pokérus ---------------------------------------------------

    public bool HasEgg => Entity.Format >= 2;

    public bool IsEgg
    {
        get => Entity.IsEgg;
        set
        {
            if (_loading || value == Entity.IsEgg)
                return;
            Entity.IsEgg = value;
            OnPropertyChanged();
            RefreshDerived(false);
        }
    }

    public bool HasPokerus => Entity.Format >= 2;

    /// <summary>0 none, 1 infected, 2 cured — mapped onto PokerusStrain/PokerusDays like WinForms.</summary>
    public int PokerusStatus
    {
        get => Entity.IsPokerusCured ? 2 : Entity.IsPokerusInfected ? 1 : 0;
        set
        {
            if (_loading || value < 0 || value == PokerusStatus)
                return;
            switch (value)
            {
                case 0:
                    Entity.PokerusStrain = 0;
                    Entity.PokerusDays = 0;
                    break;
                case 1:
                    if (Entity.PokerusStrain == 0)
                        Entity.PokerusStrain = 1;
                    if (Entity.PokerusDays == 0)
                        Entity.PokerusDays = 1;
                    break;
                default:
                    if (Entity.PokerusStrain == 0)
                        Entity.PokerusStrain = 1;
                    Entity.PokerusDays = 0;
                    break;
            }

            OnPropertyChanged();
            RefreshDerived(false);
        }
    }

    // ----- Trainer info ----------------------------------------------------

    public string OTName
    {
        get => DisplayText.Sanitize(Entity.OriginalTrainerName);
        set
        {
            var name = value ?? string.Empty;
            if (_loading || name == Entity.OriginalTrainerName)
                return;
            Entity.OriginalTrainerName = name;
            OnPropertyChanged();
            RefreshDerived(false);
        }
    }

    public string OTGenderSymbol => Entity.OriginalTrainerGender == 1 ? "♀" : "♂";

    /// <summary>Secret ID only exists from Gen 3 onward (WinForms hides the label below that).</summary>
    public bool HasSID => Entity.Generation >= 3;

    // Display values already account for the ID format: 16-bit pairs on Gen 1-6,
    // 6-digit TID / 4-digit SID on Gen 7+.
    public int MaxTID => Entity.TrainerIDDisplayFormat == TrainerIDFormat.SixDigit ? 999_999 : ushort.MaxValue;
    public int MaxSID => Entity.TrainerIDDisplayFormat == TrainerIDFormat.SixDigit ? 4294 : ushort.MaxValue;

    public int? TID
    {
        get => (int)Entity.DisplayTID;
        set
        {
            var id = (uint)Math.Clamp(value ?? 0, 0, MaxTID);
            if (_loading || id == Entity.DisplayTID)
                return;
            Entity.DisplayTID = id;
            OnPropertyChanged();
            RefreshDerived(false);
        }
    }

    public int? SID
    {
        get => (int)Entity.DisplaySID;
        set
        {
            var id = (uint)Math.Clamp(value ?? 0, 0, MaxSID);
            if (_loading || id == Entity.DisplaySID)
                return;
            Entity.DisplaySID = id;
            OnPropertyChanged();
            RefreshDerived(false);
        }
    }

    public int? Friendship
    {
        get => Entity.OriginalTrainerFriendship;
        set
        {
            var friendship = (byte)Math.Clamp(value ?? 0, 0, byte.MaxValue);
            if (_loading || friendship == Entity.OriginalTrainerFriendship)
                return;
            Entity.OriginalTrainerFriendship = friendship;
            OnPropertyChanged();
            RefreshDerived(false); // friendship feeds Return/Frustration power
        }
    }

    // ----- Origin ----------------------------------------------------------

    public bool HasOriginGame => Entity.Format >= 3;
    public bool HasMetLocation => Entity.Format >= 2;
    public bool HasFateful => Entity.Format >= 3;

    public IReadOnlyList<ComboItem> OriginGameList => _sources.Games;
    public IReadOnlyList<ComboItem> MetLocationList { get; private set; } = [];

    public int OriginGame
    {
        get => (int)Entity.Version;
        set
        {
            if (_loading || value < 0 || value == (int)Entity.Version)
                return;
            Entity.Version = (GameVersion)value;
            RebuildMetLocationList(); // location names are version-specific
            OnPropertyChanged();
            RefreshDerived(false);
        }
    }

    public int MetLocation
    {
        get => Entity.MetLocation;
        set
        {
            if (_loading || value < 0 || value == Entity.MetLocation)
                return;
            Entity.MetLocation = (ushort)value;
            OnPropertyChanged();
            RefreshDerived(false);
        }
    }

    public int? MetLevel
    {
        get => Entity.MetLevel;
        set
        {
            var level = (byte)Math.Clamp(value ?? 0, 0, 100);
            if (_loading || level == Entity.MetLevel)
                return;
            Entity.MetLevel = level;
            OnPropertyChanged();
            RefreshDerived(false);
        }
    }

    public bool FatefulEncounter
    {
        get => Entity.FatefulEncounter;
        set
        {
            if (_loading || value == Entity.FatefulEncounter)
                return;
            Entity.FatefulEncounter = value;
            OnPropertyChanged();
            RefreshDerived(false);
        }
    }

    public IReadOnlyList<string> ExtraByteOffsets { get; private set; } = [];
    public bool HasExtraBytes => ExtraByteOffsets.Count != 0;

    public int ExtraByteIndex
    {
        get => _extraByteIndex;
        set
        {
            if (value < 0 || value >= ExtraByteOffsets.Count || value == _extraByteIndex)
                return;
            _extraByteIndex = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ExtraByteValue));
        }
    }

    public int? ExtraByteValue
    {
        get
        {
            var offsets = Entity.ExtraBytes;
            if ((uint)_extraByteIndex >= (uint)offsets.Length)
                return null;
            return Entity.Data[offsets[_extraByteIndex]];
        }
        set
        {
            var offsets = Entity.ExtraBytes;
            if (_loading || (uint)_extraByteIndex >= (uint)offsets.Length)
                return;
            var b = (byte)Math.Clamp(value ?? 0, 0, byte.MaxValue);
            var offset = offsets[_extraByteIndex];
            if (Entity.Data[offset] == b)
                return;
            Entity.Data[offset] = b;
            OnPropertyChanged();
            RefreshDerived(false);
        }
    }

    public bool LegalityValid { get; private set; }
    public string LegalitySummary { get; private set; } = string.Empty;

    // Stats column totals
    public bool ShowTotalsRow => Entity.Format >= 3;
    public string BST => Entity.PersonalInfo.GetBaseStatTotal().ToString("000");
    public IBrush BSTBrush => StatColors.BaseStatTotal(Entity.PersonalInfo.GetBaseStatTotal());
    public int IVTotal => Entity.IVTotal;
    public int EVTotal => Entity.EVTotal;
    public IBrush? EVTotalBrush => StatColors.GetEVTotalBrush(Entity.EVTotal);
    public bool EVTotalHasColor => EVTotalBrush is not null;
    public string EVRemainingTip => $"Remaining: {EffortValues.Max510 - Entity.EVTotal}";

    /// <summary>
    ///     Snapshot of the entity currently being edited (with pending changes).
    /// </summary>
    public PKM GetEntityClone() => Entity.Clone();

    public void RefreshSprite() => OnPropertyChanged(nameof(Sprite));

    /// <summary>Re-displays the normalized 8-digit value (called on focus loss).</summary>
    public void NormalizePidText() => OnPropertyChanged(nameof(PIDText));

    /// <summary>
    ///     Generates a fresh valid PID like WinForms' reroll button: keeps species,
    ///     gender, nature and form consistent, never lands on an accidental shiny.
    /// </summary>
    public void RerollPid()
    {
        if (Entity.Format < 3)
            return;
        Entity.SetPIDGender(Entity.Gender);
        OnPropertyChanged(nameof(PIDText));
        NotifyPidDerived();
        RefreshDerived(false);
    }

    /// <summary>
    ///     Rebuilds a PID/IV pair the matched encounter could actually have produced.
    ///     Gen 3-5 encounters derive both from one RNG seed, so a hand-edited PID or
    ///     IV set has no seed behind it and can only be fixed by regenerating both.
    ///     Keeps the current nature and IVs when the encounter allows them, otherwise
    ///     keeps the nature alone, and picks the best IV spread found across attempts.
    /// </summary>
    public bool TryFixPidIvs(out string message)
    {
        if (Entity.Species == 0)
        {
            message = "No Pokémon to fix.";
            return false;
        }

        var la = new LegalityAnalysis(Entity);
        if (la.Valid)
        {
            message = "This Pokémon is already legal.";
            return false;
        }

        if (la.EncounterMatch is not IEncounterConvertible enc)
        {
            message = "No matching encounter to rebuild the PID from.";
            return false;
        }

        var tr = new SimpleTrainerInfo(Entity.Version)
        {
            OT = Entity.OriginalTrainerName,
            TID16 = Entity.TID16,
            SID16 = Entity.SID16,
            Gender = Entity.OriginalTrainerGender,
            Language = Entity.Language,
            Generation = Entity.Generation
        };

        var nature = Entity.Nature;
        EncounterCriteria[] tiers =
        [
            EncounterCriteria.Unrestricted with
            {
                Nature = nature,
                IV_HP = (sbyte)Entity.IV_HP, IV_ATK = (sbyte)Entity.IV_ATK, IV_DEF = (sbyte)Entity.IV_DEF,
                IV_SPA = (sbyte)Entity.IV_SPA, IV_SPD = (sbyte)Entity.IV_SPD, IV_SPE = (sbyte)Entity.IV_SPE
            },
            EncounterCriteria.Unrestricted with { Nature = nature },
            EncounterCriteria.Unrestricted
        ];

        foreach (var criteria in tiers)
        {
            if (!TryGenerateLegal(enc, tr, criteria, out var pid, out var ivs))
                continue;

            Entity.PID = pid;
            Entity.SetIVs(ivs);
            Entity.RefreshChecksum();
            Load();
            message = Entity.Nature == nature
                ? $"Rebuilt a valid PID/IV pair (nature kept, IVs {string.Join('/', ivs)})."
                : $"Rebuilt a valid PID/IV pair (nature is now {Entity.Nature}, IVs {string.Join('/', ivs)}).";
            return true;
        }

        message = "Could not rebuild a legal PID/IV pair; other fields are likely invalid too.";
        return false;
    }

    /// <summary>
    ///     Generation draws randomly whenever the criteria leave room, so sample a
    ///     few times and keep the legal candidate with the best IV total.
    /// </summary>
    private bool TryGenerateLegal(IEncounterConvertible enc, ITrainerInfo tr, EncounterCriteria criteria,
        out uint pid, out int[] ivs)
    {
        const int attempts = 256;
        pid = 0;
        ivs = [];
        var best = -1;

        var probe = Entity.Clone();
        for (var i = 0; i < attempts; i++)
        {
            var template = enc.ConvertToPKM(tr, criteria);
            int[] candidate =
                [template.IV_HP, template.IV_ATK, template.IV_DEF, template.IV_SPE, template.IV_SPA, template.IV_SPD];

            probe.PID = template.PID;
            probe.SetIVs(candidate);
            probe.RefreshChecksum();
            if (!new LegalityAnalysis(probe).Valid)
                continue;

            var total = 0;
            foreach (var iv in candidate)
                total += iv;
            if (total <= best)
                continue;
            best = total;
            pid = template.PID;
            ivs = candidate;
        }

        return best >= 0;
    }

    private void NotifyPidDerived()
    {
        OnPropertyChanged(nameof(IsShiny));
        OnPropertyChanged(nameof(GenderSymbol));
        OnPropertyChanged(nameof(Nature));
        OnPropertyChanged(nameof(AbilityIndex));
        OnPropertyChanged(nameof(SelectedAbility));
        OnPropertyChanged(nameof(SelectedForm));
    }

    public void CycleOTGender()
    {
        Entity.OriginalTrainerGender = (byte)(Entity.OriginalTrainerGender == 0 ? 1 : 0);
        OnPropertyChanged(nameof(OTGenderSymbol));
        RefreshDerived(false);
    }

    private void RebuildMetLocationList()
    {
        MetLocationList = HasMetLocation
            ? GameInfo.GetLocationList(Entity.Version, Entity.Context)
            : [];
        OnPropertyChanged(nameof(MetLocationList));
        Dispatcher.UIThread.Post(() => OnPropertyChanged(nameof(MetLocation)));
    }

    private void RebuildExtraBytes()
    {
        var offsets = Entity.ExtraBytes;
        var list = new string[offsets.Length];
        for (var i = 0; i < offsets.Length; i++)
            list[i] = $"0x{offsets[i]:X2}";
        ExtraByteOffsets = list;
        _extraByteIndex = 0;
        OnPropertyChanged(nameof(ExtraByteOffsets));
        OnPropertyChanged(nameof(HasExtraBytes));
        OnPropertyChanged(nameof(ExtraByteIndex));
        OnPropertyChanged(nameof(ExtraByteValue));
    }

    internal string GetPPDisplay(int index)
    {
        var move = Entity.GetMove(index);
        return move == 0 ? "—" : Entity.GetMovePP(move, GetPPUps(index)).ToString();
    }

    internal int GetPPUps(int index) => index switch
    {
        0 => Entity.Move1_PPUps,
        1 => Entity.Move2_PPUps,
        2 => Entity.Move3_PPUps,
        _ => Entity.Move4_PPUps
    };

    internal void SetPPUps(int index, int? value)
    {
        var ups = Math.Clamp(value ?? 0, 0, 3);
        if (_loading || ups == GetPPUps(index))
            return;
        var move = Entity.GetMove(index);
        var pp = Entity.GetMovePP(move, ups);
        switch (index)
        {
            case 0:
                Entity.Move1_PPUps = ups;
                Entity.Move1_PP = pp;
                break;
            case 1:
                Entity.Move2_PPUps = ups;
                Entity.Move2_PP = pp;
                break;
            case 2:
                Entity.Move3_PPUps = ups;
                Entity.Move3_PP = pp;
                break;
            default:
                Entity.Move4_PPUps = ups;
                Entity.Move4_PP = pp;
                break;
        }

        MoveRows[index].RefreshPPUps();
        RefreshDerived(false);
    }

    /// <summary>
    ///     Reorders the move selectors (legal moves first, WinForms-style) when the
    ///     dropdown opens and the legality state changed since the last ordering.
    /// </summary>
    public void EnsureMoveChoicesOrdered()
    {
        // Index 0 is used as the shared "is ordered" flag for the single list;
        // LegalMoveComboSource clears all flags whenever legality changes.
        if (_legalMoves.Display.GetIsMoveBoxOrdered(0))
            return;
        RebuildMoveChoices();
        _legalMoves.Display.SetIsMoveBoxOrdered(0, true);
    }

    private void RebuildMoveChoices()
    {
        var source = _legalMoves.Display.DataSource;
        var info = _legalMoves.Info;
        var judge = Entity.Species != 0; // no entity, no verdict
        var context = Entity.Context;
        var generation = Entity.Format;
        var list = new MoveChoice[source.Count];
        for (var i = 0; i < source.Count; i++)
        {
            var item = source[i];
            var move = (ushort)item.Value;
            var illegal = judge && move != 0 && !info.CanLearn(move);
            var type = move == 0 ? (byte)0 : MoveInfo.GetType(move, context);
            var category = MoveDetails.GetCategory(move, generation, context);
            list[i] = new MoveChoice(item.Text, item.Value, illegal, type, category);
        }

        MoveList = list;
        OnPropertyChanged(nameof(MoveList));
        foreach (var row in MoveRows) row.RefreshMoveList();
        // The ComboBoxes drop their selection while swapping ItemsSource; push the
        // selected values back once they have processed the new list.
        Dispatcher.UIThread.Post(() =>
        {
            foreach (var row in MoveRows) row.RefreshSelection();
        });
    }

    public void RandomizeIVs(bool max, bool clear)
    {
        Span<int> ivs = stackalloc int[6];
        if (max)
        {
            ivs.Fill(Entity.MaxIV);
            Entity.SetIVs(ivs);
        }
        else if (clear)
        {
            Entity.SetIVs(ivs);
        }
        else
        {
            var la = new LegalityAnalysis(Entity);
            var enc = la.EncounterMatch;
            if (enc is IFlawlessIVCount { FlawlessIVCount: not 0 } fc)
                Entity.SetRandomIVs(ivs, fc.FlawlessIVCount);
            else if (enc is IFixedIVSet { IVs: { IsSpecified: true } iv })
                Entity.SetRandomIVs(ivs, iv);
            else if (enc is IFlawlessIVCountConditional c && c.GetFlawlessIVCount(Entity) is { Max: not 0 } x)
                Entity.SetRandomIVs(ivs, Util.Rand.Next(x.Min, x.Max + 1));
            else
                Entity.SetRandomIVs(ivs);
        }

        RefreshDerived();
    }

    public void RandomizeEVs(bool max, bool clear)
    {
        Span<int> evs = stackalloc int[6];
        if (max)
            EffortValues.SetMax(evs, Entity);
        else if (clear)
            EffortValues.Clear(evs);
        else
            EffortValues.SetRandom(evs, Entity.Format);
        Entity.SetEVs(evs);
        RefreshDerived();
    }

    public void CycleGender()
    {
        if (!CanCycleGender)
            return;
        var gender = (byte)(Entity.Gender == 0 ? 1 : 0);
        // WinForms behavior: set the gender, then reroll a valid PID for it
        // (on Gen 3-5 the PID encodes the gender; SetPIDGender also keeps the
        // nature/form consistent and avoids accidental shinies).
        Entity.Gender = gender;
        Entity.SetPIDGender(gender);
        OnPropertyChanged(nameof(GenderSymbol));
        OnPropertyChanged(nameof(PIDText));
        OnPropertyChanged(nameof(IsShiny));
        RefreshDerived();
    }

    public void Revert()
    {
        Entity = Origin.Read();
        Load();
    }

    /// <summary>
    ///     Loads a Pokémon file straight into the editor (drop on the form), converted
    ///     to the save's format. The origin slot is untouched until Set is used.
    /// </summary>
    public bool TryLoadEntityFromFile(string path, out string message)
    {
        var pk = PkmFileService.TryLoadCompatible(path, _sav, out message);
        if (pk is null)
            return false;
        Entity = pk;
        Load();
        return true;
    }

    internal void OnStatsEdited()
    {
        if (!_loading)
            RefreshDerived(false); // keep the field being typed in untouched
    }

    public void RefreshStatInputTexts()
    {
        foreach (var row in StatRows) row.RefreshInputTexts();
    }

    internal void SetMove(int index, int value)
    {
        if (_loading || value < 0 || value == Entity.GetMove(index))
            return;
        Entity.SetMove(index, (ushort)value);
        Entity.HealPP();
        MoveRows[index].RefreshMove();
        RefreshDerived();
    }

    private void Load()
    {
        _loading = true;
        RebuildSpeciesDependentLists();
        RebuildMetLocationList();
        RebuildExtraBytes();
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
        AbilityList = Entity.Format >= 3 ? _sources.GetAbilityList(Entity.PersonalInfo) : [];
        OnPropertyChanged(nameof(AbilityList));
        OnPropertyChanged(nameof(SelectedAbility));
    }

    private void RebuildFormList()
    {
        var strings = GameInfo.Strings;
        FormList = FormConverter.GetFormList(Entity.Species, strings.types, strings.forms, GameInfo.GenderSymbolUnicode,
            Entity.Context);
        OnPropertyChanged(nameof(FormList));
        OnPropertyChanged(nameof(HasForms));
        OnPropertyChanged(nameof(SelectedForm));
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
        OnPropertyChanged(nameof(SelectedAbility));
        OnPropertyChanged(nameof(SelectedForm));
        OnPropertyChanged(nameof(HeldItem));
        OnPropertyChanged(nameof(Ball));
        OnPropertyChanged(nameof(Language));
        OnPropertyChanged(nameof(IsShiny));
        OnPropertyChanged(nameof(GenderSymbol));
        foreach (var row in MoveRows) row.RefreshAll();
        OnPropertyChanged(nameof(HasNature));
        OnPropertyChanged(nameof(HasAbility));
        OnPropertyChanged(nameof(HasBall));
        OnPropertyChanged(nameof(HasItem));
        OnPropertyChanged(nameof(HasLanguage));
        OnPropertyChanged(nameof(CanCycleGender));
        OnPropertyChanged(nameof(HasSpecies));
        OnPropertyChanged(nameof(HasPID));
        OnPropertyChanged(nameof(PIDText));
        OnPropertyChanged(nameof(HasEgg));
        OnPropertyChanged(nameof(IsEgg));
        OnPropertyChanged(nameof(HasPokerus));
        OnPropertyChanged(nameof(PokerusStatus));
        OnPropertyChanged(nameof(OTName));
        OnPropertyChanged(nameof(OTGenderSymbol));
        OnPropertyChanged(nameof(HasSID));
        OnPropertyChanged(nameof(MaxTID));
        OnPropertyChanged(nameof(MaxSID));
        OnPropertyChanged(nameof(TID));
        OnPropertyChanged(nameof(SID));
        OnPropertyChanged(nameof(Friendship));
        OnPropertyChanged(nameof(HasOriginGame));
        OnPropertyChanged(nameof(HasMetLocation));
        OnPropertyChanged(nameof(HasFateful));
        OnPropertyChanged(nameof(OriginGame));
        OnPropertyChanged(nameof(MetLocation));
        OnPropertyChanged(nameof(MetLevel));
        OnPropertyChanged(nameof(FatefulEncounter));
        OnPropertyChanged(nameof(HasExtraBytes));
        OnPropertyChanged(nameof(ExtraByteValue));
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
        if (Entity.Species != 0)
            Entity.GetStats(Entity.PersonalInfo).AsSpan().CopyTo(stats);
        foreach (var row in StatRows)
            if (refreshInputTexts)
                row.RefreshAll(stats);
            else
                row.RefreshComputed(stats);

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
        const string validLabel = "Legal ✓";
        const string invalidLabel = "Illegal ✗";

        if (Entity.Species == 0)
        {
            LegalityValid = true;
            LegalitySummary = validLabel;
        }
        else
        {
            var la = new LegalityAnalysis(Entity);
            LegalityValid = la.Valid;
            LegalitySummary = la.Valid ? validLabel : invalidLabel;
            _legalMoves.ReloadMoves(la); // clears the ordered flags when legality changed
        }

        if (MoveList.Count == 0)
            EnsureMoveChoicesOrdered(); // initial population
        OnPropertyChanged(nameof(LegalityValid));
        OnPropertyChanged(nameof(LegalitySummary));
    }
}
