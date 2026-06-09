using RoguelikeServerMVP.Api;
using RoguelikeServerMVP.Game.Entities.Behaviour;
using RoguelikeServerMVP.Game.Entities.Behaviour.EntityBehaviour;

namespace RoguelikeServerMVP.Game.Entities.Factory.Preset;

public class Lambda(Position position) : Mob(EntityType.Lambda, position, 3)
{
    private readonly IBehaviour _behaviour = new CompositeBehaviour([
        new ShootPlayerBehaviour(4),
        new RandomWalkBehaviour(1, 1)
    ]);
    
    public override void PerformTurn(GameState state)
    {
        base.PerformTurn(state);
        _behaviour.PerformTurn(this, state);
    }
}