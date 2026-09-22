using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace PKBank.Desktop.Services.Banks.Types;

/// <summary>
///     Persisted layout of a bank folder. A folder of Pokémon files has no notion of slot placement, so
///     it is kept alongside them in <c>bank.json</c>.
/// </summary>
public sealed class BankManifest
{
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;

    /// <summary>Display name; empty means "use the folder name".</summary>
    public string Name { get; set; } = string.Empty;

    public int PageCount { get; set; } = 1;

    public List<BankSlotEntry> Slots { get; set; } = [];
}

/// <summary>Where one entity file sits. <see cref="File" /> is a name, relative to the bank folder.</summary>
public sealed class BankSlotEntry
{
    public string File { get; set; } = string.Empty;
    public int Page { get; set; }
    public int Index { get; set; }
}

/// <summary>
///     Changes waiting on a successful save. Written next to the bank and deleted whenever a save is
///     opened, so a stale diff can never be replayed against the wrong save.
/// </summary>
public sealed class BankPending
{
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;

    /// <summary>Save these changes were made against; informational only.</summary>
    public string SavePath { get; set; } = string.Empty;

    public int PageCount { get; set; } = 1;

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

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(BankManifest))]
[JsonSerializable(typeof(BankPending))]
internal sealed partial class BankJsonContext : JsonSerializerContext;
