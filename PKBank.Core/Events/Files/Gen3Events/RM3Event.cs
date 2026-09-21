using System;
using System.Collections.Generic;
using PKHeX.Core;

// MemoryExtensions.Contains on the PC item span

namespace PKBank.Core.Events.Files.Gen3Events;

/// <summary>
///     Record Mixing gifts (RM3, RS/E), adapted from the WC3 Plugin. The gift lives in the
///     save rather than in a file of its own, but rides along with a Mystery Event file.
/// </summary>
/// <seealso href="https://github.com/Lusamine/PKHeXWC3Plugin" />
public static class RM3Event
{
    private const ushort EonTicket = 0x113;

    extension(SAV3 sav)
    {
        public RecordMixing3Gift? GetRecordMixing() =>
            sav.LargeBlock is ISaveBlock3LargeHoenn hoenn ? hoenn.RecordMixingGift : null;

        public void SetRecordMixing(ushort item, byte count)
        {
            if (sav.LargeBlock is not ISaveBlock3LargeHoenn hoenn)
                return;

            RecordMixing3Gift rm3 = new(new byte[RecordMixing3Gift.SIZE])
            {
                Item = item,
                Count = item == 0 ? (byte)0 : count
            };
            rm3.FixChecksum();
            hoenn.RecordMixingGift = rm3;
        }

        public bool IsValidForRecordMixing(ushort item)
        {
            if (sav.LargeBlock is not ISaveBlock3LargeHoenn)
                return false;
            if (item is 0 or EonTicket)
                return true;
            return GetRecordMixingStorage(sav)
                .GetItems(InventoryType.PCItems)
                .Contains(item);
        }

        /// <summary>
        ///     Items sendable via Record Mixing: anything the game can hold in the PC, plus the Eon Ticket.
        /// </summary>
        public IReadOnlyList<ushort> GetRecordMixingItems()
        {
            var items = GetRecordMixingStorage(sav).GetItems(InventoryType.PCItems);
            var result = new List<ushort>(items.Length + 1);
            foreach (var item in items) result.Add(item);
            result.Add(EonTicket);
            return result;
        }
    }

    private static IItemStorage GetRecordMixingStorage(SAV3 sav) =>
        sav is SAV3E ? new ItemStorage3E() : new ItemStorage3RS();
}
