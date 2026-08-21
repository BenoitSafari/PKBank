using System;
using System.Collections.Generic;
using PKHeX.Core;
using static System.Buffers.Binary.BinaryPrimitives;

namespace PKBank.Core.Events;

/// <summary>
/// Import/export of Gen 3 event data files, adapted from the WC3 Plugin
/// (https://github.com/Lusamine/PKHeXWC3Plugin, GPL v3):
/// Mystery Gifts (WC3, FRLG/E), Mystery Events (ME3, RS/E), Wonder News (WN3, FRLG/E),
/// e-Card Trainers (ECT) and e-Card Berries (ECB). Checksums are fixed on import.
/// </summary>
public static class Gen3EventFiles
{
    #region WC3
    /*
     * File Size:       INT 0x58C,         JP 0x4E4
     * Offsets:
     * WonderCard:      INT 0     - 0x14F, JP 0     - 0x0A7
     * WonderCardExtra: INT 0x150 - 0x177, JP 0x0A8 - 0x0CF // only for card type 2 (link stats)
     * Trainer IDs:     INT 0x178 - 0x19F, JP 0x0D0 - 0x0F7 // only for card type 2 (link stats)
     * MysteryData:     INT 0x1A0 - 0x58B, JP 0x0F8 - 0x4E3
     */
    public static void ImportWC3(this SAV3 sav, byte[] data)
    {
        if (sav.LargeBlock is not ISaveBlock3LargeExpansion wonder)
            return;

        int cardSize = GetWC3CardSize(sav);
        int scriptOffset = GetWC3ScriptOffset(sav);

        Memory<byte> memory = new(data);
        WonderCard3 wc3 = new(memory[..cardSize]);
        wc3.FixChecksum();
        wonder.SetWonderCard(sav.Japanese, wc3.Data);

        if (wc3.Type == CardTypeLinkStat)
        {
            WonderCard3Extra wc3Extra = new(memory[cardSize..(cardSize + WonderCard3Extra.SIZE)]);
            // wc3Extra.FixChecksum(); checksum is unused in the games
            wonder.SetWonderCardExtra(sav.Japanese, wc3Extra.Data);
            // Trainer IDs of card type 2 are not preserved (same as the WC3 Plugin).
        }

        MysteryEvent3 me3 = new(memory[scriptOffset..]);
        me3.FixChecksum();
        ((ISaveBlock3Large)sav.LargeBlock).MysteryData = me3;
    }

    public static ReadOnlySpan<byte> ExportWC3(this SAV3 sav)
    {
        if (sav.LargeBlock is not ISaveBlock3LargeExpansion wonder)
            return [];

        Span<byte> data = new byte[GetWC3FileSize(sav)];
        wonder.GetWonderCard(sav.Japanese).Data.CopyTo(data);

        if (wonder.GetWonderCard(sav.Japanese).Type == CardTypeLinkStat)
            wonder.GetWonderCardExtra(sav.Japanese).Data.CopyTo(data[GetWC3CardSize(sav)..]);

        ((ISaveBlock3Large)sav.LargeBlock).MysteryData.Data.CopyTo(data[GetWC3ScriptOffset(sav)..]);
        return data;
    }

    public static bool HasWC3(this SAV3 sav)
        => sav.LargeBlock is ISaveBlock3LargeExpansion wonder && !IsEmpty(wonder.GetWonderCard(sav.Japanese).Data);

    public static string GetWC3Title(this SAV3 sav)
        => sav.LargeBlock is ISaveBlock3LargeExpansion wonder ? wonder.GetWonderCard(sav.Japanese).Title.Trim() : string.Empty;

    public static int GetWC3FileSize(this SAV3 sav) => GetWC3ScriptOffset(sav) + MysteryEvent3.SIZE;

    private static int GetWC3CardSize(SAV3 sav) => sav.Japanese ? WonderCard3.SIZE_JAP : WonderCard3.SIZE;
    private static int GetWC3ScriptOffset(SAV3 sav) => GetWC3CardSize(sav) + (WonderCard3Extra.SIZE * 2);
    private const int CardTypeLinkStat = 2;
    #endregion WC3

