namespace RoguelikeServerMVP.Game.Entities.Behaviour.ProjectileBehaviour;

public class DamageEntityOnCollisionBehaviour(int damage) : IBehaviour
{
    private static Entity? GetCollisionInRange(Position from, Position to, GameState state)
    {
        var positions = Position.CreateRectangle(from, to);
        
        return positions
            .Select(state.GetCollision)
            .FirstOrDefault(entity => entity != null);
    }
    
    public void PerformTurn(Entity self, GameState state)
    {
        var collision = GetCollisionInRange(self.PreviousPosition, self.Position, state);

        if (collision == null)
            return;
        
        Console.WriteLine($"DamageOnCollisionBehaviour: Detected collision with {collision}");
        
        self.TryMoveTo(collision.Position);
        
        collision.TakeDamage(damage, state);
        self.Kill(state);
    }
}