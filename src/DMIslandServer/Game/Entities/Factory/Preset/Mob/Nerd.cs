using RoguelikeServerMVP.Api;
using RoguelikeServerMVP.Game.Entities.Behaviour;
using RoguelikeServerMVP.Game.Entities.Behaviour.EntityBehaviour;

namespace RoguelikeServerMVP.Game.Entities.Factory.Preset.Mob;

/// <summary>
/// Nerd enemy. Has 2 attacks:
/// 1. Attacks in a diamond pattern with theta hat (dist = 2) (when near player)
/// 2. Attacks in a straight line with theta hat (dist = 5)
/// </summary>
public class Nerd(Position position) : Entities.Mob(EntityType.Nerd, position, 20)
{
    private readonly IBehaviour<Entity> _behaviour = 
        new ChooseOnPlayerDistanceBehaviour<Entity>(
            new CompositeBehaviour<Entity>([
                new ChasePlayerBehaviour(1),
                new TimedBehaviour<Entity>(new ThetaDiamondAttackBehaviour(), 8)
            ]),
            new CompositeBehaviour<Entity>([
                new TimedBehaviour<Entity>(new ThetaInLineAttackBehavior(5), 10),
                new RandomWalkBehaviour(5),
            ]),
            3f
        );
    
    public override void PerformTurn(GameState state)
    {
        base.PerformTurn(state);
        _behaviour.PerformTurn(this, state);
    }
}