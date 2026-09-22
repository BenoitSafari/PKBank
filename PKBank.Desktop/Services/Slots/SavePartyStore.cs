using System.Collections.Generic;
using PKBank.Desktop.Services.Slots.Types;
using PKHeX.Core;

namespace PKBank.Desktop.Services.Slots;

/// <summary>The six party slots of the loaded save; a single container.</summary>
public sealed class SavePartyStore(SaveFile sav) : ISlotStore
{
    public const int PartySize = 6;

    public bool IsParty => true;
    public SlotScope Scope => SlotScope.Save;
    public int ContainerCount => 1;
    public int SlotsPerContainer => PartySize;
    public int Columns => PartySize;
    public IReadOnlyList<string> ContainerNames => [];

    public PKM Blank => sav.BlankPKM;

    public PKM Read(int container, int index) => sav.GetPartySlotAtIndex(index);

    public void Write(int container, int index, PKM pk) => sav.SetPartySlotAtIndex(pk, index);

    public PKM? TryAccept(PKM pk, out string message) => SaveEntityAccess.TryAccept(sav, pk, out message);
}
