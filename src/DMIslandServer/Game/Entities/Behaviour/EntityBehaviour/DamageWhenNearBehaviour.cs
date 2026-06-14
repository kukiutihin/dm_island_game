using RoguelikeServerMVP.Api;
using RoguelikeServerMVP.Game.Events;

namespace RoguelikeServerMVP.Game.Entities.Behaviour.EntityBehaviour;

public class DamageWhenNearBehaviour : IBehaviour<Entity>
{
    public void PerformTurn(Entity self, GameState state)
    {
        if (state.Player.Position.SquaredDistanceTo(self.Position) < 1.1f)
        {
            state.Player.TakeDamage(1, state);
            // Tell the client this mob just landed a melee hit (drives the attack burst + camera shake).
            state.AddEvent(new Event(EventType.MobAttack, self.Position, self.Id.ToString()));
        }
    }
}