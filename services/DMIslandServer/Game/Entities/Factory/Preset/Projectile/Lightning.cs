using RoguelikeServerMVP.Api;
using RoguelikeServerMVP.Game.Entities.Behaviour;
using RoguelikeServerMVP.Game.Entities.Behaviour.ProjectileBehaviour;

namespace RoguelikeServerMVP.Game.Entities.Factory.Preset.Projectile;

public class Lightning(Position position) : Entities.Projectile(EntityType.Lightning, position, int.MaxValue)
{
    private readonly IBehaviour _behaviour = new CompositeBehaviour([
        new TimedDieBehaviour(0),
        new DamagePlayerOnCollisionBehaviour(2),
        new DamageEntityOnCollisionBehaviour(4),
    ]);

    public override void PerformTurn(GameState state)
    {
        _behaviour.PerformTurn(this, state);
    }

    protected override void OnDamage(int damage, GameState state) { }
}