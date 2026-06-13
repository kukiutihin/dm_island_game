namespace RoguelikeServerMVP.Game.Entities.Behaviour;

public class ChanceBehaviour<T>(IBehaviour<T> behaviour, double chance) : IBehaviour<T> where T : Entity
{
    public void PerformTurn(T self, GameState state)
    {
        if (state.GetRandom().Random.NextDouble() < chance)
            behaviour.PerformTurn(self, state);
    }
}