using System.Collections.Generic;
using PKHeX.Core;

namespace PKBank.Desktop.Services.Slots.Types;

/// <summary>
///     A dry-run result: where every entity of a dragged batch would land. Nothing is written until the
///     whole plan is known to be valid.
/// </summary>
/// <param name="Store">Destination store; every placement addresses it.</param>
/// <param name="Sources">Locations the batch is taken from, to be emptied.</param>
/// <param name="Placements">Entity already converted for the destination, and where it goes.</param>
public sealed record MultiMovePlan(
    ISlotStore Store,
    IReadOnlyList<SlotKey> Sources,
    IReadOnlyList<MultiMovePlacement> Placements);

public sealed record MultiMovePlacement(int Container, int Index, PKM Entity);
