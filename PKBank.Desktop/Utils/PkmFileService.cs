using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Platform.Storage;
using PKHeX.Core;

namespace PKBank.Desktop.Utils;

/// <summary>
///     Reads and writes single-Pokémon files (.pk*, .ek*, .ck3, ...).
/// </summary>
public static class PkmFileService
{
    /// <summary>
    ///     Loads a Pokémon file and converts it to the save's entity type
    ///     (e.g. a .pk3 dropped on a Gen 4 save becomes a PK4 via EntityConverter).
    /// </summary>
    public static PKM? TryLoadCompatible(string path, SaveFile sav, out string message)
    {
        try
        {
            var length = new FileInfo(path).Length;
            if (FileUtil.IsFileTooBig(length) || FileUtil.IsFileTooSmall(length))
            {
                message = $"Not a Pokémon file: {Path.GetFileName(path)}";
                return null;
            }

            var data = File.ReadAllBytes(path);
            if (!FileUtil.TryGetPKM(data, out var pk, Path.GetExtension(path), sav))
            {
                message = $"Not a recognized Pokémon file: {Path.GetFileName(path)}";
                return null;
            }

            var destType = sav.PKMType;
            var converted = EntityConverter.ConvertToType(pk, destType, out var result);
            if (converted is null)
            {
                message = result.GetDisplayString(pk, destType);
                return null;
            }

            if (ReferenceEquals(pk, converted) && sav.State.Exportable)
                sav.AdaptToSaveFile(converted);

            message = result is EntityConverterResult.None or EntityConverterResult.Success
                ? $"Loaded {Path.GetFileName(path)}."
                : result.GetDisplayString(pk, destType);
            return converted;
        }
        catch (Exception ex)
        {
            message = $"Failed to read {Path.GetFileName(path)}: {ex.Message}";
            return null;
        }
    }

    /// <summary>
    ///     Writes the entity in the standard decrypted party format.
    /// </summary>
    public static void Export(PKM pk, string path)
    {
        pk.ForcePartyData();
        var buffer = new byte[pk.SIZE_PARTY];
        pk.WriteDecryptedDataParty(buffer);
        File.WriteAllBytes(path, buffer);
    }

    /// <summary>
    ///     File picker filter matching the save's supported entity extensions.
    /// </summary>
    public static List<FilePickerFileType> GetPickerFileTypes(SaveFile sav) =>
    [
        new("Pokémon files") { Patterns = [.. sav.PKMExtensions.Select(ext => $"*.{ext}"), "*.ek*"] },
        FilePickerFileTypes.All
    ];
}
