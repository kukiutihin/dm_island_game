using RoguelikeServerMVP.Api;
using RoguelikeServerMVP.Game.Entities.Behaviour;
using RoguelikeServerMVP.Game.Entities.Behaviour.ProjectileBehaviour;

namespace RoguelikeServerMVP.Game.Entities.Factory.Preset.Projectile;

public class ThetaAttack(Position position) : Entities.Projectile(EntityType.ThetaAttack, position, int.MaxValue)
{
    private readonly IBehaviour _behaviour = new CompositeBehaviour([
        new TimedDieBehaviour(2),
        new DamagePlayerOnCollisionBehaviour()
    ]);
    
    public override void PerformTurn(GameState state)
    {
        base.PerformTurn(state);
        _behaviour.PerformTurn(this, state);
    }
    
    protected override void OnDamage(int damage, GameState state) { }
}