namespace RoguelikeServerMVP.Game.Entities.Behaviour.ProjectileBehaviour;

public class DamagePlayerOnCollisionBehaviour(int damage) : IBehaviour<Projectile>
{
    public void PerformTurn(Projectile self, GameState state)
    {
        var positionsCollided =
            self.Position == state.Player.Position ||
            self.PreviousPosition == state.Player.Position;

        if (!positionsCollided)
            return;
        
        Console.WriteLine($"Damaging player ({state.Player.Position}/{state.Player.PreviousPosition}): {self.Position}/{self.PreviousPosition}");
        state.Player.TakeDamage(damage, state);
        self.KillOnNextMove(state);
    }
}
