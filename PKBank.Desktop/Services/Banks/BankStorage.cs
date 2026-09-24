using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using PKBank.Desktop.Services.Banks.Types;
using PKHeX.Core;

namespace PKBank.Desktop.Services.Banks;

/// <summary>
///     Reads and writes the files that make a folder of Pokémon behave like a bank: the manifest holding
///     the slot layout, and the pending-changes file.
/// </summary>
public static class BankStorage
{
    public const string ManifestFileName = "bank.json";
    public const string PendingFileName = "bank.pending.json";
    public const int SlotsPerBox = 60;

    /// <summary>Entity files, top level only: sub-folders are other banks' business.</summary>
    public static IReadOnlyList<string> ScanEntityFiles(string folder)
    {
        var extensions = new HashSet<string>(
            EntityFileExtension.GetExtensions().Select(static e => $".{e}"), StringComparer.OrdinalIgnoreCase);

        var found = new List<string>();
        foreach (var path in Directory.EnumerateFiles(folder, "*", SearchOption.TopDirectoryOnly))
        {
            var info = new FileInfo(path);
            if (extensions.Contains(info.Extension) && EntityDetection.IsSizePlausible(info.Length))
                found.Add(info.Name);
        }

        found.Sort(StringComparer.Ordinal);
        return found;
    }

    /// <summary>
    ///     Loads the manifest and reconciles it with what is actually on disk.
    ///     A manifest that merely drifted (a file dropped in by hand, one deleted) is <em>repaired</em>:
    ///     orphans are dropped and strays adopted into the first free slots, so the user's arrangement
    ///     survives. A structurally broken one — unparseable, duplicate coordinates, out of bounds, or a
    ///     BoxCount that cannot hold its own entries — is rebuilt from scratch, exactly as when the bank
    ///     is first seen.
    /// </summary>
    public static BankManifest LoadOrRebuild(string folder, out bool changed)
    {
        var files = ScanEntityFiles(folder);
        var manifest = TryReadManifest(folder);

        if (manifest is null || !IsStructurallyValid(manifest))
        {
            changed = true;
            return Rebuild(files);
        }

        changed = Repair(manifest, files);
        return manifest;
    }

    public static void SaveManifest(string folder, BankManifest manifest) =>
        WriteAtomic(Path.Combine(folder, ManifestFileName),
            JsonSerializer.Serialize(manifest, BankJsonContext.Default.BankManifest));

    public static BankPending? LoadPending(string folder)
    {
        try
        {
            var path = Path.Combine(folder, PendingFileName);
            if (!File.Exists(path))
                return null;
            var pending = JsonSerializer.Deserialize(File.ReadAllText(path), BankJsonContext.Default.BankPending);
            return pending?.Version == BankPending.CurrentVersion ? pending : null;
        }
        catch
        {
            return null;
        }
    }

    public static void SavePending(string folder, BankPending pending)
    {
        try
        {
            WriteAtomic(Path.Combine(folder, PendingFileName),
                JsonSerializer.Serialize(pending, BankJsonContext.Default.BankPending));
        }
        catch
        {
            // Pending changes live in memory as well; failing to mirror them is not fatal.
        }
    }

    public static void DiscardPending(string folder)
    {
        try
        {
            File.Delete(Path.Combine(folder, PendingFileName));
        }
        catch
        {
            // Nothing to discard, or the folder is read-only.
        }
    }

    public static string DefaultName(string folder) =>
        Path.GetFileName(folder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

    private static BankManifest? TryReadManifest(string folder)
    {
        try
        {
            var path = Path.Combine(folder, ManifestFileName);
            if (!File.Exists(path))
                return null;
            var manifest = JsonSerializer.Deserialize(File.ReadAllText(path), BankJsonContext.Default.BankManifest);
            return manifest?.Version == BankManifest.CurrentVersion ? manifest : null;
        }
        catch
        {
            return null;
        }
    }

    private static bool IsStructurallyValid(BankManifest manifest)
    {
        if (manifest.BoxCount < 1)
            return false;

        var coordinates = new HashSet<(int Box, int Index)>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in manifest.Slots)
        {
            if (entry.Index is < 0 or >= SlotsPerBox || entry.Box < 0 || entry.Box >= manifest.BoxCount)
                return false;
            if (!coordinates.Add((entry.Box, entry.Index)) || !names.Add(entry.File))
                return false;
        }

        return true;
    }

    /// <summary>Drops entries whose file is gone and adopts files nobody claims. True when anything moved.</summary>
    private static bool Repair(BankManifest manifest, IReadOnlyList<string> files)
    {
        var present = new HashSet<string>(files, StringComparer.Ordinal);
        var removed = manifest.Slots.RemoveAll(entry => !present.Contains(entry.File));

        var claimed = new HashSet<string>(manifest.Slots.Select(static e => e.File), StringComparer.Ordinal);
        var strays = files.Where(f => !claimed.Contains(f)).ToList();
        if (strays.Count == 0)
            return removed > 0;

        var taken = new HashSet<int>(manifest.Slots.Select(static e => (e.Box * SlotsPerBox) + e.Index));
        var position = 0;
        foreach (var stray in strays)
        {
            while (taken.Contains(position))
                position++;
            taken.Add(position);
            manifest.Slots.Add(new BankSlotEntry
            {
                File = stray,
                Box = position / SlotsPerBox,
                Index = position % SlotsPerBox
            });
        }

        manifest.BoxCount = Math.Max(manifest.BoxCount, (taken.Max() / SlotsPerBox) + 1);
        return true;
    }

    private static BankManifest Rebuild(IReadOnlyList<string> files)
    {
        var manifest = new BankManifest
        {
            BoxCount = Math.Max(1, (files.Count + SlotsPerBox - 1) / SlotsPerBox)
        };
        for (var i = 0; i < files.Count; i++)
            manifest.Slots.Add(new BankSlotEntry
            {
                File = files[i],
                Box = i / SlotsPerBox,
                Index = i % SlotsPerBox
            });
        return manifest;
    }

    /// <summary>Same-directory temp file plus rename: the swap is atomic on one filesystem.</summary>
    private static void WriteAtomic(string path, string content)
    {
        var temp = path + ".tmp";
        File.WriteAllText(temp, content);
        File.Move(temp, path, true);
    }
}
