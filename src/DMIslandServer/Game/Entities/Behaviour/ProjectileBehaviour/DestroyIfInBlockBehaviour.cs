namespace RoguelikeServerMVP.Game.Entities.Behaviour.ProjectileBehaviour;

public class DestroyIfInBlockBehaviour : IBehaviour
{
    public void PerformTurn(Entity self, GameState state)
    {
        if (state.CanMoveTo(self.Position))
            return;

        self.Kill(state);
    }
}