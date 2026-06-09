using RoguelikeServerMVP.Api;

namespace RoguelikeServerMVP.Game.Entities;

public abstract class Projectile(EntityType name, Position position, int maxHp) : Entity(name, position, maxHp), IActor
{
    public override bool IsBlocking => false;

    public virtual void PerformTurn(GameState state)
    {
        PreviousPosition = Position;
    }

    protected override void OnDeath(GameState state)
    {
    }
}