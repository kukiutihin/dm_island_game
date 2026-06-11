using RoguelikeServerMVP.Api;
using RoguelikeServerMVP.Game.Entities.Behaviour;
using RoguelikeServerMVP.Game.Entities.Behaviour.ProjectileBehaviour;

namespace RoguelikeServerMVP.Game.Entities.Factory.Preset.Projectile;

public class ThetaAttackIndicator(Position position, int attackTime) : Entities.Projectile(EntityType.AttackIndicator, position, int.MaxValue)
{
    private readonly AttackIndicatorBehaviour _behaviour = new AttackIndicatorBehaviour(attackTime);
    
    public override void PerformTurn(GameState state)
    {
        base.PerformTurn(state);
        _behaviour.PerformTurn(this, state);
    }
    
    protected override void OnDamage(int damage, GameState state) { }
}