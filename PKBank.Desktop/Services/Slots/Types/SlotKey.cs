namespace PKBank.Desktop.Services.Slots.Types;

/// <summary>
///     Identity of a physical slot. Two view-models showing the same box in different panels share one key,
///     which is what lets writes be mirrored and import targets be de-duplicated.
/// </summary>
public readonly record struct SlotKey(ISlotStore Store, int Container, int Index);
