using Avalonia.Media.Imaging;
using PKBank.Desktop.Services.Slots;
using PKBank.Desktop.Services.Slots.Types;
using PKBank.Desktop.Sprites;
using PKHeX.Core;

namespace PKBank.Desktop.ViewModels;

/// <summary>
/// A single slot of a <see cref="ISlotStore"/> — a box or party slot of the loaded save, or a bank page
/// slot. Reads decode a fresh <see cref="PKM"/> copy; writes push data back into the store.
/// </summary>
public sealed class SlotViewModel : ViewModelBase
{
    private bool _isCompatible = true;
    private bool _isEmpty = true;
    private bool _isSelected;
    private Bitmap? _sprite;

    public SlotViewModel(ISlotStore store, int container, int index)
    {
        Store = store;
        Container = container;
        Index = index;
        Refresh();
    }

    public ISlotStore Store { get; }
    public int Container { get; private set; }
    public int Index { get; }

    public bool IsParty => Store.IsParty;

    /// <summary>Identifies the physical location, so mirrored views and import targets can be matched.</summary>
    public SlotKey Key => new(Store, Container, Index);

    public Bitmap? Sprite { get => _sprite; private set => SetField(ref _sprite, value); }
    public bool IsEmpty { get => _isEmpty; private set => SetField(ref _isEmpty, value); }
    public bool IsSelected { get => _isSelected; set => SetField(ref _isSelected, value); }

    /// <summary>False greys the sprite out and blocks View/Set: the entity does not fit the loaded save.</summary>
    public bool IsCompatible { get => _isCompatible; private set => SetField(ref _isCompatible, value); }

    /// <summary>Hover preview content; computed when the tooltip binding reads it.</summary>
    public SlotPreviewViewModel? Preview => SlotPreviewViewModel.TryCreate(Read());

    public void ChangeContainer(int container)
    {
        Container = container;
        Refresh();
    }

    public PKM Read() => Store.Read(Container, Index);

    public void Write(PKM pk)
    {
        Store.Write(Container, Index, pk);
        Refresh();
    }

    public void Refresh()
    {
        var pk = Read();
        IsEmpty = pk.Species == 0;
        IsCompatible = IsEmpty || Store.IsCompatible(pk);
        Sprite = SpriteService.GetPokemonSprite(pk);
        OnPropertyChanged(nameof(Preview));
    }
}
