namespace RoguelikeServerMVP.Game.Entities.Behaviour.ProjectileBehaviour;

public class DamageEntityOnCollisionBehaviour(int damage) : IBehaviour
{
    private static Entity? GetCollisionInRange(Position from, Position to, GameState state)
    {
        var first = state.GetCollision(from);
        return first ?? state.GetCollision(to);
    }
    
    public void PerformTurn(Entity self, GameState state)
    {
        var collision = GetCollisionInRange(self.PreviousPosition, self.Position, state);

        if (collision == null)
            return;
        
        Console.WriteLine($"DamageOnCollisionBehaviour: Detected collision with {collision}");
        
        collision.TakeDamage(damage, state);
        self.Kill(state);
    }
}