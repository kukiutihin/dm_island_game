using RoguelikeServerMVP.Game.Entities.Factory.Preset.Projectile;

namespace RoguelikeServerMVP.Game.Entities.Behaviour.EntityBehaviour;

public class ThetaRandomAttackBehaviour(int count) : IBehaviour<Entity>
{
    public void PerformTurn(Entity self, GameState state)
    {
        for (var i = 0; i < count; i++)
        {
            var position = state.GetRandom().RandomPosition(state.GetCurrentRoom().Width, state.GetCurrentRoom().Height);
            if (!state.CanMoveTo(position)) continue;
            state.AddProjectile(new ThetaAttackIndicator(position, state.GetRandom().Random.Next(3) - 1));
        }
    }
}