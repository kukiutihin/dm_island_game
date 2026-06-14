namespace RoguelikeServerMVP.Game.Entities.Behaviour;

public class ChanceBehaviour(IBehaviour behaviour, double chance) : IBehaviour
{
    public void PerformTurn(Entity self, GameState state)
    {
        if (state.GetRandom().Random.NextDouble() < chance)
            behaviour.PerformTurn(self, state);
    }
}