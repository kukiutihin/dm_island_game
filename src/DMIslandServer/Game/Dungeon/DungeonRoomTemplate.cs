using RoguelikeServerMVP.Api;

namespace RoguelikeServerMVP.Game.Dungeon;

public class DungeonRoomTemplate(int floor)
{
    public List<MobSpawn> MobSpawns { get; } = [];
    public List<Position> WallSpawns { get; } = [];
    public List<ItemSpawn> ItemSpawns { get; } = [];
    
    public static DungeonRoomTemplate Empty(int floor) => new DungeonRoomTemplate(floor);

    public static DungeonRoomTemplate OfString(Random rand, int floor, string source)
    {
        var lines = source.Split('\n');
        var template = new DungeonRoomTemplate(floor);
        
        if (lines.Length != 7)
            throw new Exception("Invalid room template: line count is not 5");
        
        for (var x = 0; x < 11; x++)
        {
            for (var y = 0; y < 7; y++)
            {
                if (lines[y].Length != 11)
                    throw new Exception("Invalid room template: line length is not 11");
                
                if (lines[y][x] == 'S')
                    template.WallSpawns.Add(new Position(x + 1, y + 1));
                
                if (lines[y][x] == 'M')
                    template.MobSpawns.Add(ChooseMob(rand, floor, new Position(x + 1, y + 1)));
                
                if (lines[y][x] == 'I')
                    template.ItemSpawns.Add(ChooseItem(rand, new Position(x + 1, y + 1)));
            }
        }
        
        return template;
    }
    
    private static ItemSpawn ChooseItem(Random rand, Position position)
    {
        List<ItemType> variants = [ItemType.Haskell,
            ItemType.Python3, ItemType.Cpp, ItemType.Java,
            ItemType.OCaml, ItemType.Zig, ItemType.Rust,
            ItemType.AnsiC, ItemType.FSharp, ItemType.Roc,
            ItemType.OneF, ItemType.JavaScript, ItemType.TypeScript,
            ItemType.Go, ItemType.Kotlin, ItemType.Asm, ItemType.Scala3
        ];
        var type = variants[rand.Next(variants.Count)];
        return new ItemSpawn(type, position);
    }

    private static MobSpawn ChooseMob(Random rand, int floor, Position position)
    {
        List<EntityType> enemyTypes = floor switch
        {
            1 => [EntityType.ModusPonens, EntityType.Lambda, EntityType.Monad],
            2 => [EntityType.ModusPonens, EntityType.Lambda, EntityType.Skolem],
            3 => [EntityType.Mole, EntityType.Lambda, EntityType.Nerd],
            4 => [EntityType.Nerd, EntityType.Lambda, EntityType.NuclearNerd],
            _ => throw new ArgumentOutOfRangeException(nameof(floor), floor, null)
        };
        var enemyType = enemyTypes[rand.Next(enemyTypes.Count)];
        return new MobSpawn(enemyType, position);
    }
}