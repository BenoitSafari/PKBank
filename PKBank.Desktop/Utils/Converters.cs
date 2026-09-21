using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using PKBank.Desktop.Sprites;

namespace PKBank.Desktop.Utils;

public static class Converters
{
    public static readonly IValueConverter LegalityBrush =
        new FuncValueConverter<bool, IBrush>(valid => valid
            ? Brushes.MediumSeaGreen
            : Brushes.IndianRed
        );

    public static readonly IValueConverter BallSprite =
        new FuncValueConverter<int, Bitmap?>(ball => ball is > 0 and <= byte.MaxValue
            ? SpriteService.GetBallSprite((byte)ball)
            : null
        );

    /// <summary>National dex number shown beside a species name; blank for the empty entry.</summary>
    public static readonly IValueConverter DexNumber =
        new FuncValueConverter<int, string>(species => species > 0
            ? $"#{species:000}"
            : string.Empty
        );
}
