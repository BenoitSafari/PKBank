using Avalonia.Data.Converters;
using Avalonia.Media;

namespace PKBank.Desktop.Views;

public static class Converters
{
    /// <summary>Green when legal, red when not.</summary>
    public static readonly IValueConverter LegalityBrush =
        new FuncValueConverter<bool, IBrush>(valid => valid ? Brushes.MediumSeaGreen : Brushes.IndianRed);
}
