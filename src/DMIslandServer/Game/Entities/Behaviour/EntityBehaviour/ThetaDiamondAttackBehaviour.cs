using RoguelikeServerMVP.Game.Entities.Factory.Preset.Projectile;

namespace RoguelikeServerMVP.Game.Entities.Behaviour.EntityBehaviour;

public class ThetaDiamondAttackBehaviour : IBehaviour<Entity>
{
    private static readonly string[] Attack = ["00100", "01010", "10001", "01010", "00100"];
    
    public void PerformTurn(Entity self, GameState state)
    {
        for (var x = 0; x < 5; x++)
        {
            for (var y = 0; y < 5; y++)
            {
                if (Attack[x][y] != '1') continue;
                var position = self.Position - new Position(x - 2, y - 2);
                if (!(state.CanMoveTo(position) || position == state.Player.Position)) continue;
                state.AddProjectileDelayed(new ThetaAttackIndicator(position, -1));
            }
        }
    }
}