    #region ME3
    public static void ImportME3(this SAV3 sav, byte[] data)
    {
        Memory<byte> memory = new(data);
        Gen3MysteryData mystery;
        if (sav.LargeBlock is ISaveBlock3LargeExpansion wonder) // FRLGE
        {
            // A Mystery Event overrides any Mystery Gift present.
            wonder.SetWonderCard(sav.Japanese, new WonderCard3(new byte[GetWC3CardSize(sav)]).Data);

            var me3 = new MysteryEvent3(memory[..MysteryEvent3.SIZE]);
            me3.FixChecksum();
            mystery = me3;
        }
        else // RS
        {
            var me3 = new MysteryEvent3RS(memory[..MysteryEvent3.SIZE]);
            me3.FixChecksum();
            mystery = me3;
        }
        ((ISaveBlock3Large)sav.LargeBlock).MysteryData = mystery;

        if (sav.LargeBlock is ISaveBlock3LargeHoenn hoenn && data.Length == MysteryEvent3.SIZE + RecordMixing3Gift.SIZE)
        {
            RecordMixing3Gift rm3 = new(memory[MysteryEvent3.SIZE..]);
            rm3.FixChecksum();
            hoenn.RecordMixingGift = rm3;
        }
    }

    public static ReadOnlySpan<byte> ExportME3(this SAV3 sav)
        => ((ISaveBlock3Large)sav.LargeBlock).MysteryData.Data;

    public static bool HasME3(this SAV3 sav)
    {
        return sav is SAV3RS
            ? !IsEmpty(((ISaveBlock3Large)sav.LargeBlock).MysteryData.Data)
            : sav.LargeBlock is ISaveBlock3LargeExpansion wonder && !IsEmpty(((ISaveBlock3Large)sav.LargeBlock).MysteryData.Data) && IsEmpty(wonder.GetWonderCard(sav.Japanese).Data);
    }

    public static int GetME3FileSize(this SAV3 _) => MysteryEvent3.SIZE;
    #endregion ME3

    #region WN3
    public static void ImportWN3(this SAV3 sav, byte[] data)
    {
        if (sav.LargeBlock is not ISaveBlock3LargeExpansion wonder)
            return;

        WonderNews3 wn3 = new(new Memory<byte>(data));
        wn3.FixChecksum();
        wonder.SetWonderNews(sav.Japanese, wn3.Data);
    }

    public static ReadOnlySpan<byte> ExportWN3(this SAV3 sav)
    {
        if (sav.LargeBlock is not ISaveBlock3LargeExpansion wonder)
            return [];

        Span<byte> data = new byte[GetWN3FileSize(sav)];
        wonder.GetWonderNews(sav.Japanese).Data.CopyTo(data);
        return data;
    }

    public static bool HasWN3(this SAV3 sav)
        => sav.LargeBlock is ISaveBlock3LargeExpansion wonder && !IsEmpty(wonder.GetWonderNews(sav.Japanese).Data);

    public static string GetWN3Title(this SAV3 sav)
        => sav.LargeBlock is ISaveBlock3LargeExpansion wonder ? wonder.GetWonderNews(sav.Japanese).Title.Trim() : string.Empty;

    public static int GetWN3FileSize(this SAV3 sav) => sav.Japanese ? WonderNews3.SIZE_JAP : WonderNews3.SIZE;
    #endregion WN3

    #region ECT
    public static void ImportECT(this SAV3 sav, byte[] data)
    {
        FixECTChecksum(data).CopyTo(((ISaveBlock3Small)sav.SmallBlock).EReaderTrainer);
    }

    public static ReadOnlySpan<byte> ExportECT(this SAV3 sav)
        => ((ISaveBlock3Small)sav.SmallBlock).EReaderTrainer;

    public static bool HasECT(this SAV3 sav)
        => !IsEmpty(((ISaveBlock3Small)sav.SmallBlock).EReaderTrainer);

    public static string GetECTTrainerName(this SAV3 sav)
        => StringConverter3.GetString(sav.ExportECT()[4..(4 + (sav.Japanese ? 5 : 7))], sav.Japanese).Trim();

    public static int GetECTFileSize(this SAV3 _) => ECT_SIZE;

    private static Span<byte> FixECTChecksum(Span<byte> data)
    {
        WriteUInt32LittleEndian(data[(ECT_SIZE - 4)..], GetECTChecksum(data));
        return data;
    }

