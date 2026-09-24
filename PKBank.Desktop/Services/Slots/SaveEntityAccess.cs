using PKHeX.Core;

namespace PKBank.Desktop.Services.Slots;

/// <summary>
///     Turning an arbitrary entity into one the loaded save can hold. Shared by the box and party stores.
/// </summary>
internal static class SaveEntityAccess
{
    public static EntityImportSettings ImportSettings { get; } = new(
        EntityImportOption.UseDefault, EntityImportOption.Enable, EntityImportOption.UseDefault);

    public static PKM? TryAccept(SaveFile sav, PKM pk, out string message)
    {
        var destType = sav.PKMType;
        var converted = EntityConverter.ConvertToType(pk, destType, out var result);
        if (converted is null)
        {
            message = result.GetDisplayString(pk, destType);
            return null;
        }

        // Nothing was converted, so the entity still carries the origin save's trainer data.
        if (ReferenceEquals(pk, converted) && sav.State.Exportable)
            sav.AdaptToSaveFile(converted);

        message = result is EntityConverterResult.None or EntityConverterResult.Success
            ? string.Empty
            : result.GetDisplayString(pk, destType);
        return converted;
    }
}
