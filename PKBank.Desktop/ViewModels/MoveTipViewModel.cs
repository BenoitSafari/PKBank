using Avalonia.Media.Imaging;
using PKBank.Desktop.Sprites;
using PKHeX.Core;
using PKBank.Core.Moves;

namespace PKBank.Desktop.ViewModels;

/// <summary>Hover summary of a selected move: type, category, power, accuracy, PP.</summary>
public sealed record MoveTipViewModel(string Name, byte Type, string TypeName, string Category, string Power, string Accuracy, string PP)
{
    public Bitmap? TypeSprite => SpriteService.GetMoveTypeSprite(Type);

    public static MoveTipViewModel? TryCreate(PKM pk, int index, int ppUps)
    {
        var move = pk.GetMove(index);
        if (move == 0)
            return null;

        var strings = GameInfo.Strings;
        var name = move < strings.movelist.Length ? strings.movelist[move] : $"#{move}";
        var type = MoveInfo.GetType(move, pk.Context);
        var typeName = type < strings.types.Length ? strings.types[type] : "?";
        var category = MoveDetails.GetCategory(move, pk.Format, pk.Context).ToString();
        var power = MoveDetails.GetPower(move);
        var accuracy = MoveDetails.GetAccuracy(move);
        var pp = pk.GetMovePP(move, ppUps);

        return new MoveTipViewModel(
            name,
            type,
            typeName,
            category,
            power == 0 ? "—" : power.ToString(),
            accuracy == 0 ? "—" : $"{accuracy}%",
            pp.ToString());
    }
}
