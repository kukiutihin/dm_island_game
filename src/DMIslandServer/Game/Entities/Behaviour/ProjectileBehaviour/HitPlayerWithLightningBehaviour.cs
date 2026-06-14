using RoguelikeServerMVP.Game.Entities.Factory.Preset.Projectile;

namespace RoguelikeServerMVP.Game.Entities.Behaviour.ProjectileBehaviour;

public class HitPlayerWithLightningBehaviour<T> : IBehaviour<T> where T : Entity
{
    public void PerformTurn(T self, GameState state)
    {
        var position = state.Player.Position;
        state.DelayedProjectiles.Add(new Lightning(position));
    }
}