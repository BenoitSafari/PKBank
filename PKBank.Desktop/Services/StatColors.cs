using System;
using Avalonia.Media;
using PKHeX.Core;

namespace PKBank.Desktop.Services;

/// <summary>
/// Stat coloring, mirroring PKHeX.Drawing's ColorUtil and the WinForms StatEditor colors.
/// </summary>
public static class StatColors
{
    private const byte MaxStat = 180;      // clamp absurdly high stats
    private const byte ShiftDownBST = 175; // BST floor isn't 0
    private const float ShiftDivBST = 3;   // scale BST into single-stat range

    public static IBrush BaseStat(int stat)
    {
        var x = (uint)stat >= MaxStat ? 1f : (float)stat / MaxStat;
        return new SolidColorBrush(GetPastelRYG(x));
    }

    public static IBrush BaseStatTotal(int bst)
    {
        var sumToSingle = Math.Max(0, bst - ShiftDownBST) / ShiftDivBST;
        return BaseStat((int)sumToSingle);
    }

    /// <summary>Red→Yellow→Green pastel, 40%/60% blend with white (same formula as ColorUtil.GetPastelRYG).</summary>
    private static Color GetPastelRYG(float x)
    {
        var r = x > .5f ? 510 * (1 - x) : 255;
        var g = x > .5f ? 255 : 510 * x;

        const float white = 0.4f;
        const byte b = (byte)(0xFF * (1 - white));
        var br = (byte)((r * white) + b);
        var bg = (byte)((g * white) + b);

        return Color.FromRgb(br, bg, b);
    }

    // WinForms StatEditor: EVsInvalid=Red, EVsMaxed=LightGreen, EVsFishy=Yellow
    public static readonly IBrush EVTotalInvalid = Brushes.Red;
    public static readonly IBrush EVTotalMaxed = Brushes.LightGreen;
    public static readonly IBrush EVTotalFishy = Brushes.Yellow;

    /// <summary>Field highlight when a single EV reaches its per-stat cap.</summary>
    public static readonly IBrush EVFieldMaxed = new SolidColorBrush(Color.FromArgb(0x66, 0xFF, 0x44, 0x44));

    public static IBrush? GetEVTotalBrush(int evTotal) => EffortValues.GetGrade(evTotal) switch
    {
        EffortValueGrade.Illegal => EVTotalInvalid,
        EffortValueGrade.MaxLegal => EVTotalMaxed,
        EffortValueGrade.MaxEffective => EVTotalFishy,
        _ => null,
    };
}
