using PKHeX.Core;

namespace PKBank.Core.Moves;

public enum MoveCategory : byte
{
    Status = 0,
    Physical = 1,
    Special = 2,
}

/// <summary>
/// Per-move details absent from PKHeX.Core: damage category, base power, accuracy.
/// </summary>
public static partial class MoveDetails
{
    public static bool HasData(ushort move) => move is > 0 and <= MaxMoveId;

    /// <summary>
    /// Gets the damage category of a move for the given generation.
    /// Before the Gen 4 physical/special split, damaging moves derive their
    /// category from their type (types below Fire are physical).
    /// </summary>
    public static MoveCategory GetCategory(ushort move, byte generation, EntityContext context)
    {
        if (!HasData(move))
            return MoveCategory.Status;

        var current = (MoveCategory)Categories[move];
        if (current == MoveCategory.Status || generation >= 4)
            return current;

        // Gen 1-3: category is determined by the move's type.
        var type = MoveInfo.GetType(move, context);
        return type < (byte)MoveType.Fire ? MoveCategory.Physical : MoveCategory.Special;
    }

    /// <summary>Base power (current-generation value); 0 when not applicable.</summary>
    public static byte GetPower(ushort move) => HasData(move) ? Powers[move] : (byte)0;

    /// <summary>Accuracy in percent (current-generation value); 0 means it always hits or is not applicable.</summary>
    public static byte GetAccuracy(ushort move) => HasData(move) ? Accuracies[move] : (byte)0;
}
