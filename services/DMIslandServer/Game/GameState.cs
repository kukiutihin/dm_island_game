using RoguelikeServerMVP.Game.Entities;
using RoguelikeServerMVP.Game.Entities.Pickups;
using RoguelikeServerMVP.Game.Events;
using RoguelikeServerMVP.Game.Util;

namespace RoguelikeServerMVP.Game;

public class GameState(Player player, Room room)
{
    public Player Player { get; } = player;
    
    public List<Item> Items { get; } = [];
    public List<Projectile> Projectiles { get; } = [];
    public List<Projectile> DelayedProjectiles { get; } = [];
    public List<Mob> Mobs { get; } = [];
    public Queue<Event> EventQueue { get; } = [];
    
    public List<Entity> StaticObjects { get; } = [];
    private Room _currentRoom = room;
    public int TurnNumber { get; set; }

    private readonly GameRandom _random = new GameRandom(123);


    public Mob? GetMobAt(Position pos)
    {
        return Mobs.FirstOrDefault(m => m.IsAlive && m.Position.Equals(pos));
    }

    private Entity? GetBlockingObjectAt(Position pos)
    {
        // Сначала мобы
        var mob = GetMobAt(pos);
        if (mob is { IsBlocking: true, IsAlive: true })
            return mob;

        // Потом статические объекты (стены и т.п.)
        return StaticObjects.FirstOrDefault(o =>
            o.IsBlocking && o.IsAlive && o.Position.Equals(pos));
    }

    public bool CanMoveTo(Position pos)
    {
        // Проверка границ комнаты
        if (!_currentRoom.IsInside(pos))
            return false;

        // Можно ли пройти по тайлу
        if (!_currentRoom.IsWalkable(pos))
            return false;

        // Блокирующие объекты (мобы, стены)
        var blocking = GetBlockingObjectAt(pos);
        return blocking == null;
    }

    public Entity? GetCollision(Position pos)
    {
        return GetBlockingObjectAt(pos);
    }

    public void AddMob(Mob mob)
    {
        Mobs.Add(mob);
    }
    
    public void AddProjectile(Projectile mob)
    {
        Projectiles.Add(mob);
    }
    
    public void AddItem(Item mob)
    {
        Items.Add(mob);
    }

    public void AddObject(Entity obj)
    {
        StaticObjects.Add(obj);
    }

    public void NextTurn()
    {
        TurnNumber++;
    }

    public GameRandom GetRandom()
    {
        return _random;
    }

    public Room GetCurrentRoom()
    {
        return _currentRoom;
    }

    /// <summary>Swaps in a new active room (used when moving between dungeon rooms).</summary>
    public void SetRoom(Room room)
    {
        _currentRoom = room;
    }

    /// <summary>Removes all mobs, projectiles, effects and static objects of the current room.</summary>
    public void ClearEntities()
    {
        Mobs.Clear();
        Projectiles.Clear();
        Items.Clear();
        StaticObjects.Clear();
        DelayedProjectiles.Clear();
    }

    public void AddEvent(Event e)
    {
        EventQueue.Enqueue(e);
    }

    public void AddProjectileDelayed(Projectile projectile)
    {
        DelayedProjectiles.Add(projectile);
    }
}
