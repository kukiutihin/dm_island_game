using RoguelikeServerMVP.Game.Entities.Factory.Preset;
using RoguelikeServerMVP.Game.Generation;

namespace RoguelikeServerMVP.Game;

/// <summary>
/// Builds a fresh game world (room layout, player, mobs). Used both at startup
/// and when the player restarts after dying.
/// </summary>
public static class WorldFactory
{
    public static GameState Create(GameConfig config) => Create(config, config.Seed);

    public static GameState Create(GameConfig config, int seed)
    {
        var room = new Room(config.RoomWidth, config.RoomHeight);

        bool[,] walkable;
        if (config.UseProceduralGeneration)
        {
            walkable = new CaveRoomGenerator().Generate(config.RoomWidth, config.RoomHeight, seed);
        }
        else
        {
            // Fallback: empty room with a solid border.
            walkable = new bool[config.RoomWidth, config.RoomHeight];
            for (var x = 0; x < config.RoomWidth; x++)
            for (var y = 0; y < config.RoomHeight; y++)
                walkable[x, y] = x != 0 && y != 0 && x != config.RoomWidth - 1 && y != config.RoomHeight - 1;
        }

        RoomGen.ApplyToRoom(room, walkable);

        // Spawn the player on a floor tile near the centre.
        var spawn = RoomGen.FindNearestWalkable(
            walkable, new Position(config.RoomWidth / 2, config.RoomHeight / 2));

        var player = new Player(spawn, config.PlayerDefaultMaxHp);

        var state = new GameState(player, room);

        // Materialise walls as entities so they are sent to the client for rendering.
        foreach (var wallPos in RoomGen.WallPositions(walkable))
            state.AddObject(new Wall(wallPos));

        // Place a few mobs, each snapped to the nearest floor tile.
        Position[] preferredMobSpots =
        {
            new(5, 5),
            new(config.RoomWidth - 6, 5),
            new(5, config.RoomHeight - 6),
            new(config.RoomWidth - 6, config.RoomHeight - 6)
        };

        // Keep mobs at least this many tiles from the player's spawn so the
        // player isn't attacked the instant the game starts.
        const int minSpawnDistance = 6;

        state.AddMob(new ModusPonens(RoomGen.FindWalkableAwayFrom(walkable, preferredMobSpots[0], spawn, minSpawnDistance)));
        state.AddMob(new Lambda(RoomGen.FindWalkableAwayFrom(walkable, preferredMobSpots[1], spawn, minSpawnDistance)));
        state.AddMob(new Lambda(RoomGen.FindWalkableAwayFrom(walkable, preferredMobSpots[2], spawn, minSpawnDistance)));
        state.AddMob(new ModusPonens(RoomGen.FindWalkableAwayFrom(walkable, preferredMobSpots[3], spawn, minSpawnDistance)));

        return state;
    }
}
