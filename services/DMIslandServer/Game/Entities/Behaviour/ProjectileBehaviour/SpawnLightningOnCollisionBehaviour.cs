using RoguelikeServerMVP.Game.Entities.Factory.Preset.Projectile;

namespace RoguelikeServerMVP.Game.Entities.Behaviour.ProjectileBehaviour;

public class SpawnLightningOnCollisionBehaviour : IBehaviour
{
    private static Entity? GetCollisionInRange(Position from, Position to, GameState state)
    {
        return Position.CreateRectangle(from, to)
            .OrderBy(p => from.SquaredDistanceTo(p))
            .Select(state.GetCollision)
            .FirstOrDefault(entity => entity != null);
    }
    
    public void PerformTurn(Entity self, GameState state)
    {
        var collision = GetCollisionInRange(self.PreviousPosition, self.Position, state);

        if (collision == null)
            return;
        
        Console.WriteLine($"Lightning spawn: Detected collision with {collision}");
        
        self.TryMoveTo(collision.Position);
        state.AddProjectileDelayed(new Lightning(collision.Position));
    }
}