namespace RoguelikeServerMVP.Game.Entities.Behaviour.ProjectileBehaviour;

public class DamagePlayerOnCollisionBehaviour : IBehaviour
{
    public void PerformTurn(Entity self, GameState state)
    {
        if (self.Position != state.Player.Position && self.PreviousPosition != state.Player.Position)
            return;
        
        Console.WriteLine($"DamagePlayerOnCollisionBehaviour: Damaging a player {self.Position}/{self.PreviousPosition}");
        
        state.Player.TakeDamage(1, state);
    }
}