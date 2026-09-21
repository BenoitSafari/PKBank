using System;
using System.Collections.Generic;
using PKBank.Core.Events.Files.Gen3Events;
using PKHeX.Core;

namespace PKBank.Core.Events.Files;

/// <summary>
///     Describes one kind of Gen 3 event data file (WC3/ME3/WN3/ECT/ECB).
/// </summary>
public sealed record Gen3EventFileKind(
    string Title,
    string Extension,
    Func<SAV3, bool> Has,
    Func<SAV3, string> GetSummary,
    Func<SAV3, IReadOnlyList<int>> GetValidSizes,
    Action<SAV3, byte[]> Import,
    Func<SAV3, byte[]> Export
)
{
    public static readonly Gen3EventFileKind WC3 = new(
        "Mystery Gift (WC3)", "wc3",
        static sav => sav.HasWC3(),
        static sav => sav.GetWC3Title(),
        static sav => [sav.GetWC3FileSize()],
        static (sav, data) => sav.ImportWC3Data(data),
        static sav => [.. sav.ExportWC3Data()]);

    public static readonly Gen3EventFileKind ME3 = new(
        "Mystery Event (ME3)", "me3",
        static sav => sav.HasME3(),
        static sav => sav.GetME3Summary(), // scripts have no title; show the embedded dialog text
        static sav => sav.LargeBlock is ISaveBlock3LargeHoenn
            ? [SAV3.GetME3FileSize(), SAV3.GetME3FileSize() + RecordMixing3Gift.SIZE]
            : [SAV3.GetME3FileSize()],
        static (sav, data) => sav.ImportME3Data(data),
        static sav => [.. sav.ExportME3Data()]);

    public static readonly Gen3EventFileKind WN3 = new(
        "Wonder News (WN3)", "wn3",
        static sav => sav.HasWN3(),
        static sav => sav.GetWN3Title(),
        static sav => [sav.GetWN3FileSize()],
        static (sav, data) => sav.ImportWN3Data(data),
        static sav => [.. sav.ExportWN3Data()]);

    public static readonly Gen3EventFileKind ECT = new(
        "e-Card Trainer (ECT)", "ect",
        static sav => sav.HasECT(),
        static sav => sav.GetECTTrainerName(),
        static sav => [SAV3.GetECTFileSize()],
        static (sav, data) => sav.ImportECTData(data),
        static sav => [.. sav.ExportECTData()]);

    public static readonly Gen3EventFileKind ECB = new(
        "e-Card Berry (ECB)", "ecb",
        static sav => sav.HasECB(),
        static sav => sav.GetECBName(),
        static sav => [sav.GetECBFileSize()],
        static (sav, data) => sav.ImportECBData(data),
        static sav => [.. sav.ExportECBData()]);
}
