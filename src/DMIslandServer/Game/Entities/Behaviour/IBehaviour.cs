namespace RoguelikeServerMVP.Game.Entities.Behaviour;

public interface IBehaviour
{
    public void PerformTurn(Entity self, GameState state);
}