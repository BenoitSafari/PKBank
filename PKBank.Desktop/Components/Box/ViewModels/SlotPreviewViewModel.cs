using System.Collections.Generic;
using Avalonia.Media.Imaging;
using PKBank.Desktop.Sprites;
using PKHeX.Core;

namespace PKBank.Desktop.Components.Box.ViewModels;

/// <summary>
/// Rich hover preview for a slot, mirroring the info shown by Windows PKHeX
/// (ball + name header, ability/level/IVs/nature, legality and encounter details).
/// Built lazily when the tooltip opens.
/// </summary>
public sealed class SlotPreviewViewModel
{
    public required Bitmap? BallSprite { get; init; }
    public required string Title { get; init; }
    public required IReadOnlyList<string> Lines { get; init; }
    public required bool IsValid { get; init; }
    public required string LegalityLine { get; init; }
    public required IReadOnlyList<string> DetailLines { get; init; }

    public bool HasLegalityLine => LegalityLine.Length != 0;

    /// <summary>Same verdict badge as the editor header.</summary>
    public string LegalitySummary => IsValid ? "Legal ✓" : "Illegal ✗";

    public static SlotPreviewViewModel? TryCreate(PKM pk)
    {
        if (pk.Species == 0)
            return null;

        var strings = GameInfo.Strings;
        var species = (uint)pk.Species < strings.specieslist.Length ? strings.specieslist[pk.Species] : $"#{pk.Species}";
        var title = pk.IsNicknamed ? $"{species} ({pk.Nickname})" : species;
        if (pk.IsEgg)
            title += " 🥚";

        var lines = new List<string>(5);
        if (pk.Format >= 3)
        {
            var ability = (uint)pk.Ability < strings.abilitylist.Length ? strings.abilitylist[pk.Ability] : "?";
            lines.Add($"Ability: {ability}");
        }
        lines.Add($"Level: {pk.CurrentLevel}");
        lines.Add($"IVs: {pk.IV_HP}/{pk.IV_ATK}/{pk.IV_DEF}/{pk.IV_SPA}/{pk.IV_SPD}/{pk.IV_SPE}");
        var evTotal = pk.EV_HP + pk.EV_ATK + pk.EV_DEF + pk.EV_SPA + pk.EV_SPD + pk.EV_SPE;
        if (evTotal != 0)
            lines.Add($"EVs: {pk.EV_HP}/{pk.EV_ATK}/{pk.EV_DEF}/{pk.EV_SPA}/{pk.EV_SPD}/{pk.EV_SPE}");
        if (pk.Format >= 3)
            lines.Add($"Nature: {strings.natures[(int)pk.Nature]}");

        bool valid;
        string legalityLine;
        var details = new List<string>(2);
        try
        {
            var la = new LegalityAnalysis(pk);
            valid = la.Valid;
            legalityLine = valid ? string.Empty : GetFirstLine(la.Report(verbose: false));
            details.Add($"Encounter: {la.EncounterOriginal.LongName}");
            var pidType = la.Info.PIDIV.Type;
            if (pidType != PIDType.None)
                details.Add($"PID type: {pidType}");
        }
        catch
        {
            valid = true;
            legalityLine = string.Empty;
        }

        return new SlotPreviewViewModel
        {
            BallSprite = pk.Format >= 3 ? SpriteService.GetBallSprite(pk.Ball) : null,
            Title = title,
            Lines = lines,
            IsValid = valid,
            LegalityLine = legalityLine,
            DetailLines = details,
        };
    }

    private static string GetFirstLine(string report)
    {
        var index = report.IndexOf('\n');
        return index < 0 ? report : report[..index].TrimEnd('\r');
    }
}
