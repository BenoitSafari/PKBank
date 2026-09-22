using System.Collections.Generic;
using System.Linq;
using PKBank.Desktop.Services.Slots;
using PKBank.Desktop.Services.Slots.Types;
using PKHeX.Core;

namespace PKBank.Desktop.Services.Banks;

/// <summary>
///     A bank seen as slot storage: one container per page, plus a spare trailing page so the bank can
///     always grow. Writes go into the session, not the folder.
/// </summary>
public sealed class BankSlotStore(BankSession session, SaveFile sav, string language) : ISlotStore
{
    private const int PageColumns = 12;

    public BankSession Session { get; } = session;

    public bool IsParty => false;
    public SlotScope Scope => SlotScope.Bank;

    /// <summary>One page past the end, so there is always somewhere to drop a new entity.</summary>
    public int ContainerCount => Session.PageCount + 1;

    public int SlotsPerContainer => BankStorage.SlotsPerPage;
    public int Columns => PageColumns;

    public IReadOnlyList<string> ContainerNames =>
        Enumerable.Range(1, ContainerCount).Select(static n => $"Page {n}").ToArray();

    public PKM Blank => sav.BlankPKM;

    public PKM Read(int container, int index) => Session.Peek(container, index) ?? Blank;

    public void Write(int container, int index, PKM pk) =>
        Session.Place(container, index, pk.Species == 0 ? null : pk);

    /// <summary>Entries the loaded save could not take are shown greyed out and stay read-only.</summary>
    public bool IsCompatible(PKM pk) => BankCompatibility.IsImportable(pk, sav, language);

    /// <summary>A bank stores raw files of any generation, so it takes anything as-is.</summary>
    public PKM? TryAccept(PKM pk, out string message)
    {
        message = string.Empty;
        return pk;
    }
}
