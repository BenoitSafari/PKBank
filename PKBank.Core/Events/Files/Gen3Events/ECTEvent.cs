using System;
using PKHeX.Core;
using static System.Buffers.Binary.BinaryPrimitives;

namespace PKBank.Core.Events.Files.Gen3Events;

/// <summary>
///     Import/export of Gen 3 e-Card Trainer (ECT) data files, adapted from the WC3 Plugin.
///     Checksums are fixed on import.
/// </summary>
/// <seealso href="https://github.com/Lusamine/PKHeXWC3Plugin" />
public static class ECTEvent
{
    private const int ECT_SIZE = 188;

    extension(SAV3 sav)
    {
        public bool HasECT() => !Gen3EventDataReader.IsEmptyEvent(sav.SmallBlock.EReaderTrainer);

        public string GetECTTrainerName() => StringConverter3
            .GetString(sav.ExportECTData()[4..(4 + (sav.Japanese ? 5 : 7))], sav.Japanese)
            .Trim();

        public static int GetECTFileSize() => ECT_SIZE;

        public ReadOnlySpan<byte> ExportECTData() => sav.SmallBlock.EReaderTrainer;

        public void ImportECTData(byte[] data) => FixECTChecksum(data).CopyTo(sav.SmallBlock.EReaderTrainer);
    }

    private static Span<byte> FixECTChecksum(Span<byte> data)
    {
        WriteUInt32LittleEndian(data[(ECT_SIZE - 4)..], GetECTChecksum(data));
        return data;
    }

    private static uint GetECTChecksum(Span<byte> data)
    {
        uint chk = 0;
        for (var i = 0; i < ECT_SIZE - 4; i += 4)
            chk += ReadUInt32LittleEndian(data[i..]);
        return chk;
    }
}
