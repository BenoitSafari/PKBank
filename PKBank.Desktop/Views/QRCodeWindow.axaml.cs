using System.IO;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using PKBank.Desktop.Sprites;
using PKHeX.Core;
using QRCoder;

namespace PKBank.Desktop.Views;

public sealed partial class QRCodeWindow : Window
{
    public QRCodeWindow() => InitializeComponent(); // designer

    public QRCodeWindow(PKM pk) : this()
    {
        QrImage.Source = GenerateQr(QRMessageUtil.GetMessage(pk));
        SpriteImage.Source = SpriteService.GetPokemonSprite(pk);

        var la = new LegalityAnalysis(pk);
        LegalityImage.Source = la.Parsed ? SpriteService.GetLegalityOverlay(la.Valid) : null;

        var lines = pk.GetQRLines();
        var display = new string[lines.Length + 1];
        lines.CopyTo(display, 0);
        display[^1] = $"PKBank.Desktop ({pk.GetType().Name})";
        LinesControl.ItemsSource = display;
    }

    private static Bitmap GenerateQr(string message)
    {
        using var data = QRCodeGenerator.GenerateQrCode(message, QRCodeGenerator.ECCLevel.Q);
        var png = new PngByteQRCode(data).GetGraphic(8);
        using var ms = new MemoryStream(png);
        return new Bitmap(ms);
    }
}
