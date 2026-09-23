using System;
using System.Collections.Generic;
using PKHeX.Core;

namespace PKBank.Desktop.Components.Roamer;

/// <summary>
///     The values a roamer slot holds that legality can speak about, shared by the
///     Gen 3 and Gen 4 editors.
/// </summary>
public sealed class RoamerState
{
    public ushort Species { get; set; }
    public uint PID { get; set; }
    public uint IV32 { get; set; }
    public byte Level { get; set; }
    public ushort HpCurrent { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>
///     Packs and unpacks the roamer structures' IV32 field, whose stat order (HP, Atk,
///     Def, Spe, SpA, SpD) differs from the order the editors display.
/// </summary>
public static class RoamerIVs
{
    public static uint Pack(int hp, int atk, int def, int spa, int spd, int spe)
        => (uint)((hp & 31) | ((atk & 31) << 5) | ((def & 31) << 10)
                  | ((spe & 31) << 15) | ((spa & 31) << 20) | ((spd & 31) << 25));

    public static int HP(uint iv32) => (int)(iv32 & 31);
    public static int ATK(uint iv32) => (int)((iv32 >> 5) & 31);
    public static int DEF(uint iv32) => (int)((iv32 >> 10) & 31);
    public static int SPE(uint iv32) => (int)((iv32 >> 15) & 31);
    public static int SPA(uint iv32) => (int)((iv32 >> 20) & 31);
    public static int SPD(uint iv32) => (int)((iv32 >> 25) & 31);
}

/// <summary>Verdict for one roamer slot: the badge label plus the lines explaining it.</summary>
public sealed record RoamerVerdict(bool Valid, string Summary, string Detail)
{
    public bool HasDetail => Detail.Length != 0;
}

/// <summary>
///     Legality rules for a single roamer slot. A roamer is not a <see cref="PKM"/>, so
///     <see cref="LegalityAnalysis"/> cannot judge one; the save only keeps what the game
///     rolled when the roamer spawned, and that is exactly what is checked here.
/// </summary>
public sealed class RoamerLegality
{
    /// <summary>The roamer structures only use 30 of the 32 IV bits.</summary>
    private const uint IVMask = 0x3FFF_FFFF;

    /// <summary>A candidate PID that keeps both the shininess and the nature.</summary>
    private const int MaxScore = 3;

    private readonly SaveFile _sav;
    private readonly ushort[] _species;
    private readonly byte _level;
    private readonly bool _truncatesIVs;

    private RoamerLegality(SaveFile sav, ushort[] species, byte level, bool truncatesIVs)
    {
        _sav = sav;
        _species = species;
        _level = level;
        _truncatesIVs = truncatesIVs;
    }

    /// <summary>Rules for the single Gen 3 roamer slot, which species depends on the version.</summary>
    public static RoamerLegality For(SAV3 sav)
    {
        (ushort[] Species, byte Level) rules = sav.Version switch
        {
            GameVersion.R => ([(ushort)Species.Latios], 40),
            GameVersion.S => ([(ushort)Species.Latias], 40),
            GameVersion.FR or GameVersion.LG or GameVersion.FRLG =>
                ([(ushort)Species.Raikou, (ushort)Species.Entei, (ushort)Species.Suicune], 50),
            // Emerald roams both, and so does an R/S save whose version is still ambiguous.
            _ => ([(ushort)Species.Latias, (ushort)Species.Latios], 40),
        };
        // Only Emerald stores and reads back the full 32 bits of IVs.
        return new RoamerLegality(sav, rules.Species, rules.Level, sav is not SAV3E);
    }

    /// <summary>Rules for one Gen 4 slot, whose species and level are fixed by the slot itself.</summary>
    public static RoamerLegality For(SAV4 sav, ushort species, byte level)
        => new(sav, [species], level, false);

    public RoamerVerdict Check(RoamerState state)
    {
        if (IsNeverSpawned(state))
            return new RoamerVerdict(true, "Empty slot", "This roamer has never spawned.");

        var issues = new List<string>();

        if (Array.IndexOf(_species, state.Species) < 0)
            issues.Add($"{GetSpeciesName(state.Species)} does not roam in {_sav.Version}.");

        if (state.Level != _level)
            issues.Add($"Level should be {_level}; a roamer keeps the level it spawned at.");

        if (!IsMethod1(state))
            issues.Add("PID and IVs do not share an RNG seed (roamers are always Method 1).");

        if (TryGetMaxHP(state, out var max))
        {
            if (state.HpCurrent > max)
                issues.Add($"Current HP {state.HpCurrent} is above the maximum of {max}.");
            else if (state.IsActive && state.HpCurrent == 0)
                issues.Add("An active roamer cannot be at 0 HP.");
        }

        var note = GetTruncationNote(state);
        if (issues.Count == 0)
            return new RoamerVerdict(true, "Legal ✓", note);

        if (note.Length != 0)
            issues.Add(note);
        return new RoamerVerdict(false, "Illegal ✗", string.Join('\n', issues));
    }

    /// <summary>
    ///     Rebuilds the slot into a state the game could have produced, changing as little
    ///     as possible. The IVs are kept and the PID recomputed from their seed; when that
    ///     spread has no seed, or when rebuilding the PID would cost a shiny, the PID is
    ///     kept instead and the best IV spread its seeds allow is used. Only when neither
    ///     side can be reversed is a fresh pair rolled.
    /// </summary>
    public bool TryFix(RoamerState state, out string message)
    {
        if (Check(state).Valid)
        {
            message = "This roamer is already legal.";
            return false;
        }

        var changes = new List<string>();

        if (Array.IndexOf(_species, state.Species) < 0)
        {
            state.Species = _species[0];
            changes.Add($"species set to {GetSpeciesName(state.Species)}");
        }

        if (state.Level != _level)
        {
            state.Level = _level;
            changes.Add($"level set to {_level}");
        }

        if (!IsMethod1(state))
            FixPidIVs(state, changes);

        // Last, because the maximum depends on the species, level and IVs settled above.
        if (TryGetMaxHP(state, out var max) && (state.HpCurrent > max || (state.IsActive && state.HpCurrent == 0)))
        {
            state.HpCurrent = (ushort)max;
            changes.Add($"current HP set to {max}");
        }

        if (changes.Count == 0)
        {
            message = "Nothing could be fixed automatically.";
            return false;
        }

        message = $"Fixed: {string.Join(", ", changes)}.";
        return true;
    }

    private void FixPidIVs(RoamerState state, List<string> changes)
    {
        var ivs = state.IV32 & IVMask;
        var hasPid = TryRebuildPid(ivs, state.PID, out var pid);

        void KeepIVs()
        {
            state.PID = pid;
            state.IV32 = ivs;
            changes.Add("PID rebuilt (IVs kept)");
        }

        // Best case: the spread has a seed, and the PID it implies keeps the shininess.
        if (hasPid && IsShiny(pid) == IsShiny(state.PID))
        {
            KeepIVs();
            return;
        }

        // Rebuilding the PID would cost a shiny; keeping it instead preserves both the
        // shininess and the nature, at the price of the best spread its seeds allow.
        if (TryRebuildIVs(state.PID, out var iv32))
        {
            state.IV32 = iv32;
            changes.Add($"IVs rebuilt to {Describe(iv32)} (PID kept)");
            return;
        }

        // Neither side can be reversed as-is. Only a shiny is worth rerolling both for.
        if (hasPid && !IsShiny(state.PID))
        {
            KeepIVs();
            return;
        }

        RollFresh(state, IsShiny(state.PID));
        changes.Add($"PID and IVs rerolled, IVs are now {Describe(state.IV32)}");
    }

    /// <summary>
    ///     Applies a Method 1 frame of the requested shininess, keeping the nature when one
    ///     turns up. A seed ties the PID to the IVs, so both are replaced together.
    /// </summary>
    public string SetShiny(RoamerState state, bool shiny)
    {
        RollFresh(state, shiny);
        // The new spread moves the HP ceiling, so keep the staged value under it.
        if (TryGetMaxHP(state, out var max) && state.HpCurrent > max)
            state.HpCurrent = (ushort)max;
        return $"{(shiny ? "Shiny" : "Non-shiny")} frame applied, IVs are now {Describe(state.IV32)}.";
    }

    /// <summary>
    ///     Finds the PID a Method 1 seed would have produced alongside these exact IVs.
    ///     Several seeds can yield one spread, so prefer the candidate that keeps the
    ///     current shininess, then the current nature.
    /// </summary>
    private bool TryRebuildPid(uint iv32, uint current, out uint pid)
    {
        Span<uint> seeds = stackalloc uint[LCRNG.MaxCountSeedsIV];
        var count = LCRNGReversal.GetSeedsIVs(seeds, (iv32 & 0x7FFF) << 16, (iv32 >> 15) << 16);

        pid = 0;
        var best = -1;
        foreach (var seed in seeds[..count])
        {
            var candidate = ClassicEraRNG.GetSequentialPID(LCRNG.Prev2(seed));
            if (!MethodFinder.GetLCRNGMethod1Match(candidate, iv32, out _))
                continue;
            var score = Score(candidate, current);
            if (score <= best)
                continue;
            best = score;
            pid = candidate;
        }

        return best >= 0;
    }

    /// <summary>
    ///     Keeps the PID (and with it the nature and shininess) and takes the best IV
    ///     spread among the seeds that PID can come from.
    /// </summary>
    private static bool TryRebuildIVs(uint pid, out uint iv32)
    {
        Span<uint> seeds = stackalloc uint[LCRNG.MaxCountSeedsIV];
        var count = LCRNGReversal.GetSeeds(seeds, pid << 16, pid & 0xFFFF_0000);

        iv32 = 0;
        var best = -1;
        foreach (var seed in seeds[..count])
        {
            var s = LCRNG.Next2(seed);
            var candidate = ClassicEraRNG.GetSequentialIVs(ref s);
            var total = GetIVTotal(candidate);
            if (total <= best)
                continue;
            best = total;
            iv32 = candidate;
        }

        return best >= 0;
    }

    /// <summary>
    ///     Walks fresh Method 1 frames and keeps the best one with the wanted shininess,
    ///     preferring the current nature. Used when nothing can be reversed to a seed, and
    ///     when the shiny flag is toggled by hand.
    /// </summary>
    private void RollFresh(RoamerState state, bool wantShiny)
    {
        const int attempts = 1 << 21;
        const int goodEnough = 160;

        var current = state.PID;
        var bestScore = -1;
        var bestTotal = -1;
        var seed = Util.Rand32();

        for (var i = 0; i < attempts; i++, seed = LCRNG.Next(seed))
        {
            var s = seed;
            var pid = ClassicEraRNG.GetSequentialPID(ref s);
            var ivs = ClassicEraRNG.GetSequentialIVs(ref s);

            var score = Score(pid, current, wantShiny);
            var total = GetIVTotal(ivs);
            if (score < bestScore || (score == bestScore && total <= bestTotal))
                continue;

            bestScore = score;
            bestTotal = total;
            state.PID = pid;
            state.IV32 = ivs;
            if (bestScore == MaxScore && bestTotal >= goodEnough)
                return;
        }
    }

    /// <summary>Shininess weighs more than nature when picking between candidate PIDs.</summary>
    private int Score(uint candidate, uint current) => Score(candidate, current, IsShiny(current));

    private int Score(uint candidate, uint current, bool wantShiny)
        => (IsShiny(candidate) == wantShiny ? 2 : 0) + (candidate % 25 == current % 25 ? 1 : 0);

    private bool IsShiny(uint pid) => Roamer3.IsShiny(pid, _sav);

    private bool IsMethod1(RoamerState state)
        => MethodFinder.GetLCRNGMethod1Match(state.PID, state.IV32 & IVMask, out _);

    /// <summary>A zeroed slot is what a save holds before the roamer has ever spawned.</summary>
    private static bool IsNeverSpawned(RoamerState state)
        => state is { Species: 0, PID: 0, IV32: 0, Level: 0, HpCurrent: 0, IsActive: false };

    /// <summary>
    ///     Roamers never battle-train, so their maximum HP only depends on base stats,
    ///     IVs and level.
    /// </summary>
    private bool TryGetMaxHP(RoamerState state, out int max)
    {
        max = 0;
        if (!_sav.Personal.IsSpeciesInGame(state.Species))
            return false;

        var baseHP = _sav.Personal[state.Species].HP;
        if (baseHP == 1) // Shedinja, never a roamer, but the formula differs
            max = 1;
        else
            max = ((RoamerIVs.HP(state.IV32) + (2 * baseHP) + 100) * state.Level / 100) + 10;
        return true;
    }

    /// <summary>
    ///     R/S and FR/LG only copy the low IV byte into the battle encounter, so the roamer
    ///     you actually meet has a different spread than the one stored here.
    /// </summary>
    private string GetTruncationNote(RoamerState state)
    {
        var ivs = state.IV32 & IVMask;
        if (!_truncatesIVs || ivs <= 0xFF)
            return string.Empty;
        return $"Note: R/S and FR/LG only load the low IV byte when the roamer is battled, " +
               $"so in-game its IVs will be {Describe(ivs & 0xFF)}.";
    }

    private static string GetSpeciesName(ushort species)
    {
        var names = GameInfo.Strings.specieslist;
        return species < names.Length ? names[species] : $"#{species}";
    }

    /// <summary>Formats a packed IV32 in the editor's display order (HP/Atk/Def/SpA/SpD/Spe).</summary>
    private static string Describe(uint iv32)
        => $"{RoamerIVs.HP(iv32)}/{RoamerIVs.ATK(iv32)}/{RoamerIVs.DEF(iv32)}/" +
           $"{RoamerIVs.SPA(iv32)}/{RoamerIVs.SPD(iv32)}/{RoamerIVs.SPE(iv32)}";

    private static int GetIVTotal(uint iv32)
    {
        var total = 0;
        for (var i = 0; i < 6; i++)
            total += (int)((iv32 >> (5 * i)) & 31);
        return total;
    }
}
