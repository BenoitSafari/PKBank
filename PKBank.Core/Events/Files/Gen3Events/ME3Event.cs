using System;
using PKHeX.Core;

namespace PKBank.Core.Events.Files.Gen3Events;

/// <summary>
///     Import/export of Gen 3 Mystery Events (ME3, RS/E) data files, adapted from the WC3 Plugin.
/// </summary>
/// <seealso href="https://github.com/Lusamine/PKHeXWC3Plugin" />
public static class ME3Event
{
    extension(SAV3 sav)
    {
        public bool HasME3()
            => sav is SAV3RS
                ? !Gen3EventDataReader.IsEmptyEvent(sav.LargeBlock.MysteryData.Data)
                : sav.LargeBlock is ISaveBlock3LargeExpansion wonder
                  && !Gen3EventDataReader.IsEmptyEvent(sav.LargeBlock.MysteryData.Data)
                  && Gen3EventDataReader.IsEmptyEvent(wonder.GetWonderCard(sav.Japanese).Data);

        /// <summary>
        ///     Mystery Events are scripts without a title field; the embedded dialog text
        ///     (e.g. “DAD: It appears to be a ferry TICKET,”) is the best identifier.
        /// </summary>
        public string GetME3Summary() => sav.HasME3()
            ? Gen3EventDataReader.ExtractReadableText(sav.ExportME3Data(), sav.Japanese)
            : string.Empty;

        public static int GetME3FileSize() => MysteryEvent3.SIZE;

        public ReadOnlySpan<byte> ExportME3Data() => sav.LargeBlock.MysteryData.Data;

        public void ImportME3Data(byte[] data)
        {
            Memory<byte> memory = new(data);
            Gen3MysteryData mystery;
            if (sav.LargeBlock is ISaveBlock3LargeExpansion wonder) // FRLGE
            {
                // A Mystery Event overrides any Mystery Gift present.
                wonder.SetWonderCard(sav.Japanese, new WonderCard3(new byte[sav.GetWC3CardSize()]).Data);
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

            sav.LargeBlock.MysteryData = mystery;

            if (
                sav.LargeBlock is ISaveBlock3LargeHoenn hoenn
                && data.Length == MysteryEvent3.SIZE + RecordMixing3Gift.SIZE
            )
            {
                RecordMixing3Gift rm3 = new(memory[MysteryEvent3.SIZE..]);
                rm3.FixChecksum();
                hoenn.RecordMixingGift = rm3;
            }
        }
    }
}
