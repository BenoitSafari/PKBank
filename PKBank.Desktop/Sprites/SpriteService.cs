using System.Collections.Concurrent;
using System.Reflection;
using Avalonia.Media.Imaging;
using PKHeX.Core;
using PKHeX.Drawing.PokeSprite;

namespace PKBank.Desktop.Sprites;

public static class SpriteService
{
    private const string MiscValidResource = "misc.valid.png";
    private const string MiscWarnResource = "misc.warn.png";
    private const string ItemResourcePrefix = "item.bitem";
    private const string BallResourcePrefix = "ball.";
    private const string BallResourceDefault = "ball._ball4.png";
    private const string PkmResourcePrefix = "pkm.b";
    private const string PkmResourceUnknown = "pkm.b_unknown.png";
    private const string PkmResourceEgg = "pkm.b_egg.png";

    private static readonly Assembly Assembly = typeof(SpriteService).Assembly;
    private static readonly ConcurrentDictionary<string, Bitmap?> Cache = new();

    public static Bitmap? GetPokemonSprite(PKM pk)
    {
        if (pk.Species == 0)
            return null;
        if (pk is { IsEgg: true })
            return Load(PkmResourceEgg);

        var formArg = pk is IFormArgument fa ? fa.FormArgument : 0;
        var name = SpriteName.GetResourceStringSprite(pk.Species, pk.Form, pk.Gender, formArg, pk.Context, pk.IsShiny);
        return Load($"{PkmResourcePrefix}{name}.png")
               ?? Load($"{PkmResourcePrefix}_{pk.Species}.png") // fall back to base form
               ?? Load(PkmResourceUnknown);
    }

    public static Bitmap? GetItemSprite(int item)
    {
        return item <= 0 ? null : Load($"{ItemResourcePrefix}_{item}.png");
    }

    public static Bitmap? GetLegalityOverlay(bool valid)
    {
        return Load(valid ? MiscValidResource : MiscWarnResource);
    }

    /// <summary>Small square type icon (same art WinForms uses in the move selectors).</summary>
    public static Bitmap? GetMoveTypeSprite(byte type)
    {
        return Load($"type.type_icon_s_{type:00}.png");
    }

    public static Bitmap? GetBallSprite(byte ball)
    {
        return Load($"{BallResourcePrefix}{SpriteName.GetResourceStringBall(ball)}.png") ?? Load(BallResourceDefault);
    }

    private static Bitmap? Load(string logicalName) => Cache.GetOrAdd(logicalName, static name =>
    {
        using var stream = Assembly.GetManifestResourceStream(name);
        return stream is null ? null : new Bitmap(stream);
    });
}
