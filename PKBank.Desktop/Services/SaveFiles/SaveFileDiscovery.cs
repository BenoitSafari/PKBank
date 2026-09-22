using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using PKBank.Desktop.Services.SaveFiles.Types;
using PKBank.Desktop.Utils;
using PKHeX.Core;

namespace PKBank.Desktop.Services.SaveFiles;

public static class SaveFileDiscovery
{
    public static IReadOnlyList<SaveFileSummary> Scan(IReadOnlyList<string> folders, CancellationToken token)
    {
        var found = new List<SaveFileSummary>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var folder in folders)
        {
            if (token.IsCancellationRequested)
                break;
            if (!SaveUtil.GetSavesFromFolder(folder, true, token, out var paths))
                continue;

            foreach (var path in paths)
            {
                if (token.IsCancellationRequested)
                    break;
                if (!seen.Add(NormalizePath(path)))
                    continue;
                if (TryDescribe(path) is { } summary)
                    found.Add(summary);
            }
        }

        DropConsoleBackups(found);
        found.Sort(Compare);
        return found;
    }

    /// <summary>
    ///     Switch titles keep an in-game backup beside the live save: <c>Backup.bin</c> next to
    ///     <c>SaveData.bin</c> on Brilliant Diamond/Shining Pearl, <c>backup</c> next to <c>main</c>
    ///     on Sword/Shield, Legends Arceus and Scarlet/Violet. PKHeX only skips the lower-case
    ///     extension-less spelling, so the capitalised <c>.bin</c> form reaches us and shows up as a
    ///     duplicate entry.
    /// </summary>
    /// <remarks>
    ///     A backup sitting alone in its folder is kept — it is then the only save the user has
    ///     there, and hiding it would make the folder look empty.
    /// </remarks>
    private static void DropConsoleBackups(List<SaveFileSummary> found)
    {
        var withLiveSave = new HashSet<string>(StringComparer.Ordinal);
        foreach (var summary in found)
            if (!IsConsoleBackup(summary.Path) && DirectoryOf(summary.Path) is { } directory)
                withLiveSave.Add(directory);

        found.RemoveAll(s => IsConsoleBackup(s.Path)
                             && DirectoryOf(s.Path) is { } directory
                             && withLiveSave.Contains(directory));
    }

    private static bool IsConsoleBackup(string? path) =>
        path is not null && Path.GetFileNameWithoutExtension(path.AsSpan())
            .Equals("backup", StringComparison.OrdinalIgnoreCase);

    private static string? DirectoryOf(string? path)
    {
        if (path is null)
            return null;
        try
        {
            return Path.GetDirectoryName(NormalizePath(path));
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    ///     Release/preference order within each generation. Versions absent from this
    ///     table sort after the listed ones, by their enum value.
    /// </summary>
    private static readonly GameVersion[] GameOrder =
    [
        // Gen 1 — Green[JP]/Blue[INT], Red, Blue[JP], Yellow. RB/RBY are the groupings
        // an auto-detected save reports when the exact title cannot be told apart.
        GameVersion.GN, GameVersion.RD, GameVersion.BU, GameVersion.YW, GameVersion.RB, GameVersion.RBY,
        // Gen 2 — Silver, Gold, Crystal (GS is the grouping for an undetermined Gold/Silver)
        GameVersion.SI, GameVersion.GD, GameVersion.C, GameVersion.GS, GameVersion.GSC,
        // Gen 3 — Ruby, Sapphire, Emerald, FireRed, LeafGreen
        GameVersion.R, GameVersion.S, GameVersion.E, GameVersion.FR, GameVersion.LG,
        // Gen 4 — Diamond, Pearl, Platinum, SoulSilver, HeartGold
        GameVersion.D, GameVersion.P, GameVersion.Pt, GameVersion.SS, GameVersion.HG,
        // Gen 5 — Black, White, Black 2, White 2
        GameVersion.B, GameVersion.W, GameVersion.B2, GameVersion.W2,
        // Gen 6 — X, Y, Omega Ruby, Alpha Sapphire
        GameVersion.X, GameVersion.Y, GameVersion.OR, GameVersion.AS,
        // Gen 7 — Sun, Moon, Ultra Sun, Ultra Moon
        GameVersion.SN, GameVersion.MN, GameVersion.US, GameVersion.UM
    ];

    private static readonly Dictionary<GameVersion, int> GameRanks = BuildGameRanks();

    private static Dictionary<GameVersion, int> BuildGameRanks()
    {
        var ranks = new Dictionary<GameVersion, int>(GameOrder.Length);
        for (var i = 0; i < GameOrder.Length; i++)
            ranks[GameOrder[i]] = i;
        return ranks;
    }

    /// <summary>Generation, then game, then trainer name — all ascending.</summary>
    private static int Compare(SaveFileSummary a, SaveFileSummary b)
    {
        var result = a.Generation.CompareTo(b.Generation);
        if (result != 0)
            return result;

        // Gen 1 is the one family ordered by language first (JPN, then US, then FR).
        if (a.Generation == 1)
        {
            result = LanguageRank(a.Language).CompareTo(LanguageRank(b.Language));
            if (result != 0)
                return result;
        }

        result = GameRank(a.Version).CompareTo(GameRank(b.Version));
        if (result != 0)
            return result;

        result = string.Compare(a.TrainerName, b.TrainerName, StringComparison.CurrentCultureIgnoreCase);
        return result != 0
            ? result
            : string.Compare(a.Path, b.Path, StringComparison.Ordinal);
    }

    private static int GameRank(GameVersion version) =>
        GameRanks.TryGetValue(version, out var rank) ? rank : GameOrder.Length + (int)version;

    private static int LanguageRank(int language) => language switch
    {
        (int)LanguageID.Japanese => 0,
        (int)LanguageID.English => 1,
        (int)LanguageID.French => 2,
        _ => 3 + language // the rest keeps a stable order, nothing more
    };

    private static string NormalizePath(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch
        {
            return path;
        }
    }

    private static SaveFileSummary? TryDescribe(string path)
    {
        try
        {
            if (!SaveUtil.TryGetSaveFile(path, out var sav))
                return null;

            return new SaveFileSummary(
                path,
                sav.Generation,
                sav.Version,
                sav.Language,
                GameInfo.GetVersionName(sav.Version),
                DisplayText.Sanitize(sav.OT),
                sav.DisplayTID,
                sav.Generation > 1 ? sav.Gender : null,
                ReadPlayTime(sav),
                sav is IMultiplayerSprite ms ? ms.MultiplayerSpriteID : null);
        }
        catch
        {
            return null; // unreadable or corrupt
        }
    }

    private static string ReadPlayTime(SaveFile sav)
    {
        try
        {
            return sav.PlayTimeString;
        }
        catch
        {
            return "–";
        }
    }
}
