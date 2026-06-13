using RoguelikeServerMVP.Api;
using RoguelikeServerMVP.Game.Entities.Behaviour;
using RoguelikeServerMVP.Game.Entities.Behaviour.EntityBehaviour;

namespace RoguelikeServerMVP.Game.Entities.Factory.Preset.Mob;

/// <summary>
/// Nuclear nerd: runs away from player and sometimes spawns missiles
/// </summary>
public class NuclearNerd(Position position) : Entities.Mob(EntityType.NuclearNerd, position, 15)
{
    private readonly IBehaviour<Entity> _behaviour = 
        new ChooseOnPlayerDistanceBehaviour<Entity>(
            new CompositeBehaviour<Entity>([
                new TimedBehaviour<Entity>(new ThetaDiamondAttackBehaviour(), 12)
            ]),
            new CompositeBehaviour<Entity>([
                new TimedBehaviour<Entity>(new ThetaRandomAttackBehaviour(24), 15),
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