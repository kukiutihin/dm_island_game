using RoguelikeServerMVP.Game.Entities.Pickups;

namespace RoguelikeServerMVP.Game.Entities.Behaviour.ItemBehaviour;

public class PickupOnCollisionBehaviour : IBehaviour
{
    public void PerformTurn(Entity self, GameState state)
    {
        if (self.Position != state.Player.Position)
            return;

        if (self is not Item item)
        {
            Console.WriteLine("Warning: PickupOnCollisionBehaviour is called on a non-item object");
            return;
        }

        state.Player.PickupItem(item.GetItemType());
        self.Kill(state);
    }
}