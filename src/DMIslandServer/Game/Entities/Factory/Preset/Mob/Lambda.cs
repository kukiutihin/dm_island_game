using RoguelikeServerMVP.Api;
using RoguelikeServerMVP.Game.Entities.Behaviour;
using RoguelikeServerMVP.Game.Entities.Behaviour.EntityBehaviour;

namespace RoguelikeServerMVP.Game.Entities.Factory.Preset.Mob;

/// <summary>
/// Lambda mob is flying around and attacking with projectiles
/// </summary>
/// <param name="position"></param>
public class Lambda(Position position) : Entities.Mob(EntityType.Lambda, position, 3)
{
    private readonly IBehaviour<Entity> _behaviour = new CompositeBehaviour<Entity>([
        new ShootPlayerBehaviour(4),
        new RandomWalkBehaviour(1)
    ]);
    
    public override void PerformTurn(GameState state)
    {
        base.PerformTurn(state);
        _behaviour.PerformTurn(this, state);
    }
}