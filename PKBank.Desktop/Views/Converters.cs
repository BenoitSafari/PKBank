using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using PKBank.Desktop.Sprites;

namespace PKBank.Desktop.Views;

public static class Converters
{
    /// <summary>Green when legal, red when not.</summary>
    public static readonly IValueConverter LegalityBrush =
        new FuncValueConverter<bool, IBrush>(valid => valid ? Brushes.MediumSeaGreen : Brushes.IndianRed);

    /// <summary>Ball item id → 20×20 ball sprite.</summary>
    public static readonly IValueConverter BallSprite =
        new FuncValueConverter<int, Bitmap?>(ball => ball is > 0 and <= byte.MaxValue ? SpriteService.GetBallSprite((byte)ball) : null);
}
