namespace RoguelikeServerMVP.Game.Entities.Behaviour.EntityBehaviour;

public class ChooseOnPlayerDistanceBehaviour<T>(IBehaviour<T> whenClose, IBehaviour<T> whenFar, float distance) : IBehaviour<T> where T : Entity
{
    public void PerformTurn(T self, GameState state)
    {
        if (state.Player.Position.SquaredDistanceTo(self.Position) <= distance * distance)
            whenClose.PerformTurn(self, state);
        else
            whenFar.PerformTurn(self, state);
    }
}