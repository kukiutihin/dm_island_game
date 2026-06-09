using RoguelikeServerMVP.Api;
using RoguelikeServerMVP.Game.Entities.Behaviour;
using RoguelikeServerMVP.Game.Entities.Behaviour.EntityBehaviour;

namespace RoguelikeServerMVP.Game.Entities.Factory.Preset;

public class ModusPonens(Position position) : Mob(EntityType.ModusPonens, position, 3)
{
    private readonly IBehaviour _behaviour = new CompositeBehaviour([
        new DamageWhenNearBehaviour(),
        new ChooseOnPlayerDistanceBehaviour(
            new ChasePlayerBehaviour(1),
            new RandomWalkBehaviour(5, 1),
            4f
        )
    ]);
    
    public override void PerformTurn(GameState state)
    {
        base.PerformTurn(state);
        _behaviour.PerformTurn(this, state);
    }
}