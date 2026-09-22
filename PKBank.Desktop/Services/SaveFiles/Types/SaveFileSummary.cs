using PKHeX.Core;

namespace PKBank.Desktop.Services.SaveFiles.Types;

public sealed record SaveFileSummary(
    string? Path,
    byte Generation,
    GameVersion Version,
    int Language,
    string GameName,
    string TrainerName,
    uint TrainerId,
    byte? Gender,
    string PlayTime,
    int? SpriteId
);
