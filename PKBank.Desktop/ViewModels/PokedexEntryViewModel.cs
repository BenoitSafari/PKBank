namespace PKBank.Desktop.ViewModels;

/// <summary>One species row of the Pokédex editor (seen/caught flags).</summary>
public sealed class PokedexEntryViewModel(ushort species, string label, bool seen, bool caught) : ViewModelBase
{
    private bool _seen = seen;
    private bool _caught = caught;

    public ushort Species { get; } = species;
    public string Label { get; } = label;

    public bool Seen { get => _seen; set => SetField(ref _seen, value); }
    public bool Caught { get => _caught; set => SetField(ref _caught, value); }
}
