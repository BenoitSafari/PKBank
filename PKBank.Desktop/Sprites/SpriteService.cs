using System.Collections.Concurrent;
using System.Reflection;
using Avalonia.Media.Imaging;
using PKHeX.Core;
using PKHeX.Drawing.PokeSprite;

namespace PKBank.Desktop.Sprites;

/// <summary>
/// Loads Pokémon/item/ball sprites from the PNGs shared with PKHeX.Drawing.PokeSprite,
/// embedded in this assembly under stable logical names (see the csproj).
/// </summary>
public static class SpriteService
{
    private static readonly Assembly Assembly = typeof(SpriteService).Assembly;
    private static readonly ConcurrentDictionary<string, Bitmap?> Cache = new();

    public static Bitmap? GetPokemonSprite(PKM pk)
    {
        if (pk.Species == 0)
            return null;
        if (pk is { IsEgg: true })
            return Load("pkm.b_egg.png");

        var formArg = pk is IFormArgument fa ? fa.FormArgument : 0;
        var name = SpriteName.GetResourceStringSprite(pk.Species, pk.Form, pk.Gender, formArg, pk.Context, pk.IsShiny);
        return Load($"pkm.b{name}.png")
               ?? Load($"pkm.b_{pk.Species}.png") // fall back to base form
               ?? Load("pkm.b_unknown.png");
    }

    public static Bitmap? GetItemSprite(int item) => item <= 0 ? null : Load($"item.bitem_{item}.png");

    public static Bitmap? GetLegalityOverlay(bool valid) => Load(valid ? "misc.valid.png" : "misc.warn.png");

    public static Bitmap? GetBallSprite(byte ball) => Load($"ball.{SpriteName.GetResourceStringBall(ball)}.png") ?? Load("ball._ball4.png");

    private static Bitmap? Load(string logicalName) => Cache.GetOrAdd(logicalName, static name =>
    {
        using var stream = Assembly.GetManifestResourceStream(name);
        return stream is null ? null : new Bitmap(stream);
    });
}
