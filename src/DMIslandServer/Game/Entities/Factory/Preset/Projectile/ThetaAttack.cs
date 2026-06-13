using RoguelikeServerMVP.Api;
using RoguelikeServerMVP.Game.Entities.Behaviour;
using RoguelikeServerMVP.Game.Entities.Behaviour.ProjectileBehaviour;

namespace RoguelikeServerMVP.Game.Entities.Factory.Preset.Projectile;

public class ThetaAttack(Position position) : Entities.Projectile(EntityType.ThetaAttack, position, int.MaxValue)
{
    private readonly IBehaviour<Entities.Projectile> _behaviour = new CompositeBehaviour<Entities.Projectile>([
        new TimedDieBehaviour(1),
        new DamagePlayerOnCollisionBehaviour(2)
    ]);
    
    public override void PerformTurn(GameState state)
    {
        base.PerformTurn(state);
        _behaviour.PerformTurn(this, state);
    }
    
    protected override void OnDamage(int damage, GameState state) { }
}