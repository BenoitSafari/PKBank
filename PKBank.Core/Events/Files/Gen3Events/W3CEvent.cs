using System;
using PKHeX.Core;

namespace PKBank.Core.Events.Files.Gen3Events;

/// <summary>
///     Import/export of Gen 3 Mystery Gifts (WC3, FRLG/E) data files, adapted from the WC3 Plugin.
/// </summary>
/// <seealso href="https://github.com/Lusamine/PKHeXWC3Plugin" />
public static class W3CEvent
{
    /*
     * File Size:       INT 0x58C,         JP 0x4E4
     * Offsets:
     * WonderCard:      INT 0     - 0x14F, JP 0     - 0x0A7
     * WonderCardExtra: INT 0x150 - 0x177, JP 0x0A8 - 0x0CF // only for card type 2 (link stats)
     * Trainer IDs:     INT 0x178 - 0x19F, JP 0x0D0 - 0x0F7 // only for card type 2 (link stats)
     * MysteryData:     INT 0x1A0 - 0x58B, JP 0x0F8 - 0x4E3
     */

    private const int CardTypeLinkStat = 2;

    extension(SAV3 sav)
    {
        public bool HasWC3()
            => sav.LargeBlock is ISaveBlock3LargeExpansion wonder &&
               !Gen3EventDataReader.IsEmptyEvent(wonder.GetWonderCard(sav.Japanese).Data);

        public string GetWC3Title()
            => sav.LargeBlock is ISaveBlock3LargeExpansion wonder
                ? wonder.GetWonderCard(sav.Japanese).Title.Trim()
                : string.Empty;

        public int GetWC3FileSize() => sav.GetWC3ScriptOffset() + MysteryEvent3.SIZE;
        public int GetWC3CardSize() => sav.Japanese ? WonderCard3.SIZE_JAP : WonderCard3.SIZE;
        public int GetWC3ScriptOffset() => sav.GetWC3CardSize() + (WonderCard3Extra.SIZE * 2);

        public ReadOnlySpan<byte> ExportWC3Data()
        {
            if (sav.LargeBlock is not ISaveBlock3LargeExpansion wonder)
                return [];

            Span<byte> data = new byte[sav.GetWC3FileSize()];
            wonder.GetWonderCard(sav.Japanese).Data.CopyTo(data);

            if (wonder.GetWonderCard(sav.Japanese).Type == CardTypeLinkStat)
                wonder.GetWonderCardExtra(sav.Japanese).Data.CopyTo(data[sav.GetWC3CardSize()..]);

            sav.LargeBlock.MysteryData.Data.CopyTo(data[sav.GetWC3ScriptOffset()..]);
            return data;
        }

        public void ImportWC3Data(byte[] data)
        {
            if (sav.LargeBlock is not ISaveBlock3LargeExpansion wonder)
                return;

            var cardSize = sav.GetWC3CardSize();
            var scriptOffset = sav.GetWC3ScriptOffset();

            Memory<byte> memory = new(data);
            WonderCard3 wc3 = new(memory[..cardSize]);
            wc3.FixChecksum();
            wonder.SetWonderCard(sav.Japanese, wc3.Data);

            if (wc3.Type == CardTypeLinkStat)
            {
                WonderCard3Extra wc3Extra = new(memory[cardSize..(cardSize + WonderCard3Extra.SIZE)]);
                // wc3Extra.FixChecksum(); checksum is unused in the games
                wonder.SetWonderCardExtra(sav.Japanese, wc3Extra.Data);
            }

            MysteryEvent3 me3 = new(memory[scriptOffset..]);
            me3.FixChecksum();
            sav.LargeBlock.MysteryData = me3;
        }
    }
}
