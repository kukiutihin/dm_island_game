namespace RoguelikeServerMVP.Game.Entities.Behaviour;

public interface IBehaviour<in T> where T : Entity
{
    public void PerformTurn(T self, GameState state);
}