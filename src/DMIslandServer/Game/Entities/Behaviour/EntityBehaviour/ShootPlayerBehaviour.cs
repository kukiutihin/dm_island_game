using RoguelikeServerMVP.Game.Entities.Factory.Preset;

namespace RoguelikeServerMVP.Game.Entities.Behaviour.EntityBehaviour;

public class ShootPlayerBehaviour(int cooldown) : IBehaviour
{
    private int _currentCooldown = cooldown;
    
    public void PerformTurn(Entity self, GameState state)
    {
        if (_currentCooldown > 0) {
            _currentCooldown--;
            return;
        }
        
        _currentCooldown = cooldown;

        List<Direction> directions = [Direction.Up, Direction.Down, Direction.Left, Direction.Right];
        var bestDirection = directions.MinBy(x => self.Position.Move(x).SquaredDistanceTo(state.Player.Position));
        
        var projectile = new EnemyProjectile(bestDirection, self.Position);
        state.AddProjectile(projectile);
    }
}