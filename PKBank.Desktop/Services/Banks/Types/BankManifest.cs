using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace PKBank.Desktop.Services.Banks.Types;

/// <summary>
///     Persisted layout of a bank folder. A folder of Pokémon files has no notion of slot placement, so
///     it is kept alongside them in <c>bank.json</c>. The bank name is the folder name, not stored here.
/// </summary>
public sealed class BankManifest
{
    /// <summary>2: boxes of 30 slots (version 1 had 60).</summary>
    public const int CurrentVersion = 2;

    public int Version { get; set; } = CurrentVersion;

    public int BoxCount { get; set; } = 1;

    public List<BankSlotEntry> Slots { get; set; } = [];
}

/// <summary>Where one entity file sits. <see cref="File" /> is a name, relative to the bank folder.</summary>
public sealed class BankSlotEntry
{
    public string File { get; set; } = string.Empty;
    public int Box { get; set; }
    public int Index { get; set; }
}

/// <summary>
///     Changes waiting on a successful save. Written next to the bank and deleted whenever a save is
///     opened, so a stale diff can never be replayed against the wrong save.
/// </summary>
public sealed class BankPending
{
    public const int CurrentVersion = 2;

    public int Version { get; set; } = CurrentVersion;

    /// <summary>Save these changes were made against; informational only.</summary>
    public string SavePath { get; set; } = string.Empty;

    public int BoxCount { get; set; } = 1;

    /// <summary>The complete placement after the changes — authoritative, not a delta.</summary>
    public List<BankSlotEntry> Slots { get; set; } = [];

    /// <summary>Entities not on disk yet, base64 of the decrypted party format.</summary>
    public List<BankPendingFile> Added { get; set; } = [];

    /// <summary>Files to delete when the changes are committed.</summary>
    public List<string> Removed { get; set; } = [];
}

public sealed class BankPendingFile
{
    public string File { get; set; } = string.Empty;
    public string Data { get; set; } = string.Empty;
}

/// <summary>
///     Bank creations, renames and deletions made in the app, waiting on a successful save. Written in the
///     banks folder and deleted whenever a save is opened, like <see cref="BankPending" />.
/// </summary>
public sealed class BankLibraryPending
{
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;

    /// <summary>Names of banks to create.</summary>
    public List<string> Created { get; set; } = [];

    public List<BankLibraryRename> Renamed { get; set; } = [];

    /// <summary>Folder names of banks to delete.</summary>
    public List<string> Deleted { get; set; } = [];
}

public sealed class BankLibraryRename
{
    /// <summary>Folder name on disk.</summary>
    public string Folder { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(BankManifest))]
[JsonSerializable(typeof(BankPending))]
[JsonSerializable(typeof(BankLibraryPending))]
internal sealed partial class BankJsonContext : JsonSerializerContext;
