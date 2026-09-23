using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using PKBank.Desktop.Components.Pokedex.ViewModels;
using PKHeX.Core;

namespace PKBank.Desktop.Components.Pokedex;

/// <summary>
/// Simple Pokédex editor, mirroring WinForms' SAV_SimplePokedex: seen and
/// caught checkbox lists over every species the save knows, saved on demand.
/// </summary>
public sealed partial class PokedexEditorWindow : Window
{
    private readonly SaveFile? _sav;
    private readonly List<PokedexEntryViewModel> _entries = [];

    public PokedexEditorWindow() => InitializeComponent(); // designer

    public PokedexEditorWindow(SaveFile sav) : this()
    {
        _sav = sav;
        var names = GameInfo.Strings.specieslist;
        for (ushort species = 1; species <= sav.MaxSpeciesID; species++)
        {
            var name = species < names.Length ? names[species] : $"#{species}";
            _entries.Add(new PokedexEntryViewModel(
                species,
                $"{species:000} - {name}",
                sav.GetSeen(species),
                sav.GetCaught(species)));
        }
        SeenList.ItemsSource = _entries;
        CaughtList.ItemsSource = _entries;
    }

    private void OnAllSeenClicked(object? sender, RoutedEventArgs e) => SetAll(static e => e.Seen = true);
    private void OnNoneSeenClicked(object? sender, RoutedEventArgs e) => SetAll(static e => e.Seen = false);
    private void OnAllCaughtClicked(object? sender, RoutedEventArgs e) => SetAll(static e => e.Caught = true);
    private void OnNoneCaughtClicked(object? sender, RoutedEventArgs e) => SetAll(static e => e.Caught = false);

    private void SetAll(System.Action<PokedexEntryViewModel> apply)
    {
        foreach (var entry in _entries)
            apply(entry);
    }

    private void OnCancelClicked(object? sender, RoutedEventArgs e) => Close();

    private void OnSaveClicked(object? sender, RoutedEventArgs e)
    {
        if (_sav is not { } sav)
        {
            Close();
            return;
        }

        foreach (var entry in _entries)
        {
            sav.SetSeen(entry.Species, entry.Seen);
            sav.SetCaught(entry.Species, entry.Caught);
        }
        SanityCheck(sav);
        sav.State.Edited = true;
        Close();
    }

    // Same post-save fixups as WinForms' SAV_SimplePokedex.
    private void SanityCheck(SaveFile sav)
    {
        if (sav is SAV3FRLG { IsVirtualConsole: true })
        {
            for (ushort species = 151; species <= sav.MaxSpeciesID; species++)
            {
                if (Legal.IsForeignFRLG(species))
                    sav.SetCaught(species, false);
            }
        }
        if (sav is SAV3 s3)
            s3.MirrorSeenFlags();
    }
}
