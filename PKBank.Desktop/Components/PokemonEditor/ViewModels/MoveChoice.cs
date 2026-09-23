using Avalonia.Media.Imaging;
using PKBank.Desktop.Sprites;
using PKBank.Core.Moves;

namespace PKBank.Desktop.Components.PokemonEditor.ViewModels;

/// <summary>An entry of the move selectors: display text, move id, type, category and legality for the current entity.</summary>
public sealed record MoveChoice(string Text, int Value, bool IsIllegal, byte Type, MoveCategory Category)
{
    public bool HasType => Value != 0;
    public Bitmap? TypeSprite => HasType ? SpriteService.GetMoveTypeSprite(Type) : null;
    public string CategoryLabel => Value == 0 ? string.Empty : $"({Category})";
}
