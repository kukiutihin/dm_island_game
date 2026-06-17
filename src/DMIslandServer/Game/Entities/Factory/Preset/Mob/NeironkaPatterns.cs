using System.Collections.Generic;

namespace RoguelikeServerMVP.Game.Entities.Factory.Preset.Mob;

internal static class NeironkaPatterns
{
    public static IEnumerable<Position> Choose(System.Random rand, int w, int h) => Checkerboard(w, h);

    private static IEnumerable<Position> Checkerboard(int w, int h)
    {
        for (var x = 0; x < w; x++)
        for (var y = 0; y < h; y++)
            if ((x + y) % 2 == 0)
                yield return new Position(x, y);
    }
}
