namespace RoguelikeServerMVP.Game.Dungeon;

public class DungeonRoomTemplateStorage
{
    private const string TemplatePath = "RoomTemplates";

    private static IEnumerable<string> GetRoomsInSubDir(string dir)
    {
        return Directory
            .EnumerateFiles(Path.Combine(TemplatePath, dir))
            .Select(File.ReadAllText)
            .ToList();
    }

    public static IEnumerable<string> GetNormalRoomSources() => GetRoomsInSubDir("Normal");
    public static IEnumerable<string> GetItemRooms() => GetRoomsInSubDir("Item");
    public static IEnumerable<string> GetExitRooms() => GetRoomsInSubDir("Exit");
    
    public static IEnumerable<string> ChooseN(Random rand, IEnumerable<string> variants, int count)
    {
        var vars = variants.ToList();
        
        for (var i = 0; i < count; i++)
            yield return vars[rand.Next(vars.Count)];
    }
}