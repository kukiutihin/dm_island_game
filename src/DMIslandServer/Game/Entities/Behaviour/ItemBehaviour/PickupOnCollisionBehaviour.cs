using RoguelikeServerMVP.Game.Entities.Pickups;

namespace RoguelikeServerMVP.Game.Entities.Behaviour.ItemBehaviour;

public class PickupOnCollisionBehaviour : IBehaviour<Item>
{
    public void PerformTurn(Item self, GameState state)
    {
        if (self.Position != state.Player.Position)
            return;

        state.Player.PickupItem(self.GetItemType());
        self.Kill(state);
    }
}