using System;
using PKHeX.Core;

namespace PKBank.Core.Events.Files.Gen3Events;

/// <summary>
///     Import/export of Gen 3 Wonder News (WN3, FRLG/E) data files, adapted from the WC3 Plugin.
/// </summary>
/// <seealso href="https://github.com/Lusamine/PKHeXWC3Plugin" />
public static class WN3Event
{
    extension(SAV3 sav)
    {
        public bool HasWN3()
            => sav.LargeBlock is ISaveBlock3LargeExpansion wonder &&
               !Gen3EventDataReader.IsEmptyEvent(wonder.GetWonderNews(sav.Japanese).Data);

        public string GetWN3Title()
            => sav.LargeBlock is ISaveBlock3LargeExpansion wonder
                ? wonder.GetWonderNews(sav.Japanese).Title.Trim()
                : string.Empty;

        public int GetWN3FileSize() => sav.Japanese ? WonderNews3.SIZE_JAP : WonderNews3.SIZE;

        public ReadOnlySpan<byte> ExportWN3Data()
        {
            if (sav.LargeBlock is not ISaveBlock3LargeExpansion wonder)
                return [];

            Span<byte> data = new byte[sav.GetWN3FileSize()];
            wonder.GetWonderNews(sav.Japanese).Data.CopyTo(data);
            return data;
        }

        public void ImportWN3Data(byte[] data)
        {
            if (sav.LargeBlock is not ISaveBlock3LargeExpansion wonder)
                return;

            WonderNews3 wn3 = new(new Memory<byte>(data));
            wn3.FixChecksum();
            wonder.SetWonderNews(sav.Japanese, wn3.Data);
        }
    }
}
