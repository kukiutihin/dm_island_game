using RoguelikeServerMVP.Api;
using RoguelikeServerMVP.Game.Entities.Behaviour;
using RoguelikeServerMVP.Game.Entities.Behaviour.EntityBehaviour;

namespace RoguelikeServerMVP.Game.Entities.Factory.Preset.Mob;

/// <summary>
/// Skolem enemy has a lot of health, but does not attack.
/// When player is near he just blocks them
/// </summary>
public class Skolem(Position position) : Entities.Mob(EntityType.Skolem, position, 10)
{
    private readonly IBehaviour<Entity> _behaviour = 
        new ChooseOnPlayerDistanceBehaviour<Entity>(
            new ChasePlayerBehaviour(2),
            new TimedBehaviour<Entity>(new RandomWalkBehaviour(3), 1),
            3f
        );
    
    public override void PerformTurn(GameState state)
    {
        base.PerformTurn(state);
        _behaviour.PerformTurn(this, state);
    }
}