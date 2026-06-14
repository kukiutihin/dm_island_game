using RoguelikeServerMVP.Api;

namespace RoguelikeServerMVP.Game.Entities.Factory.Preset.Mob;

public class GoldenFreddy(Position position) : Entities.Mob(EntityType.GoldenFreddy, position, 3)
{
    public override void PerformTurn(GameState state)
    {
        base.PerformTurn(state);
        Kill(state);
    }
}