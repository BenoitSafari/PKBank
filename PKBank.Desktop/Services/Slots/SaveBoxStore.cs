using System.Collections.Generic;
using PKBank.Desktop.Services.Slots.Types;
using PKBank.Desktop.Utils;
using PKHeX.Core;

namespace PKBank.Desktop.Services.Slots;

/// <summary>The boxes of the loaded save; one container per box.</summary>
public sealed class SaveBoxStore : ISlotStore
{
    private const int BoxColumns = 6;

    private readonly SaveFile _sav;

    private SaveBoxStore(SaveFile sav, IReadOnlyList<string> names)
    {
        _sav = sav;
        ContainerNames = names;
    }

    public bool IsParty => false;
    public SlotScope Scope => SlotScope.Save;
    public int ContainerCount => _sav.BoxCount;
    public int SlotsPerContainer => _sav.BoxSlotCount;
    public int Columns => BoxColumns;
    public IReadOnlyList<string> ContainerNames { get; }

    public PKM Blank => _sav.BlankPKM;

    public PKM Read(int container, int index) => _sav.GetBoxSlotAtIndex(container, index);

    public void Write(int container, int index, PKM pk) => _sav.SetBoxSlotAtIndex(pk, container, index);

    public PKM? TryAccept(PKM pk, out string message) => SaveEntityAccess.TryAccept(_sav, pk, out message);

    /// <summary>Reads the box names once; they only change when the save or the language does.</summary>
    public static SaveBoxStore Create(SaveFile sav)
    {
        var names = new string[sav.BoxCount];
        for (var i = 0; i < names.Length; i++)
        {
            string? name;
            try
            {
                name = (sav as IBoxDetailNameRead)?.GetBoxName(i);
            }
            catch
            {
                name = null; // some saves lack the underlying blocks
            }

            name = DisplayText.Sanitize(name ?? string.Empty);
            names[i] = string.IsNullOrWhiteSpace(name) ? BoxDetailNameExtensions.GetDefaultBoxName(i) : name;
        }

        return new SaveBoxStore(sav, names);
    }
}
