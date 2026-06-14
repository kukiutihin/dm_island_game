using RoguelikeServerMVP.Api;
using RoguelikeServerMVP.Game.Entities.Behaviour;
using RoguelikeServerMVP.Game.Entities.Behaviour.EntityBehaviour;

namespace RoguelikeServerMVP.Game.Entities.Factory.Preset.Mob;

/// <summary>
/// Monad enemy. Has a single attack:
/// When player is on the same lane, after 1 tick tries to ramp them
/// Roaming mode: cling to walls
/// </summary>
public class Monad(Position position) : Entities.Mob(EntityType.Monad, position, 4)
{
    private readonly IBehaviour<Entity> _behaviour =
        new CompositeBehaviour<Entity>([
            new DamageWhenNearBehaviour(),
            new TimedBehaviour<Entity>(new FlyDiagonallyBehaviour(), 2),
        ]);
    
    public override void PerformTurn(GameState state)
    {
        base.PerformTurn(state);
        _behaviour.PerformTurn(this, state);
    }
}