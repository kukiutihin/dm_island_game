using RoguelikeServerMVP.Api;
using RoguelikeServerMVP.Game.Entities.Behaviour;
using RoguelikeServerMVP.Game.Entities.Behaviour.EntityBehaviour;

namespace RoguelikeServerMVP.Game.Entities.Factory.Preset.Mob;

/// <summary>
/// Mole enemy sometimes pops in a random location, after 5 ticks shoots in all for directions
/// </summary>
public class Mole(Position position) : Entities.Mob(EntityType.Mole, position, 5)
{
    private IBehaviour<Entity> _behaviour = new CompositeBehaviour<Entity>([
        new ShootPlayerBehaviour(1),
        new TimedBehaviour<Entity>(new TeleportBehaviour(), 5)
    ]);

    public override void PerformTurn(GameState state)
    {
        base.PerformTurn(state);
        _behaviour.PerformTurn(this, state);
    }
}