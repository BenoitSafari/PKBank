using System;
using PKHeX.Core;
using static System.Buffers.Binary.BinaryPrimitives;

namespace PKBank.Core.Events.Files.Gen3Events;

/// <summary>
///     Import/export of Gen 3 e-Card Berry (ECB) data files, adapted from the WC3 Plugin.
///     Checksums are fixed on import.
/// </summary>
/// <seealso href="https://github.com/Lusamine/PKHeXWC3Plugin" />
public static class ECBEvent
{
    private const int ECB_SIZE_RS = 1328;
    private const int ECB_SIZE_FRLGE = 52;
    private const int VarEnigmaBerryAvailableRSE = 0x2D; // 0x402D
    private const int VarEnigmaBerryAvailableFRLG = 0x33; // 0x4033; unused but set by script command

    extension(SAV3 sav)
    {
        public bool HasECB() => !Gen3EventDataReader.IsEmptyEvent(sav.LargeBlock.EReaderBerry);

        public string GetECBName() => sav.EBerryName.Trim();

        public int GetECBFileSize() => sav is SAV3RS ? ECB_SIZE_RS : ECB_SIZE_FRLGE;

        public ReadOnlySpan<byte> ExportECBData() => sav.LargeBlock.EReaderBerry;

        public void ImportECBData(byte[] data)
        {
            FixECBChecksum(data).CopyTo(sav.LargeBlock.EReaderBerry);
            sav.SetWork(
                sav.LargeBlock is ISaveBlock3LargeHoenn ? VarEnigmaBerryAvailableRSE : VarEnigmaBerryAvailableFRLG, 1
            );
        }
    }

    private static ReadOnlySpan<byte> FixECBChecksum(Span<byte> data)
    {
        WriteUInt16LittleEndian(data[^4..], GetECBChecksum(data));
        return data;
    }

    private static ushort GetECBChecksum(ReadOnlySpan<byte> data)
    {
        ushort chk = 0;
        if (data.Length == ECB_SIZE_RS)
        {
            for (var i = 0; i < ECB_SIZE_RS - 4; i++)
                if (i is < 0xC or >= 0x14)
                    chk += data[i];
        }
        else
        {
            for (var i = 0; i < ECB_SIZE_FRLGE - 4; i++)
                chk += data[i];
        }

        return chk;
    }
}
