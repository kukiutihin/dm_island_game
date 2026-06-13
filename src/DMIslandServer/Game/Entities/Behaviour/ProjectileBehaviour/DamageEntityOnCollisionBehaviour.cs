namespace RoguelikeServerMVP.Game.Entities.Behaviour.ProjectileBehaviour;

public class DamageEntityOnCollisionBehaviour(int damage) : IBehaviour<Projectile>
{
    private static Entity? GetCollisionInRange(Position from, Position to, GameState state)
    {
        return Position.CreateRectangle(from, to)
            .OrderBy(p => from.SquaredDistanceTo(p))
            .Select(state.GetCollision)
            .FirstOrDefault(entity => entity != null);
    }
    
    public void PerformTurn(Projectile self, GameState state)
    {
        var collision = GetCollisionInRange(self.PreviousPosition, self.Position, state);

        if (collision == null)
            return;
        
        Console.WriteLine($"DamageOnCollisionBehaviour: {self.Id} detected collision with {collision}, {collision.Hp} -> {collision.Hp - damage}");
        
        self.TryMoveTo(collision.Position);
        collision.TakeDamage(damage, state);
        self.KillOnNextMove(state);
    }
}