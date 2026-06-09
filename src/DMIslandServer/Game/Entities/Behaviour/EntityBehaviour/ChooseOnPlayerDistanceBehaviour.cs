namespace RoguelikeServerMVP.Game.Entities.Behaviour.EntityBehaviour;

public class ChooseOnPlayerDistanceBehaviour(IBehaviour whenClose, IBehaviour whenFar, float distance) : IBehaviour
{
    public void PerformTurn(Entity self, GameState state)
    {
        if (state.Player.Position.SquaredDistanceTo(self.Position) <= distance * distance)
            whenClose.PerformTurn(self, state);
        else
            whenFar.PerformTurn(self, state);
    }
}