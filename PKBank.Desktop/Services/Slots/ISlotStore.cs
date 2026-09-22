using System.Collections.Generic;
using PKBank.Desktop.Services.Slots.Types;
using PKHeX.Core;

namespace PKBank.Desktop.Services.Slots;

/// <summary>
///     Storage addressed by (container, index): the boxes of a save, its party, or the pages of a bank.
///     The instance doubles as the identity of the storage — two slots sharing store, container and index
///     point at the same physical location.
/// </summary>
public interface ISlotStore
{
    bool IsParty { get; }
    SlotScope Scope { get; }

    /// <summary>Boxes, pages, or 1 for the party.</summary>
    int ContainerCount { get; }

    int SlotsPerContainer { get; }

    /// <summary>Slots per row when the container is laid out as a grid.</summary>
    int Columns { get; }

    /// <summary>Names shown in the container picker; empty when there is nothing to pick.</summary>
    IReadOnlyList<string> ContainerNames { get; }

    PKM Blank { get; }

    PKM Read(int container, int index);
    void Write(int container, int index, PKM pk);

    /// <summary>
    ///     Whether an entity already sitting here can be viewed and edited. Always true for a save; a bank
    ///     holds entities from any generation, and those the loaded save cannot take are read-only.
    /// </summary>
    bool IsCompatible(PKM pk) => true;

    /// <summary>
    ///     Converts an entity coming from another store into what this one stores; null when it cannot be
    ///     taken. The caller passes a clone: conversion may mutate the entity in place.
    /// </summary>
    PKM? TryAccept(PKM pk, out string message);
}
