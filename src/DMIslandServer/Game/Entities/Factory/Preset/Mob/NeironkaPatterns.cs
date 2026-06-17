using System;
using System.Collections.Generic;

namespace RoguelikeServerMVP.Game.Entities.Factory.Preset.Mob;

internal static class NeironkaPatterns
{
    public static IEnumerable<Position> Choose(Random rand, int w, int h) => rand.Next(3) switch
    {
        0 => Checkerboard(w, h),
        1 => Pluses(w, h),
        _ => Text(w, h),
    };

    private static IEnumerable<Position> Checkerboard(int w, int h)
    {
        for (var x = 0; x < w; x++)
        for (var y = 0; y < h; y++)
            if ((x + y) % 2 == 0)
                yield return new Position(x, y);
    }

    private static IEnumerable<Position> Pluses(int w, int h)
    {
        for (var cx = 2; cx < w - 1; cx += 3)
        for (var cy = 2; cy < h - 1; cy += 3)
        {
            yield return new Position(cx, cy);
            yield return new Position(cx + 1, cy);
            yield return new Position(cx - 1, cy);
            yield return new Position(cx, cy + 1);
            yield return new Position(cx, cy - 1);
        }
    }

    private static readonly Dictionary<char, string[]> Font = new()
    {
        ['O'] = ["111", "101", "101", "101", "111"],
        ['C'] = ["111", "100", "100", "100", "111"],
        ['A'] = ["111", "101", "111", "101", "101"],
        ['M'] = ["101", "111", "111", "101", "101"],
        ['L'] = ["100", "100", "100", "100", "111"],
        ['P'] = ["111", "101", "111", "100", "100"],
        ['R'] = ["111", "101", "111", "110", "101"],
    };

    private static IEnumerable<Position> Text(int w, int h)
    {
        foreach (var p in Line("OCAML", w, h / 2 - 6)) yield return p;
        foreach (var p in Line("PRO", w, h / 2 + 1)) yield return p;
    }

    private static IEnumerable<Position> Line(string text, int w, int top)
    {
        const int gw = 3, gh = 5, gap = 1;
        var lineWidth = text.Length * (gw + gap) - gap;
        var cx = Math.Max(1, (w - lineWidth) / 2);
        foreach (var ch in text)
        {
            if (Font.TryGetValue(ch, out var glyph))
                for (var gy = 0; gy < gh; gy++)
                for (var gx = 0; gx < gw; gx++)
                    if (glyph[gy][gx] == '1')
                        yield return new Position(cx + gx, top + gy);
            cx += gw + gap;
        }
    }
}
