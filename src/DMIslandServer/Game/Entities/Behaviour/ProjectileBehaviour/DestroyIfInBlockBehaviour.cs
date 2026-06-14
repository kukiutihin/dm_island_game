namespace RoguelikeServerMVP.Game.Entities.Behaviour.ProjectileBehaviour;

public class DestroyIfInBlockBehaviour<T> : IBehaviour<T> where T : Entity
{
    public void PerformTurn(T self, GameState state)
    {
        if (state.CanMoveTo(self.Position))
            return;

        self.Kill(state);
    }
}