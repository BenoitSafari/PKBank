using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
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
    private const string TrainerResourcePrefix = "trainer.tr_";
    private const string HeroResourcePrefix = "trainer.hero_";

    /// <summary>Side of the bundled hero sprites, and the size every trainer avatar is drawn at.</summary>
    private const int HeroSize = 32;

    /// <summary>Desaturation strength, matching PKHeX WinForms' mismatch grayscale.</summary>
    private const float GrayscaleIntensity = 0.70f;

    private const string GraySuffix = "#gray";

    private static readonly Assembly Assembly = typeof(SpriteService).Assembly;
    private static readonly ConcurrentDictionary<string, Bitmap?> Cache = new();

    public static Bitmap? GetPokemonSprite(PKM pk) => GetPokemonSprite(pk, false);

    /// <summary>
    ///     Desaturated variant, marking a bank entry the loaded save cannot take.
    /// </summary>
    public static Bitmap? GetPokemonSpriteGrayscale(PKM pk) => GetPokemonSprite(pk, true);

    private static Bitmap? GetPokemonSprite(PKM pk, bool gray)
    {
        if (pk.Species == 0)
            return null;
        if (pk is { IsEgg: true })
            return Load(PkmResourceEgg, gray);

        var formArg = pk is IFormArgument fa ? fa.FormArgument : 0;
        var name = SpriteName.GetResourceStringSprite(pk.Species, pk.Form, pk.Gender, formArg, pk.Context, pk.IsShiny);
        return Load($"{PkmResourcePrefix}{name}.png", gray)
               ?? Load($"{PkmResourcePrefix}_{pk.Species}.png", gray) // fall back to base form
               ?? Load(PkmResourceUnknown, gray);
    }

    public static Bitmap? GetItemSprite(int item) => item <= 0 ? null : Load($"{ItemResourcePrefix}_{item}.png");

    public static Bitmap? GetMysteryGiftSprite(MysteryGift gift)
    {
        if (gift.IsEmpty)
            return null;

        if (gift.IsEntity)
        {
            if (gift.IsEgg)
                return Load(PkmResourceEgg);
            var name = SpriteName.GetResourceStringSprite(gift.Species, gift.Form, gift.Gender, 0, gift.Context,
                gift.IsShiny);
            return Load($"{PkmResourcePrefix}{name}.png")
                   ?? Load($"{PkmResourcePrefix}_{gift.Species}.png") // fall back to base form
                   ?? Load(PkmResourceUnknown);
        }

        if (gift.IsItem)
        {
            var item = (ushort)gift.ItemID;
            if (ItemStorage7USUM.GetCrystalHeld(item, out var value))
                item = value;
            return GetItemSprite(item) ?? Load(PkmResourceUnknown);
        }

        return Load(PkmResourceUnknown);
    }

    public static Bitmap? GetLegalityOverlay(bool valid) => Load(valid ? MiscValidResource : MiscWarnResource);

    public static Bitmap? GetMoveTypeSprite(byte type) => Load($"type.type_icon_s_{type:00}.png");

    /// <summary>
    ///     Front-facing avatar of a save's trainer. Gen 6 saves carry an explicit sprite id
    ///     (WinForms' PlayerSpriteUtil); every other family falls back to the bundled
    ///     per-version-group hero sprite.
    /// </summary>
    public static Bitmap? GetFrontFacingTrainerSprite(GameVersion version, byte? gender, int? spriteId)
    {
        // PKHeX ships its Gen 6 avatars at 40px; the row is 32px, so they are
        // downsampled once and cached at the size they will be drawn.
        if (spriteId is >= 0)
            return LoadScaled($"{TrainerResourcePrefix}{spriteId:00}.png", HeroSize);
        return GetHeroResource(version, gender) is { } name ? Load(name) : null;
    }

    /// <summary>
    ///     Hero sprite per version group. Gen 1/2 has no trainer gender.
    /// </summary>
    private static string? GetHeroResource(GameVersion version, byte? gender)
    {
        var suffix = gender == 1 ? "f" : "m";
        return version switch
        {
            GameVersion.RD or GameVersion.GN or GameVersion.BU or GameVersion.YW
                or GameVersion.RB or GameVersion.RBY => $"{HeroResourcePrefix}rby.png",
            GameVersion.GD or GameVersion.SI or GameVersion.C
                or GameVersion.GS or GameVersion.GSC => $"{HeroResourcePrefix}gsc_{suffix}.png",
            GameVersion.R or GameVersion.S or GameVersion.RS => $"{HeroResourcePrefix}rs_{suffix}.png",
            GameVersion.E or GameVersion.RSE => $"{HeroResourcePrefix}e_{suffix}.png",
            GameVersion.FR or GameVersion.LG or GameVersion.FRLG => $"{HeroResourcePrefix}frlg_{suffix}.png",
            GameVersion.D or GameVersion.P or GameVersion.Pt
                or GameVersion.DP or GameVersion.DPPt => $"{HeroResourcePrefix}dppt_{suffix}.png",
            GameVersion.HG or GameVersion.SS or GameVersion.HGSS => $"{HeroResourcePrefix}hgss_{suffix}.png",
            GameVersion.B or GameVersion.W or GameVersion.BW => $"{HeroResourcePrefix}bw_{suffix}.png",
            GameVersion.B2 or GameVersion.W2 or GameVersion.B2W2 => $"{HeroResourcePrefix}b2w2_{suffix}.png",
            GameVersion.GP or GameVersion.GE or GameVersion.GG => $"{HeroResourcePrefix}gpge_{suffix}.png",
            GameVersion.SW or GameVersion.SH or GameVersion.SWSH => $"{HeroResourcePrefix}swsh_{suffix}.png",
            GameVersion.BD or GameVersion.SP or GameVersion.BDSP => $"{HeroResourcePrefix}bdsp_{suffix}.png",
            _ => null // Gen 6 goes through the sprite id; Gen 7 and PLA/SV have no bundled art yet
        };
    }

    public static Bitmap? GetBallSprite(byte ball) =>
        Load($"{BallResourcePrefix}{SpriteName.GetResourceStringBall(ball)}.png") ?? Load(BallResourceDefault);

    private static Bitmap? Load(string logicalName) => Cache.GetOrAdd(logicalName, static name =>
    {
        using var stream = Assembly.GetManifestResourceStream(name);
        return stream is null ? null : new Bitmap(stream);
    });

    private static Bitmap? Load(string logicalName, bool gray) => gray ? LoadGrayscale(logicalName) : Load(logicalName);

    private static Bitmap? LoadGrayscale(string logicalName) =>
        Cache.GetOrAdd($"{logicalName}{GraySuffix}", _ => Load(logicalName) is { } source ? ToGrayscale(source) : null);

    private static Bitmap ToGrayscale(Bitmap source)
    {
        var size = source.PixelSize;
        var target = new WriteableBitmap(size, source.Dpi, PixelFormat.Bgra8888, AlphaFormat.Unpremul);
        using (var buffer = target.Lock())
        {
            source.CopyPixels(buffer); // transcodes into BGRA8888 for us
            var pixels = new byte[buffer.RowBytes * size.Height];
            Marshal.Copy(buffer.Address, pixels, 0, pixels.Length);
            Desaturate(pixels);
            Marshal.Copy(pixels, 0, buffer.Address, pixels.Length);
        }

        return target;
    }

    /// <summary>
    ///     PKHeX.Drawing's <c>ImageUtil.SetAllColorToGrayScale</c>, on BGRA bytes. Fully transparent
    ///     pixels are left alone so the sprite outline keeps its shape.
    /// </summary>
    private static void Desaturate(Span<byte> pixels)
    {
        const float keep = 1f - GrayscaleIntensity;
        for (var i = 0; i + 3 < pixels.Length; i += 4)
        {
            if (pixels[i + 3] == 0)
                continue;
            var grey = (byte)((0.3 * pixels[i + 2]) + (0.59 * pixels[i + 1]) + (0.11 * pixels[i + 0]));
            for (var channel = 0; channel < 3; channel++)
                pixels[i + channel] = (byte)((pixels[i + channel] * keep) + (grey * GrayscaleIntensity));
        }
    }

    /// <summary>
    ///     Loads a sprite already resized to <paramref name="size" />, bilinear
    ///     (<see cref="BitmapInterpolationMode.LowQuality" /> is Skia's linear filter, no mipmaps).
    ///     Sources already at that size are returned untouched.
    /// </summary>
    private static Bitmap? LoadScaled(string logicalName, int size) =>
        Cache.GetOrAdd($"{logicalName}@{size}", _ =>
        {
            if (Load(logicalName) is not { } source)
                return null;
            return source.PixelSize is { Width: var w, Height: var h } && w == size && h == size
                ? source
                : source.CreateScaledBitmap(new PixelSize(size, size), BitmapInterpolationMode.LowQuality);
        });
}
