using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using PKBank.Desktop.Services.Banks.Types;
using PKBank.Desktop.Utils;
using PKHeX.Core;

namespace PKBank.Desktop.Services.Banks;

/// <summary>
///     One open bank: the slot layout in memory, the entities of the boxes that have been looked at, and
///     the changes waiting on a successful save. Nothing reaches the folder until <see cref="Commit" />.
/// </summary>
public sealed class BankSession
{
    /// <summary>Entities staged for a file that does not exist yet.</summary>
    private readonly Dictionary<string, byte[]> _added = new(StringComparer.Ordinal);

    /// <summary>Decoded entities by file name; only boxes that were shown are in here.</summary>
    private readonly Dictionary<string, PKM> _entities = new(StringComparer.Ordinal);

    private readonly HashSet<int> _loadedBoxes = [];

    /// <summary>Entity files present in the folder when the bank was opened.</summary>
    private readonly HashSet<string> _onDisk;

    private readonly Dictionary<int, string> _placement = [];
    private bool _changed;

    private BankSession(string folder, BankManifest manifest, IEnumerable<string> onDisk)
    {
        Folder = folder;
        BoxCount = manifest.BoxCount;
        _onDisk = new HashSet<string>(onDisk, StringComparer.Ordinal);
        foreach (var entry in manifest.Slots)
            _placement[(entry.Box * BankStorage.SlotsPerBox) + entry.Index] = entry.File;
    }

    public string Folder { get; private set; }

    /// <summary>The folder name: renaming the folder is renaming the bank.</summary>
    public string Name => BankStorage.DefaultName(Folder);
    public int BoxCount { get; private set; }

    public bool IsDirty => _changed;

    /// <summary>A bank that exists only in the app so far; its folder is created when the save is written.</summary>
    public static BankSession CreateNew(string plannedFolder) => new(plannedFolder, new BankManifest(), []);

    /// <summary>Opens a bank, rebuilding or repairing its manifest to match the folder.</summary>
    public static BankSession Open(string folder)
    {
        var manifest = BankStorage.LoadOrRebuild(folder, out var changed);
        if (changed)
            BankStorage.SaveManifest(folder, manifest);
        return new BankSession(folder, manifest, BankStorage.ScanEntityFiles(folder));
    }

    /// <summary>
    ///     The folder was renamed or moved. Files are addressed relative to it, so nothing else changes;
    ///     the pending-changes file moved along with it.
    /// </summary>
    public void Relocate(string folder) => Folder = folder;

    /// <summary>Decodes the entities of one box. Safe to call off the UI thread; a no-op once loaded.</summary>
    public void LoadBox(int box, CancellationToken token)
    {
        if (!_loadedBoxes.Add(box))
            return;

        var first = box * BankStorage.SlotsPerBox;
        for (var index = 0; index < BankStorage.SlotsPerBox; index++)
        {
            token.ThrowIfCancellationRequested();
            if (_placement.TryGetValue(first + index, out var file) && !_entities.ContainsKey(file)
                                                                   && Decode(file) is { } pk)
                _entities[file] = pk;
        }
    }

    /// <summary>
    ///     The entity at that slot, or null when empty or not decoded yet. Never touches the disk: the
    ///     tooltip binding reads this on every hover.
    /// </summary>
    public PKM? Peek(int box, int index) =>
        _placement.TryGetValue((box * BankStorage.SlotsPerBox) + index, out var file)
        && _entities.TryGetValue(file, out var pk)
            ? pk
            : null;

    /// <summary>Whether a slot holds an entity, decoded or not.</summary>
    public bool IsOccupied(int box, int index) => _placement.ContainsKey((box * BankStorage.SlotsPerBox) + index);

    /// <summary>Puts an entity into a slot, or clears it with a blank one.</summary>
    public void Place(int box, int index, PKM? pk)
    {
        var position = (box * BankStorage.SlotsPerBox) + index;
        _placement.Remove(position);

        if (pk is { Species: > 0 })
        {
            _placement[position] = Stage(pk);
            if (box >= BoxCount)
                BoxCount = box + 1; // the spare box became real
        }

        _changed = true;
    }

