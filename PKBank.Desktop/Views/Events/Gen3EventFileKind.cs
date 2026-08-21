using System;
using System.Collections.Generic;
using PKHeX.Core;
using PKBank.Core.Events;

namespace PKBank.Desktop.Views.Events;

/// <summary>
/// Describes one kind of Gen 3 event data file (WC3/ME3/WN3/ECT/ECB) so a single
/// import/export window can service all of them, mirroring the WC3 Plugin forms.
/// </summary>
public sealed record Gen3EventFileKind(
    string Title,
    string Extension,
    Func<SAV3, bool> Has,
    Func<SAV3, string> GetSummary,
    Func<SAV3, IReadOnlyList<int>> GetValidSizes,
    Action<SAV3, byte[]> Import,
    Func<SAV3, byte[]> Export)
{
    public static readonly Gen3EventFileKind WC3 = new(
        "Mystery Gift (WC3)", "wc3",
        static sav => sav.HasWC3(),
        static sav => sav.GetWC3Title(),
        static sav => [sav.GetWC3FileSize()],
        static (sav, data) => sav.ImportWC3(data),
        static sav => sav.ExportWC3().ToArray());

    public static readonly Gen3EventFileKind ME3 = new(
        "Mystery Event (ME3)", "me3",
        static sav => sav.HasME3(),
        static sav => sav.GetME3Summary(), // scripts have no title; show the embedded dialog text
        static sav => sav.LargeBlock is ISaveBlock3LargeHoenn
            ? [sav.GetME3FileSize(), sav.GetME3FileSize() + RecordMixing3Gift.SIZE]
            : [sav.GetME3FileSize()],
        static (sav, data) => sav.ImportME3(data),
        static sav => sav.ExportME3().ToArray());

    public static readonly Gen3EventFileKind WN3 = new(
        "Wonder News (WN3)", "wn3",
        static sav => sav.HasWN3(),
        static sav => sav.GetWN3Title(),
        static sav => [sav.GetWN3FileSize()],
        static (sav, data) => sav.ImportWN3(data),
        static sav => sav.ExportWN3().ToArray());

    public static readonly Gen3EventFileKind ECT = new(
        "e-Card Trainer (ECT)", "ect",
        static sav => sav.HasECT(),
        static sav => sav.GetECTTrainerName(),
        static sav => [sav.GetECTFileSize()],
        static (sav, data) => sav.ImportECT(data),
        static sav => sav.ExportECT().ToArray());

    public static readonly Gen3EventFileKind ECB = new(
        "e-Card Berry (ECB)", "ecb",
        static sav => sav.HasECB(),
        static sav => sav.GetECBName(),
        static sav => [sav.GetECBFileSize()],
        static (sav, data) => sav.ImportECB(data),
        static sav => sav.ExportECB().ToArray());
}
