using RoguelikeServerMVP.Api;
using RoguelikeServerMVP.Game.Entities.Behaviour;
using RoguelikeServerMVP.Game.Entities.Behaviour.ProjectileBehaviour;
using RoguelikeServerMVP.Game.Events;

namespace RoguelikeServerMVP.Game.Entities.Factory.Preset;

public class EnemyProjectile(Direction direction, Position position) : Projectile(EntityType.Tear, position, 1) 
{
    private readonly IBehaviour _behaviour = new CompositeBehaviour([
        new DamagePlayerOnCollisionBehaviour(),
        new StraightLineBehaviour(1, direction)
    ]);
    
    public override void PerformTurn(GameState state)
    {
        base.PerformTurn(state);
        _behaviour.PerformTurn(this, state);
    }

    protected override void OnDeath(GameState state)
    {
        state.AddEvent(new Event(EventType.TearPop, Position, Type.ToString()));
    }

    protected override void OnDamage(int damage, GameState state)
    {
        
    }
}