    public BankPending BuildPending(string savePath) => new()
    {
        SavePath = savePath,
        BoxCount = BoxCount,
        Slots = [.. BuildEntries()],
        Added = [.. PendingAdds().Select(static a => new BankPendingFile
        {
            File = a.Key,
            Data = Convert.ToBase64String(a.Value)
        })],
        Removed = [.. PendingRemovals()]
    };

    /// <summary>
    ///     Writes the pending changes to the folder. Entity files first, then the manifest — the manifest
    ///     rename is the commit point. A crash in between leaves unreferenced files, which the next open
    ///     adopts back into free slots.
    /// </summary>
    public string Commit()
    {
        if (!_changed)
            return string.Empty;

        try
        {
            var added = PendingAdds().ToList();
            var removed = PendingRemovals().ToList();

            foreach (var (file, data) in added)
                File.WriteAllBytes(Path.Combine(Folder, file), data);

            foreach (var file in removed)
                try
                {
                    File.Delete(Path.Combine(Folder, file));
                }
                catch (FileNotFoundException)
                {
                    // Already gone: the commit is meant to be repeatable.
                }

            // Empty boxes at the end go; an empty box before a used one stays, keeping the layout.
            BoxCount = _placement.Count == 0 ? 1 : (_placement.Keys.Max() / BankStorage.SlotsPerBox) + 1;

            BankStorage.SaveManifest(Folder, new BankManifest
            {
                BoxCount = BoxCount,
                Slots = [.. BuildEntries()]
            });
            BankStorage.DiscardPending(Folder);

            foreach (var (file, _) in added)
            {
                _added.Remove(file);
                _onDisk.Add(file);
            }

            foreach (var file in removed)
                _onDisk.Remove(file);
            _changed = false;

            return added.Count + removed.Count > 0
                ? $" Bank “{Name}” updated."
                : $" Bank “{Name}” rearranged.";
        }
        catch (Exception ex)
        {
            // Never throw: the save itself is already written by the time we get here.
            return $" Bank “{Name}” could not be updated: {ex.Message}";
        }
    }

    /// <summary>
    ///     Staged entities that a slot still points at. Anything staged then moved away again is simply
    ///     never written, so no ordering of moves can leave a stray file behind.
    /// </summary>
    private IEnumerable<KeyValuePair<string, byte[]>> PendingAdds()
    {
        var referenced = Referenced();
        return _added.Where(a => referenced.Contains(a.Key));
    }

    /// <summary>Files on disk that no slot points at any more.</summary>
    private IEnumerable<string> PendingRemovals()
    {
        var referenced = Referenced();
        return _onDisk.Where(f => !referenced.Contains(f));
    }

    private HashSet<string> Referenced() => new(_placement.Values, StringComparer.Ordinal);

    private IEnumerable<BankSlotEntry> BuildEntries() =>
        _placement.OrderBy(static p => p.Key).Select(static p => new BankSlotEntry
        {
            File = p.Value,
            Box = p.Key / BankStorage.SlotsPerBox,
            Index = p.Key % BankStorage.SlotsPerBox
        });

    private PKM? Decode(string file)
    {
        try
        {
            var path = Path.Combine(Folder, file);
            var data = File.ReadAllBytes(path);
            // No save passed on purpose: a bank is heterogeneous, so the extension decides the context
            // rather than whichever save happens to be loaded.
            return FileUtil.TryGetPKM(data, out var pk, Path.GetExtension(path)) ? pk : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    ///     The file name an entity should live under. An entity already known to this bank keeps its file,
    ///     so rearranging slots only rewrites the manifest.
    /// </summary>
    private string Stage(PKM pk)
    {
        foreach (var (file, known) in _entities)
            if (ReferenceEquals(known, pk))
                return file;

        var name = PathUtil.CleanFileName(pk.FileName);
        var stem = Path.GetFileNameWithoutExtension(name);
        var extension = Path.GetExtension(name);
        var candidate = name;
        for (var n = 2; IsTaken(candidate); n++)
            candidate = $"{stem} ({n}){extension}";

        _added[candidate] = Encode(pk);
        _entities[candidate] = pk;
        return candidate;
    }

    private bool IsTaken(string file) => _added.ContainsKey(file) || _onDisk.Contains(file);

    private static byte[] Encode(PKM pk)
    {
        pk.ForcePartyData();
        var buffer = new byte[pk.SIZE_PARTY];
        pk.WriteDecryptedDataParty(buffer);
        return buffer;
    }
}
