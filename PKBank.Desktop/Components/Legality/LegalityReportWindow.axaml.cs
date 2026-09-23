using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using PKBank.Desktop.Sprites;
using PKHeX.Core;

namespace PKBank.Desktop.Components.Legality;

public sealed partial class LegalityReportWindow : Window
{
    public LegalityReportWindow() => InitializeComponent(); // designer

    public LegalityReportWindow(PKM pk) : this()
    {
        SpriteImage.Source = SpriteService.GetPokemonSprite(pk);

        var strings = GameInfo.Strings;
        var species = (uint)pk.Species < strings.specieslist.Length ? strings.specieslist[pk.Species] : $"#{pk.Species}";
        TitleText.Text = pk.IsNicknamed ? $"{species} ({pk.Nickname})" : species;

        var la = new LegalityAnalysis(pk);
        VerdictText.Text = la.Valid ? "Legal ✓" : "Illegal ✗";
        VerdictText.Foreground = la.Valid ? Brushes.MediumSeaGreen : Brushes.IndianRed;
        ReportText.Text = la.Report(verbose: true);
    }

    private void OnCloseClicked(object? sender, RoutedEventArgs e) => Close();
}
