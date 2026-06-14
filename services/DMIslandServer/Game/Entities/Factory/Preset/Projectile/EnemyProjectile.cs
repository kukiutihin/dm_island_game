using RoguelikeServerMVP.Api;
using RoguelikeServerMVP.Game.Entities.Behaviour;
using RoguelikeServerMVP.Game.Entities.Behaviour.ProjectileBehaviour;
using RoguelikeServerMVP.Game.Events;

namespace RoguelikeServerMVP.Game.Entities.Factory.Preset.Projectile;

/// <summary>
/// Enemy projectile
/// </summary>
/// <param name="direction"></param>
/// <param name="position"></param>
public class EnemyProjectile(Direction direction, Position position) : Entities.Projectile(EntityType.EnemyProjectile, position, 1) 
{
    private readonly IBehaviour _behaviour = new CompositeBehaviour([
        new DestroyIfInBlockBehaviour(),
        new StraightLineBehaviour(1, direction),
        new DamagePlayerOnCollisionBehaviour(1),
    ]);
    
    public override void PerformTurn(GameState state)
    {
        base.PerformTurn(state);
        _behaviour.PerformTurn(this, state);
    }

    protected override void OnDeath(GameState state)
    {
        state.AddEvent(new Event(EventType.EnemyProjectilePop, Position, Id.ToString()));
    }

    protected override void OnDamage(int damage, GameState state) { }
}