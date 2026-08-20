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

    /// <summary>Base power as it was in the given generation; 0 when not applicable (or the move did not exist yet).</summary>
    public static byte GetPower(ushort move, byte generation)
        => HasData(move) ? PowersByGeneration[(GetGenerationIndex(generation) * Stride) + move] : (byte)0;

    /// <summary>Accuracy in percent as it was in the given generation; 0 means it always hits or is not applicable.</summary>
    public static byte GetAccuracy(ushort move, byte generation)
        => HasData(move) ? AccuraciesByGeneration[(GetGenerationIndex(generation) * Stride) + move] : (byte)0;

    private static int GetGenerationIndex(byte generation)
        => System.Math.Clamp(generation, (byte)1, (byte)Generations) - 1;
}
