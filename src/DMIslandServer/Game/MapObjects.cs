using RoguelikeServerMVP.Api;
using RoguelikeServerMVP.Game;

namespace RoguelikeServerMVP.Game;

/// <summary>
/// Стена: неподвижный, непроходимый объект.
/// </summary>
public class Wall : Entity
{
    public override bool IsBlocking => true;
    
    protected override void OnDeath(GameState state)
    {
        
    }

    protected override void OnDamage(int damage, GameState state)
    {
        
    }

    public Wall(Position position)
        : base(EntityType.Wall, position, int.MaxValue)
    {
    }

    public void TakeDamage(int amount)
    {
        // Если стены неразрушаемые — ничего не делаем.
        // Если нужно разрушение, здесь можно уменьшать Hp и
        // убирать объект из мира, когда Hp <= 0.
    }
}

/// <summary>
/// The floor exit: a walkable (non-blocking) object that always sits in the exit
/// room. While the floor isn't fully cleared it is shown deactivated (and does
/// nothing); once the floor is cleared it activates and stepping onto it transfers
/// the player to the next floor.
/// </summary>
public class Exit : Entity
{
    public bool IsActive { get; }

    public override bool IsBlocking => false;

    protected override void OnDeath(GameState state) { }
    protected override void OnDamage(int damage, GameState state) { }

    public Exit(Position position, bool isActive)
        : base(isActive ? EntityType.Exit : EntityType.ExitClosed, position, int.MaxValue)
    {
        IsActive = isActive;
    }
}
