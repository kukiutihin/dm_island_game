using RoguelikeServerMVP.Game.Entities.Debuffs;

namespace RoguelikeServerMVP.Game.Entities.Behaviour.ProjectileBehaviour;

public class StunEnemyBehaviour(int duration) : IBehaviour<Projectile>
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
        var collision = GetCollisionInRange(self.PreviousPosition, self.PreviousPosition, state);

        if (collision is Mob mob)
            mob.AddDebuff(DebuffType.Stunned, duration);
        
    }
}