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
