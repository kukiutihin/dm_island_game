namespace RoguelikeServerMVP.Game.Entities.Behaviour.ProjectileBehaviour;

public class DamagePlayerOnCollisionBehaviour : IBehaviour
{
    public void PerformTurn(Entity self, GameState state)
    {
        var positionsCollided =
            self.Position == state.Player.Position ||
            self.PreviousPosition == state.Player.Position;
        
        if (positionsCollided)
        {
            self.Kill(state);
            Console.WriteLine($"Damaging player ({state.Player.Position}/{state.Player.PreviousPosition}): {self.Position}/{self.PreviousPosition}");
            state.Player.TakeDamage(1, state);
        }
    }
}