    private static uint GetECTChecksum(Span<byte> data)
    {
        uint chk = 0;
        for (int i = 0; i < ECT_SIZE - 4; i += 4)
            chk += ReadUInt32LittleEndian(data[i..]);
        return chk;
    }

    private const int ECT_SIZE = 188;
    #endregion ECT

    #region ECB
    public static void ImportECB(this SAV3 sav, byte[] data)
    {
        FixECBChecksum(data).CopyTo(((ISaveBlock3Large)sav.LargeBlock).EReaderBerry);
        sav.SetWork(sav.LargeBlock is ISaveBlock3LargeHoenn ? VarEnigmaBerryAvailableRSE : VarEnigmaBerryAvailableFRLG, 1);
    }

    public static ReadOnlySpan<byte> ExportECB(this SAV3 sav)
        => ((ISaveBlock3Large)sav.LargeBlock).EReaderBerry;

    public static bool HasECB(this SAV3 sav)
        => !IsEmpty(((ISaveBlock3Large)sav.LargeBlock).EReaderBerry);

    public static string GetECBName(this SAV3 sav) => sav.EBerryName.Trim();

    public static int GetECBFileSize(this SAV3 sav) => sav is SAV3RS ? ECB_SIZE_RS : ECB_SIZE_FRLGE;

    private static ReadOnlySpan<byte> FixECBChecksum(Span<byte> data)
    {
        WriteUInt16LittleEndian(data[(data.Length - 4)..], GetECBChecksum(data));
        return data;
    }

    private static ushort GetECBChecksum(ReadOnlySpan<byte> data)
    {
        ushort chk = 0;
        if (data.Length == ECB_SIZE_RS)
        {
            for (int i = 0; i < ECB_SIZE_RS - 4; i++)
            {
                if (i is < 0xC or >= 0x14)
                    chk += data[i];
            }
        }
        else
        {
            for (int i = 0; i < ECB_SIZE_FRLGE - 4; i++)
                chk += data[i];
        }
        return chk;
    }

    private const int ECB_SIZE_RS = 1328;
    private const int ECB_SIZE_FRLGE = 52;
    private const int VarEnigmaBerryAvailableRSE = 0x2D;  // 0x402D
    private const int VarEnigmaBerryAvailableFRLG = 0x33; // 0x4033; unused but set by script command
    #endregion ECB

    #region RM3
    public static void SetRecordMixing(this SAV3 sav, ushort item, byte count)
    {
        if (sav.LargeBlock is not ISaveBlock3LargeHoenn hoenn)
            return;

        RecordMixing3Gift rm3 = new(new byte[RecordMixing3Gift.SIZE])
        {
            Item = item,
            Count = item == 0 ? (byte)0 : count,
        };
        rm3.FixChecksum();
        hoenn.RecordMixingGift = rm3;
    }

    public static RecordMixing3Gift? GetRecordMixing(this SAV3 sav)
        => sav.LargeBlock is ISaveBlock3LargeHoenn hoenn ? hoenn.RecordMixingGift : null;

    public static bool IsValidForRecordMixing(this SAV3 sav, ushort item)
    {
        if (sav.LargeBlock is not ISaveBlock3LargeHoenn)
            return false;

        if (item is 0 or EonTicket)
            return true;

        return GetRecordMixingStorage(sav).GetItems(InventoryType.PCItems).Contains(item);
    }

    /// <summary>Items sendable via Record Mixing: anything the game can hold in the PC, plus the Eon Ticket.</summary>
    public static IReadOnlyList<ushort> GetRecordMixingItems(this SAV3 sav)
    {
        var items = GetRecordMixingStorage(sav).GetItems(InventoryType.PCItems);
        var result = new List<ushort>(items.Length + 1);
        foreach (var item in items)
            result.Add(item);
        result.Add(EonTicket);
        return result;
    }

    private static IItemStorage GetRecordMixingStorage(SAV3 sav) => sav is SAV3E ? new ItemStorage3E() : new ItemStorage3RS();

    private const ushort EonTicket = 0x113;
    #endregion RM3

    private static bool IsEmpty(ReadOnlySpan<byte> data) => data.IndexOfAnyExcept<byte>(0, 0xFF) == -1;
}
