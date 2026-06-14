using RoguelikeServerMVP.Game.Entities.Factory.Preset.Projectile;

namespace RoguelikeServerMVP.Game.Entities.Behaviour.ProjectileBehaviour;

public class HitPlayerWithLightningBehaviour : IBehaviour
{
    public void PerformTurn(Entity self, GameState state)
    {
        var position = state.Player.Position;
        state.DelayedProjectiles.Add(new Lightning(position));
    }
}