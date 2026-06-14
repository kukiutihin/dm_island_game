namespace RoguelikeServerMVP.Game.Entities.Behaviour;

public class CompositeBehaviour(IEnumerable<IBehaviour> behaviours) : IBehaviour
{
    public void PerformTurn(Entity self, GameState state)
    {
        foreach (var behaviour in behaviours)
            behaviour.PerformTurn(self, state);
    }
}