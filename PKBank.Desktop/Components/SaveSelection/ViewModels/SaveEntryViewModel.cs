using Avalonia.Media.Imaging;
using PKBank.Desktop.Services.SaveFiles.Types;
using PKBank.Desktop.Sprites;

namespace PKBank.Desktop.Components.SaveSelection.ViewModels;

/// <summary>One selectable save in the startup selection screen.</summary>
public sealed class SaveEntryViewModel(SaveFileSummary summary)
{
    public SaveFileSummary Summary { get; } = summary;

    public string Path => Summary.Path ?? string.Empty;
    public string FileName => System.IO.Path.GetFileName(Path);
    public string GameName => Summary.GameName;
    public string PlayTime => Summary.PlayTime;

    public string TrainerName => string.IsNullOrWhiteSpace(Summary.TrainerName) ? "—" : Summary.TrainerName;

    public string TrainerId => $"TID {Summary.TrainerId}";

    /// <summary>Empty on formats without a trainer gender, so the row simply omits it.</summary>
    public string Gender => Summary.Gender switch
    {
        1 => "♀",
        0 => "♂",
        _ => string.Empty
    };

    public bool HasGender => Gender.Length != 0;

    public Bitmap? Sprite { get; } =
        SpriteService.GetFrontFacingTrainerSprite(summary.Version, summary.Gender, summary.SpriteId);

    public bool HasSprite => Sprite is not null;

    public string Tooltip => Path;
}
