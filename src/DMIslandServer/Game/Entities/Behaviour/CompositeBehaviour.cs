namespace RoguelikeServerMVP.Game.Entities.Behaviour;

public class CompositeBehaviour<T>(IEnumerable<IBehaviour<T>> behaviours) : IBehaviour<T> where T : Entity
{
    public void PerformTurn(T self, GameState state)
    {
        foreach (var behaviour in behaviours)
            behaviour.PerformTurn(self, state);
    }
}