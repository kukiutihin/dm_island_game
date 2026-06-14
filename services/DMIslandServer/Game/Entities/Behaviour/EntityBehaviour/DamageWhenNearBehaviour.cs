namespace RoguelikeServerMVP.Game.Entities.Behaviour.EntityBehaviour;

public class DamageWhenNearBehaviour : IBehaviour
{
    public void PerformTurn(Entity self, GameState state)
    {
        if (state.Player.Position.SquaredDistanceTo(self.Position) < 1.1f)
        {
            state.Player.TakeDamage(1, state);
        }
    }
}