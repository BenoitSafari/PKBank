using System.Diagnostics.CodeAnalysis;
using System.Linq;
using PKHeX.Core;

namespace PKBank.Desktop.Services.Banks;

/// <summary>
///     Whether an entity out of a bank can enter the loaded save. A bank holds files from any generation,
///     so this is the gate that greys entries out and refuses transfers.
/// </summary>
public static class BankCompatibility
{
    /// <summary>
    ///     The per-item form of <see cref="SaveExtensions.GetCompatible" />: convertible, language-compatible
    ///     for the Gen 1/2 pair, and within the save's own species/move/item limits.
    /// </summary>
    public static bool IsImportable(PKM pk, SaveFile sav, string language,
        [NotNullWhen(true)] out PKM? converted, out string message)
    {
        var destType = sav.PKMType;
        converted = EntityConverter.ConvertToType(pk, destType, out var result);
        if (converted is null)
        {
            message = result.GetDisplayString(pk, destType);
            return false;
        }

        if (sav is ILangDeviantSave deviant &&
            !EntityConverter.IsCompatibleGB(pk, deviant.Japanese, converted.Japanese))
        {
            message = EntityConverterResult.IncompatibleLanguageGB
                .GetIncompatibleGBMessage(converted, deviant.Japanese);
            converted = null;
            return false;
        }

        // Not a legality check: species/form/move/ability/item against what this game knows about.
        var errata = sav.EvaluateCompatibility(converted, language);
        if (errata.Count > 0)
        {
            message = string.Join('\n', errata);
            converted = null;
            return false;
        }

        message = string.Empty;
        return true;
    }

    public static bool IsImportable(PKM pk, SaveFile sav, string language) =>
        IsImportable(pk, sav, language, out _, out _);
}
