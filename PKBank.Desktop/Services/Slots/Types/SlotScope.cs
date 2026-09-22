namespace PKBank.Desktop.Services.Slots.Types;

/// <summary>
///     Selection boundary. A selection never spans two scopes: box and party slots of the loaded save
///     select together, bank slots select on their own.
/// </summary>
public enum SlotScope
{
    Save,
    Bank
}
