using RoguelikeServerMVP.Api;

namespace RoguelikeServerMVP.Game.Entities;

public abstract class Effect(EntityType name, Position position, int maxHp) : Entity(name, position, maxHp), IActor
{
    public abstract void PerformTurn(GameState state);
    protected override void OnDeath(GameState state) {}
}