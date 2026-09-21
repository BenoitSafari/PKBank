using System;
using System.Collections.Generic;
using PKHeX.Core;

namespace PKBank.Core.Events.Files;

public static class Gen3EventDataReader
{
    /// <summary>
    ///     Determines whether the specified event data buffer is empty or uninitialized.
    /// </summary>
    public static bool IsEmptyEvent(ReadOnlySpan<byte> data) => data.IndexOfAnyExcept<byte>(0, 0xFF) == -1;

    /// <summary>
    ///     Decodes the 0xFF-terminated Gen 3 text segments of a raw event payload and
    ///     returns the longest human-readable run, script bytes decoding to noise.
    /// </summary>
    public static string ExtractReadableText(ReadOnlySpan<byte> data, bool japanese)
    {
        var best = string.Empty;
        var start = 0;
        for (var i = 0; i <= data.Length; i++)
        {
            if (i < data.Length && data[i] != 0xFF)
                continue;
            if (i - start >= 8)
                foreach (var run in GetReadableRuns(StringConverter3.GetString(data[start..i], japanese), japanese))
                    if (run.Length > best.Length)
                        best = run;

            start = i + 1;
        }

        return best.TrimEnd(',', ' ');
    }

    private static IEnumerable<string> GetReadableRuns(string decoded, bool japanese)
    {
        var start = -1;
        for (var i = 0; i <= decoded.Length; i++)
        {
            var readable = i < decoded.Length && IsReadable(decoded[i], japanese);
            if (readable)
            {
                // Script opcodes decode to stray accented letters right before real
                // text (0x01-0x06 => À-É); only open a run on a plain letter/digit.
                if (start < 0 && (japanese || char.IsAsciiLetterOrDigit(decoded[i])))
                    start = i;
                continue;
            }

            if (start >= 0 && i - start >= 12)
                yield return decoded[start..i].Trim();
            start = -1;
        }
    }

    private static bool IsReadable(char c, bool japanese)
    {
        if (c is ' ' or '.' or ',' or '!' or '?' or ':' or '\'' or '-' or '…' or '“' or '”')
            return true;
        if (!char.IsLetterOrDigit(c))
            return false;
        // International saves decode script bytes to stray kana; restrict to Latin there.
        return japanese || c < 'ƀ';
    }
}
