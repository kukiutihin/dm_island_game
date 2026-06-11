using RoguelikeServerMVP.Game.Entities.Factory.Preset.Projectile;

namespace RoguelikeServerMVP.Game.Entities.Behaviour.ProjectileBehaviour;

public class AttackIndicatorBehaviour(int ttl) : IBehaviour
{
    private int _ttl = ttl;
    public void PerformTurn(Entity self, GameState state)
    {
        if (_ttl-- >= 0)
            return;
        
        state.AddProjectileDelayed(new ThetaAttack(self.Position));
        self.Kill(state);
    }
}