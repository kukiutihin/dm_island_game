using RoguelikeServerMVP.Game.Entities.Factory.Preset.Projectile;

namespace RoguelikeServerMVP.Game.Entities.Behaviour.EntityBehaviour;

public class ThetaInLineAttackBehavior(int range) : IBehaviour
{
    public void PerformTurn(Entity self, GameState state)
    {
        List<Direction> directions = [Direction.Up, Direction.Down, Direction.Left, Direction.Right];
        var bestDirection = directions.MinBy(x => self.Position.Move(x).SquaredDistanceTo(state.Player.Position));

        var position = self.Position.Move(bestDirection);
        for (var i = 0; i < range; i++)
        {
            if (!state.CanMoveTo(position)) break;
            state.AddProjectile(new ThetaAttackIndicator(position, i / 2 - 1));
            position = position.Move(bestDirection);
        }
    }
}