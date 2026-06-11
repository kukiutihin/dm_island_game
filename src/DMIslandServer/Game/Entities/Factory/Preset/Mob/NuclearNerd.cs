using RoguelikeServerMVP.Api;
using RoguelikeServerMVP.Game.Entities.Behaviour;
using RoguelikeServerMVP.Game.Entities.Behaviour.EntityBehaviour;

namespace RoguelikeServerMVP.Game.Entities.Factory.Preset.Mob;

/// <summary>
/// Nuclear nerd: runs away from player and sometimes spawns missiles
/// </summary>
public class NuclearNerd(Position position) : Entities.Mob(EntityType.NuclearNerd, position, 15)
{
    private readonly IBehaviour _behaviour = 
        new ChooseOnPlayerDistanceBehaviour(
            new CompositeBehaviour([
                new TimedBehaviour(new ThetaDiamondAttackBehaviour(), 12)
            ]),
            new CompositeBehaviour([
                new TimedBehaviour(new ThetaRandomAttackBehaviour(10), 15),
                new RandomWalkBehaviour(5, 1),
            ]),
            3f
        );
    
    public override void PerformTurn(GameState state)
    {
        base.PerformTurn(state);
        _behaviour.PerformTurn(this, state);
    }
}