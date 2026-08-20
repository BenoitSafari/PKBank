using Avalonia.Media.Imaging;
using PKBank.Desktop.Sprites;
using PKHeX.Core;

namespace PKBank.Desktop.ViewModels;

/// <summary>
/// A single box or party slot of the currently loaded save.
/// Reads decode a fresh <see cref="PKM"/> copy; writes push data back into the save.
/// </summary>
public sealed class SlotViewModel : ViewModelBase
{
    private readonly SaveFile _sav;
    private Bitmap? _sprite;
    private string _toolTip = string.Empty;
    private bool _isSelected;
    private bool _isEmpty = true;

    public bool IsParty { get; }
    public int Box { get; private set; }
    public int Slot { get; }

    public SlotViewModel(SaveFile sav, bool isParty, int box, int slot)
    {
        _sav = sav;
        IsParty = isParty;
        Box = box;
        Slot = slot;
        Refresh();
    }

    public Bitmap? Sprite { get => _sprite; private set => SetField(ref _sprite, value); }
    public string ToolTip { get => _toolTip; private set => SetField(ref _toolTip, value); }
    public bool IsEmpty { get => _isEmpty; private set => SetField(ref _isEmpty, value); }
    public bool IsSelected { get => _isSelected; set => SetField(ref _isSelected, value); }

    public void ChangeBox(int box)
    {
        Box = box;
        Refresh();
    }

    public PKM Read() => IsParty ? _sav.GetPartySlotAtIndex(Slot) : _sav.GetBoxSlotAtIndex(Box, Slot);

    public void Write(PKM pk)
    {
        if (IsParty)
            _sav.SetPartySlotAtIndex(pk, Slot);
        else
            _sav.SetBoxSlotAtIndex(pk, Box, Slot);
        Refresh();
    }

    public void Refresh()
    {
        var pk = Read();
        IsEmpty = pk.Species == 0;
        Sprite = SpriteService.GetPokemonSprite(pk);

        var species = GameInfo.Strings.specieslist;
        ToolTip = IsEmpty || pk.Species >= species.Length
            ? "(empty)"
            : $"{pk.Nickname} — {species[pk.Species]} (Lv. {pk.CurrentLevel})";
    }
}
