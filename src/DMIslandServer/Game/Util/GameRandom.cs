namespace RoguelikeServerMVP.Game.Util;

public class GameRandom(int seed)
{
    private static readonly Random _random = new Random();
    
    public Random Random => _random;

    public Position RandomPosition(int width, int height)
    {
        return new Position(_random.Next(width), _random.Next(height));
    }

    public bool OneIn(int bound)
    {
        return _random.Next(bound) == 0;
    }
}