using System.Diagnostics.CodeAnalysis;
using System.IO;
using PKHeX.Core;

namespace PKBank.Desktop.Utils;

/// <summary>
/// Recognizes dropped save files so they load as a save instead of being
/// mistaken for an entity file by the slot and editor drop handlers.
/// </summary>
public static class SaveFileDrop
{
    public static bool TryGetSaveFile(string path, [NotNullWhen(true)] out SaveFile? sav)
    {
        sav = null;
        var info = new FileInfo(path);
        if (!info.Exists || FileUtil.IsFileTooBig(info.Length) || FileUtil.IsFileTooSmall(info.Length))
            return false;
        return SaveUtil.TryGetSaveFile(path, out sav);
    }

    public static bool IsSaveFile(string path) => TryGetSaveFile(path, out _);
}
